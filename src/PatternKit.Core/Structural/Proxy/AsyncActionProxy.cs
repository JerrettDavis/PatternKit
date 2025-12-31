namespace PatternKit.Structural.Proxy;

/// <summary>
/// Fluent, allocation-light async action proxy that controls access to an async action (void-returning async).
/// Build once, then call <see cref="ExecuteAsync"/> to invoke the subject through the proxy.
/// </summary>
/// <typeparam name="TIn">Input type passed to the subject action.</typeparam>
/// <remarks>
/// <para>
/// This is the async action counterpart combining features of <see cref="AsyncProxy{TIn,TOut}"/>
/// and <see cref="ActionProxy{TIn}"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var proxy = AsyncActionProxy&lt;string&gt;.Create(async (msg, ct) => await SaveAsync(msg, ct))
///     .Before(async (msg, ct) => await LogAsync($"Saving: {msg}", ct))
///     .Build();
/// await proxy.ExecuteAsync("Hello");
/// </code>
/// </example>
public sealed class AsyncActionProxy<TIn> where TIn : notnull
{
    /// <summary>
    /// Async subject action delegate.
    /// </summary>
    public delegate ValueTask Subject(TIn input, CancellationToken ct);

    /// <summary>
    /// Async interceptor delegate.
    /// </summary>
    public delegate ValueTask Interceptor(TIn input, CancellationToken ct, Subject next);

    /// <summary>
    /// Async access validator delegate.
    /// </summary>
    public delegate ValueTask<bool> AccessValidator(TIn input, CancellationToken ct);

    /// <summary>
    /// Async subject factory delegate.
    /// </summary>
    public delegate ValueTask<Subject> SubjectFactory(CancellationToken ct);

    /// <summary>
    /// Async before/after action delegate.
    /// </summary>
    public delegate ValueTask ActionHook(TIn input, CancellationToken ct);

    private enum InterceptorType : byte { Direct, Before, After, Intercept, Protection, Virtual }

    private readonly Subject? _subject;
    private readonly SubjectFactory? _subjectFactory;
    private readonly InterceptorType _type;
    private readonly object? _interceptor;
    private readonly bool _isVirtual;

    private Subject? _cachedSubject;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private AsyncActionProxy(Subject? subject, SubjectFactory? subjectFactory, InterceptorType type, object? interceptor)
    {
        _subject = subject;
        _subjectFactory = subjectFactory;
        _type = type;
        _interceptor = interceptor;
        _isVirtual = type == InterceptorType.Virtual;
    }

    /// <summary>
    /// Executes the proxy asynchronously.
    /// </summary>
    public async ValueTask ExecuteAsync(TIn input, CancellationToken ct = default)
    {
        if (_isVirtual)
        {
            await ExecuteVirtualAsync(input, ct).ConfigureAwait(false);
            return;
        }

        switch (_type)
        {
            case InterceptorType.Direct:
                await _subject!(input, ct).ConfigureAwait(false);
                break;
            case InterceptorType.Intercept:
                await ((Interceptor)_interceptor!)(input, ct, _subject!).ConfigureAwait(false);
                break;
            case InterceptorType.Before:
                await ((ActionHook)_interceptor!)(input, ct).ConfigureAwait(false);
                await _subject!(input, ct).ConfigureAwait(false);
                break;
            case InterceptorType.After:
                await _subject!(input, ct).ConfigureAwait(false);
                await ((ActionHook)_interceptor!)(input, ct).ConfigureAwait(false);
                break;
            case InterceptorType.Protection:
                if (!await ((AccessValidator)_interceptor!)(input, ct).ConfigureAwait(false))
                    throw new UnauthorizedAccessException("Access denied by protection proxy.");
                await _subject!(input, ct).ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask ExecuteVirtualAsync(TIn input, CancellationToken ct)
    {
        if (_cachedSubject is not null)
        {
            await _cachedSubject(input, ct).ConfigureAwait(false);
            return;
        }

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _cachedSubject ??= await _subjectFactory!(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        await _cachedSubject(input, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new builder.
    /// </summary>
    public static Builder Create(Subject? subject = null) => new(subject);

    /// <summary>
    /// Fluent builder for <see cref="AsyncActionProxy{TIn}"/>.
    /// </summary>
    public sealed class Builder
    {
        private Subject? _subject;
        private SubjectFactory? _subjectFactory;
        private InterceptorType _type = InterceptorType.Direct;
        private object? _interceptor;

        internal Builder(Subject? subject) => _subject = subject;

        /// <summary>
        /// Configures a virtual proxy with async lazy initialization.
        /// </summary>
        public Builder VirtualProxy(SubjectFactory factory)
        {
            _subjectFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            _type = InterceptorType.Virtual;
            _subject = null;
            return this;
        }

        /// <summary>
        /// Configures a virtual proxy with sync lazy initialization.
        /// </summary>
        public Builder VirtualProxy(Func<Subject> factory)
        {
            return VirtualProxy(Adapter);
            ValueTask<Subject> Adapter(CancellationToken _) => new(factory());
        }

        /// <summary>
        /// Configures an async protection proxy.
        /// </summary>
        public Builder ProtectionProxy(AccessValidator validator)
        {
            if (_subject is null) throw new InvalidOperationException("Protection proxy requires a subject.");
            _interceptor = validator ?? throw new ArgumentNullException(nameof(validator));
            _type = InterceptorType.Protection;
            return this;
        }

        /// <summary>
        /// Configures a sync protection proxy.
        /// </summary>
        public Builder ProtectionProxy(Func<TIn, bool> validator)
        {
            return ProtectionProxy(Adapter);
            ValueTask<bool> Adapter(TIn x, CancellationToken _) => new(validator(x));
        }

        /// <summary>
        /// Adds an async interceptor.
        /// </summary>
        public Builder Intercept(Interceptor interceptor)
        {
            if (_subject is null) throw new InvalidOperationException("Intercept requires a subject.");
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
            _type = InterceptorType.Intercept;
            return this;
        }

        /// <summary>
        /// Adds an async action to execute before the subject.
        /// </summary>
        public Builder Before(ActionHook action)
        {
            if (_subject is null) throw new InvalidOperationException("Before requires a subject.");
            _interceptor = action ?? throw new ArgumentNullException(nameof(action));
            _type = InterceptorType.Before;
            return this;
        }

        /// <summary>
        /// Adds a sync action to execute before the subject.
        /// </summary>
        public Builder Before(Action<TIn> action)
        {
            return Before(Adapter);
            ValueTask Adapter(TIn x, CancellationToken _)
            {
                action(x);
                return default;
            }
        }

        /// <summary>
        /// Adds an async action to execute after the subject.
        /// </summary>
        public Builder After(ActionHook action)
        {
            if (_subject is null) throw new InvalidOperationException("After requires a subject.");
            _interceptor = action ?? throw new ArgumentNullException(nameof(action));
            _type = InterceptorType.After;
            return this;
        }

        /// <summary>
        /// Adds a sync action to execute after the subject.
        /// </summary>
        public Builder After(Action<TIn> action)
        {
            return After(Adapter);
            ValueTask Adapter(TIn x, CancellationToken _)
            {
                action(x);
                return default;
            }
        }

        /// <summary>
        /// Builds the async action proxy.
        /// </summary>
        public AsyncActionProxy<TIn> Build()
        {
            if (_type != InterceptorType.Virtual && _subject is null)
                throw new InvalidOperationException("Proxy requires a subject unless using VirtualProxy.");
            return new AsyncActionProxy<TIn>(_subject, _subjectFactory, _type, _interceptor);
        }
    }
}
