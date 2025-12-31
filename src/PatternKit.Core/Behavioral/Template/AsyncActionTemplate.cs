namespace PatternKit.Behavioral.Template;

/// <summary>
/// Fluent, allocation-light async ActionTemplate that defines an algorithm skeleton with optional async hooks,
/// executing side effects without returning a result.
/// Build once, then call ExecuteAsync or TryExecuteAsync.
/// </summary>
/// <typeparam name="TContext">Input context type.</typeparam>
/// <remarks>
/// <para>
/// This is the async action (void-returning) counterpart to <see cref="AsyncTemplate{TContext,TResult}"/>.
/// Use when the algorithm performs async side effects rather than computing a result.
/// </para>
/// <para>
/// <b>Thread-safety:</b> The built template is immutable and thread-safe.
/// Use <see cref="Builder.Synchronized"/> to serialize concurrent executions.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var template = AsyncActionTemplate&lt;OrderContext&gt;.Create(
///         async (ctx, ct) => await ProcessOrderAsync(ctx, ct))
///     .Before(async (ctx, ct) => await ctx.LogAsync("Starting", ct))
///     .After(async (ctx, ct) => await ctx.LogAsync("Completed", ct))
///     .Build();
///
/// await template.ExecuteAsync(new OrderContext());
/// </code>
/// </example>
public sealed class AsyncActionTemplate<TContext>
{
    /// <summary>Async hook executed before the main step.</summary>
    public delegate ValueTask BeforeHookAsync(TContext context, CancellationToken cancellationToken);

    /// <summary>The main async step to execute.</summary>
    public delegate ValueTask StepHookAsync(TContext context, CancellationToken cancellationToken);

    /// <summary>Async hook executed after the main step completes successfully.</summary>
    public delegate ValueTask AfterHookAsync(TContext context, CancellationToken cancellationToken);

    /// <summary>Async hook executed when an error occurs.</summary>
    public delegate ValueTask ErrorHookAsync(TContext context, string error, CancellationToken cancellationToken);

    private readonly BeforeHookAsync[] _before;
    private readonly StepHookAsync _step;
    private readonly AfterHookAsync[] _after;
    private readonly ErrorHookAsync[] _onError;
    private readonly bool _synchronized;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private AsyncActionTemplate(
        List<BeforeHookAsync> before,
        StepHookAsync step,
        List<AfterHookAsync> after,
        List<ErrorHookAsync> onError,
        bool synchronized)
    {
        _before = before.ToArray();
        _step = step;
        _after = after.ToArray();
        _onError = onError.ToArray();
        _synchronized = synchronized;
    }

    /// <summary>Execute the template: before -> step -> after. Throws on errors.</summary>
    public async Task ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        if (_synchronized)
        {
            await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ExecuteCoreAsync(context, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _mutex.Release();
            }
            return;
        }

        await ExecuteCoreAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Like ExecuteAsync, but returns false with an error message instead of throwing.</summary>
    public async Task<(bool ok, string? error)> TryExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex)
        {
            foreach (var h in _onError)
            {
                try
                {
                    await h(context, ex.Message, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    /* swallow error hooks */
                }
            }

            return (false, ex.Message);
        }
    }

    private async Task ExecuteCoreAsync(TContext context, CancellationToken cancellationToken)
    {
        foreach (var h in _before)
            await h(context, cancellationToken).ConfigureAwait(false);

        await _step(context, cancellationToken).ConfigureAwait(false);

        foreach (var h in _after)
            await h(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Create a builder for this async template.</summary>
    public static Builder Create(StepHookAsync step) => new(step);

    /// <summary>Fluent builder for <see cref="AsyncActionTemplate{TContext}"/>.</summary>
    public sealed class Builder
    {
        private readonly List<BeforeHookAsync> _before = new(2);
        private readonly StepHookAsync _step;
        private readonly List<AfterHookAsync> _after = new(2);
        private readonly List<ErrorHookAsync> _onError = new(1);
        private bool _synchronized;

        internal Builder(StepHookAsync step)
        {
            _step = step ?? throw new ArgumentNullException(nameof(step));
        }

        /// <summary>Add an async pre-step hook.</summary>
        public Builder Before(BeforeHookAsync before)
        {
            _before.Add(before);
            return this;
        }

        /// <summary>Add a sync pre-step hook.</summary>
        public Builder Before(Action<TContext> before)
        {
            _before.Add((ctx, _) =>
            {
                before(ctx);
                return default;
            });
            return this;
        }

        /// <summary>Add an async post-step hook.</summary>
        public Builder After(AfterHookAsync after)
        {
            _after.Add(after);
            return this;
        }

        /// <summary>Add a sync post-step hook.</summary>
        public Builder After(Action<TContext> after)
        {
            _after.Add((ctx, _) =>
            {
                after(ctx);
                return default;
            });
            return this;
        }

        /// <summary>Add an async error hook invoked when ExecuteAsync would throw.</summary>
        public Builder OnError(ErrorHookAsync onError)
        {
            _onError.Add(onError);
            return this;
        }

        /// <summary>Add a sync error hook invoked when ExecuteAsync would throw.</summary>
        public Builder OnError(Action<TContext, string> onError)
        {
            _onError.Add((ctx, err, _) =>
            {
                onError(ctx, err);
                return default;
            });
            return this;
        }

        /// <summary>Synchronize executions across threads.</summary>
        public Builder Synchronized(bool synchronized = true)
        {
            _synchronized = synchronized;
            return this;
        }

        /// <summary>Build an immutable async template.</summary>
        public AsyncActionTemplate<TContext> Build()
            => new(_before, _step, _after, _onError, _synchronized);
    }
}
