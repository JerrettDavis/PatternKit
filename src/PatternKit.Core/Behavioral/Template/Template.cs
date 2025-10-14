using System;

namespace PatternKit.Behavioral.Template;

/// <summary>
/// Fluent, allocation-light Template that defines an algorithm skeleton with optional hooks.
/// Build once, then call Execute or TryExecute.
/// </summary>
/// <typeparam name="TContext">Input context type.</typeparam>
/// <typeparam name="TResult">Result type.</typeparam>
public sealed class Template<TContext, TResult>
{
    public delegate void BeforeHook(TContext context);
    public delegate TResult StepHook(TContext context);
    public delegate void AfterHook(TContext context, TResult result);
    public delegate void ErrorHook(TContext context, string error);

    private readonly BeforeHook? _before;
    private readonly StepHook _step;
    private readonly AfterHook? _after;
    private readonly ErrorHook? _onError;
    private readonly bool _synchronized;
    private readonly object _sync = new();

    private Template(BeforeHook? before, StepHook step, AfterHook? after, ErrorHook? onError, bool synchronized)
    {
        _before = before;
        _step = step;
        _after = after;
        _onError = onError;
        _synchronized = synchronized;
    }

    /// <summary>
    /// Execute the template: before → step → after. Throws on errors.
    /// </summary>
    public TResult Execute(TContext context)
    {
        if (_synchronized)
        {
            lock (_sync)
            {
                _before?.Invoke(context);
                var result = _step(context);
                _after?.Invoke(context, result);
                return result;
            }
        }

        _before?.Invoke(context);
        var res = _step(context);
        _after?.Invoke(context, res);
        return res;
    }

    /// <summary>
    /// Like <see cref="Execute"/>, but returns false with an error message instead of throwing.
    /// </summary>
    public bool TryExecute(TContext context, out TResult result, out string? error)
    {
        try
        {
            if (_synchronized)
            {
                lock (_sync)
                {
                    _before?.Invoke(context);
                    result = _step(context);
                    _after?.Invoke(context, result);
                    error = null;
                    return true;
                }
            }

            _before?.Invoke(context);
            result = _step(context);
            _after?.Invoke(context, result);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            _onError?.Invoke(context, ex.Message);
            result = default!;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Create a builder for this template.</summary>
    public static Builder Create(StepHook step) => new(step);

    /// <summary>Fluent builder for <see cref="Template{TContext,TResult}"/>.</summary>
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
        public Template<TContext, TResult> Build()
            => new(_before, _step, _after, _onError, _synchronized);
    }
}
