namespace PatternKit.Behavioral.Template;

/// <summary>
/// Fluent, allocation-light async Template that defines an algorithm skeleton with optional async hooks.
/// Build once, then call ExecuteAsync or TryExecuteAsync.
/// </summary>
/// <typeparam name="TContext">Input context type.</typeparam>
/// <typeparam name="TResult">Result type.</typeparam>
public sealed class AsyncTemplate<TContext, TResult>
{
    public delegate ValueTask BeforeHookAsync(TContext context, CancellationToken cancellationToken);

    public delegate ValueTask<TResult> StepHookAsync(TContext context, CancellationToken cancellationToken);

    public delegate ValueTask AfterHookAsync(TContext context, TResult result, CancellationToken cancellationToken);

    public delegate ValueTask ErrorHookAsync(TContext context, string error, CancellationToken cancellationToken);

    private readonly BeforeHookAsync[] _before;
    private readonly StepHookAsync _step;
    private readonly AfterHookAsync[] _after;
    private readonly ErrorHookAsync[] _onError;
    private readonly bool _synchronized;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    private AsyncTemplate(
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

    /// <summary>Execute the template: before → step → after. Throws on errors.</summary>
    public async Task<TResult> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        if (_synchronized)
        {
            await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await ExecuteCoreAsync(context, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _mutex.Release();
            }
        }

        return await ExecuteCoreAsync(context, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Like ExecuteAsync, but returns false with an error message instead of throwing.</summary>
    public async Task<(bool ok, TResult? result, string? error)> TryExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var res = await ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
            return (true, res, null);
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

            return (false, default, ex.Message);
        }
    }

    private async Task<TResult> ExecuteCoreAsync(TContext context, CancellationToken cancellationToken)
    {
        foreach (var h in _before)
            await h(context, cancellationToken).ConfigureAwait(false);

        var result = await _step(context, cancellationToken).ConfigureAwait(false);

        foreach (var h in _after)
            await h(context, result, cancellationToken).ConfigureAwait(false);

        return result;
    }

    /// <summary>Create a builder for this async template.</summary>
    public static Builder Create(StepHookAsync step) => new(step);

    /// <summary>Fluent builder for <see cref="AsyncTemplate{TContext,TResult}"/>.</summary>
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
        public Builder After(Action<TContext, TResult> after)
        {
            _after.Add((ctx, res, _) =>
            {
                after(ctx, res);
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
        public AsyncTemplate<TContext, TResult> Build()
            => new(_before, _step, _after, _onError, _synchronized);
    }
}