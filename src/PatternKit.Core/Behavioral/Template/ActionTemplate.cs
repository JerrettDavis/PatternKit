using System.Diagnostics.CodeAnalysis;

namespace PatternKit.Behavioral.Template;

/// <summary>
/// Fluent, allocation-light ActionTemplate that defines an algorithm skeleton with optional hooks,
/// executing side effects without returning a result.
/// Build once, then call Execute or TryExecute.
/// </summary>
/// <typeparam name="TContext">Input context type.</typeparam>
/// <remarks>
/// <para>
/// This is the action (void-returning) counterpart to <see cref="Template{TContext,TResult}"/>.
/// Use when the algorithm performs side effects rather than computing a result.
/// </para>
/// <para>
/// <b>Thread-safety:</b> The built template is immutable and thread-safe.
/// Use <see cref="Builder.Synchronized"/> to serialize concurrent executions.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var template = ActionTemplate&lt;OrderContext&gt;.Create(ctx => ProcessOrder(ctx))
///     .Before(ctx => ctx.Log("Starting"))
///     .After(ctx => ctx.Log("Completed"))
///     .OnError((ctx, err) => ctx.Log($"Error: {err}"))
///     .Build();
///
/// template.Execute(new OrderContext());
/// </code>
/// </example>
public sealed class ActionTemplate<TContext>
{
    /// <summary>Hook executed before the main step.</summary>
    public delegate void BeforeHook(TContext context);

    /// <summary>The main step to execute.</summary>
    public delegate void StepHook(TContext context);

    /// <summary>Hook executed after the main step completes successfully.</summary>
    public delegate void AfterHook(TContext context);

    /// <summary>Hook executed when an error occurs.</summary>
    public delegate void ErrorHook(TContext context, string error);

    private readonly BeforeHook? _before;
    private readonly StepHook _step;
    private readonly AfterHook? _after;
    private readonly ErrorHook? _onError;
    private readonly bool _synchronized;
    private readonly object _sync = new();

    private ActionTemplate(BeforeHook? before, StepHook step, AfterHook? after, ErrorHook? onError, bool synchronized)
    {
        _before = before;
        _step = step;
        _after = after;
        _onError = onError;
        _synchronized = synchronized;
    }

    /// <summary>
    /// Execute the template: before -> step -> after. Throws on errors.
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentlySynchronizedField")]
    public void Execute(TContext context)
    {
        if (_synchronized)
        {
            lock (_sync)
            {
                ExecuteCore(context);
            }
            return;
        }

        ExecuteCore(context);
    }

    private void ExecuteCore(TContext context)
    {
        _before?.Invoke(context);
        _step(context);
        _after?.Invoke(context);
    }

    /// <summary>
    /// Like <see cref="Execute"/>, but returns false with an error message instead of throwing.
    /// </summary>
    public bool TryExecute(TContext context, out string? error)
    {
        try
        {
            if (_synchronized)
            {
                lock (_sync)
                {
                    ExecuteCore(context);
                    error = null;
                    return true;
                }
            }

            ExecuteCore(context);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            _onError?.Invoke(context, ex.Message);
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Create a builder for this template.</summary>
    public static Builder Create(StepHook step) => new(step);

    /// <summary>Fluent builder for <see cref="ActionTemplate{TContext}"/>.</summary>
    public sealed class Builder
    {
        private BeforeHook? _before;
        private readonly StepHook _step;
        private AfterHook? _after;
        private ErrorHook? _onError;
        private bool _synchronized;

        internal Builder(StepHook step)
        {
            _step = step ?? throw new ArgumentNullException(nameof(step));
        }

        /// <summary>Add a pre-step hook.</summary>
        public Builder Before(BeforeHook before)
        {
            _before = (_before is null) ? before : (BeforeHook)Delegate.Combine(_before, before);
            return this;
        }

        /// <summary>Add a post-step hook.</summary>
        public Builder After(AfterHook after)
        {
            _after = (_after is null) ? after : (AfterHook)Delegate.Combine(_after, after);
            return this;
        }

        /// <summary>Add an error hook invoked when <see cref="Execute"/> would throw.</summary>
        public Builder OnError(ErrorHook onError)
        {
            _onError = (_onError is null) ? onError : (ErrorHook)Delegate.Combine(_onError, onError);
            return this;
        }

        /// <summary>Synchronize executions across threads.</summary>
        public Builder Synchronized(bool synchronized = true)
        {
            _synchronized = synchronized;
            return this;
        }

        /// <summary>Build an immutable template.</summary>
        public ActionTemplate<TContext> Build()
            => new(_before, _step, _after, _onError, _synchronized);
    }
}
