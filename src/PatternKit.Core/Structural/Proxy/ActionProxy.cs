namespace PatternKit.Structural.Proxy;

/// <summary>
/// Fluent, allocation-light action proxy that controls access to a subject action (void-returning).
/// Build once, then call <see cref="Execute"/> to invoke the subject through the proxy.
/// </summary>
/// <typeparam name="TIn">Input type passed to the subject action.</typeparam>
/// <remarks>
/// <para>
/// This is the action (void-returning) counterpart to <see cref="Proxy{TIn,TOut}"/>.
/// Use when the subject performs side effects without returning a value.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var proxy = ActionProxy&lt;string&gt;.Create(msg => Console.WriteLine(msg))
///     .Before(msg => Console.WriteLine($"[{DateTime.Now}]"))
///     .Build();
/// proxy.Execute("Hello");
/// </code>
/// </example>
public sealed class ActionProxy<TIn> where TIn : notnull
{
    /// <summary>
    /// Delegate representing the real subject action.
    /// </summary>
    public delegate void Subject(TIn input);

    /// <summary>
    /// Delegate for intercepting calls.
    /// </summary>
    public delegate void Interceptor(TIn input, Subject next);

    /// <summary>
    /// Delegate for validating access.
    /// </summary>
    public delegate bool AccessValidator(TIn input);

    /// <summary>
    /// Delegate for lazy initialization.
    /// </summary>
    public delegate Subject SubjectFactory();

    private enum InterceptorType : byte { Direct, Before, After, Intercept, Protection, Virtual }

    private readonly Subject? _subject;
    private readonly SubjectFactory? _subjectFactory;
    private readonly InterceptorType _type;
    private readonly object? _interceptor;
    private readonly bool _isVirtual;

    private Subject? _cachedSubject;
    private readonly object _lock = new();

    private ActionProxy(Subject? subject, SubjectFactory? subjectFactory, InterceptorType type, object? interceptor)
    {
        _subject = subject;
        _subjectFactory = subjectFactory;
        _type = type;
        _interceptor = interceptor;
        _isVirtual = type == InterceptorType.Virtual;
    }

    /// <summary>
    /// Executes the proxy.
    /// </summary>
    public void Execute(in TIn input)
    {
        if (_isVirtual)
        {
            ExecuteVirtual(in input);
            return;
        }

        switch (_type)
        {
            case InterceptorType.Direct:
                _subject!(input);
                break;
            case InterceptorType.Intercept:
                ((Interceptor)_interceptor!)(input, _subject!);
                break;
            case InterceptorType.Before:
                ((Action<TIn>)_interceptor!)(input);
                _subject!(input);
                break;
            case InterceptorType.After:
                _subject!(input);
                ((Action<TIn>)_interceptor!)(input);
                break;
            case InterceptorType.Protection:
                if (!((AccessValidator)_interceptor!)(input))
                    throw new UnauthorizedAccessException("Access denied by protection proxy.");
                _subject!(input);
                break;
        }
    }

    private void ExecuteVirtual(in TIn input)
    {
        if (_cachedSubject is not null)
        {
            _cachedSubject(input);
            return;
        }

        lock (_lock)
        {
            _cachedSubject ??= _subjectFactory!();
        }

        _cachedSubject(input);
    }

    /// <summary>
    /// Creates a new builder.
    /// </summary>
    public static Builder Create(Subject? subject = null) => new(subject);

    /// <summary>
    /// Fluent builder for <see cref="ActionProxy{TIn}"/>.
    /// </summary>
    public sealed class Builder
    {
        private Subject? _subject;
        private SubjectFactory? _subjectFactory;
        private InterceptorType _type = InterceptorType.Direct;
        private object? _interceptor;

        internal Builder(Subject? subject) => _subject = subject;

        /// <summary>
        /// Configures a virtual proxy with lazy initialization.
        /// </summary>
        public Builder VirtualProxy(SubjectFactory factory)
        {
            _subjectFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            _type = InterceptorType.Virtual;
            _subject = null;
            return this;
        }

        /// <summary>
        /// Configures a protection proxy.
        /// </summary>
        public Builder ProtectionProxy(AccessValidator validator)
        {
            if (_subject is null) throw new InvalidOperationException("Protection proxy requires a subject.");
            _interceptor = validator ?? throw new ArgumentNullException(nameof(validator));
            _type = InterceptorType.Protection;
            return this;
        }

        /// <summary>
        /// Adds a custom interceptor.
        /// </summary>
        public Builder Intercept(Interceptor interceptor)
        {
            if (_subject is null) throw new InvalidOperationException("Intercept requires a subject.");
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
            _type = InterceptorType.Intercept;
            return this;
        }

        /// <summary>
        /// Adds an action to execute before the subject.
        /// </summary>
        public Builder Before(Action<TIn> action)
        {
            if (_subject is null) throw new InvalidOperationException("Before requires a subject.");
            _interceptor = action ?? throw new ArgumentNullException(nameof(action));
            _type = InterceptorType.Before;
            return this;
        }

        /// <summary>
        /// Adds an action to execute after the subject.
        /// </summary>
        public Builder After(Action<TIn> action)
        {
            if (_subject is null) throw new InvalidOperationException("After requires a subject.");
            _interceptor = action ?? throw new ArgumentNullException(nameof(action));
            _type = InterceptorType.After;
            return this;
        }

        /// <summary>
        /// Builds the proxy.
        /// </summary>
        public ActionProxy<TIn> Build()
        {
            if (_type != InterceptorType.Virtual && _subject is null)
                throw new InvalidOperationException("Proxy requires a subject unless using VirtualProxy.");
            return new ActionProxy<TIn>(_subject, _subjectFactory, _type, _interceptor);
        }
    }
}
