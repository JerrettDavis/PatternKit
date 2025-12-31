namespace PatternKit.Structural.Proxy;

/// <summary>
/// Fluent, allocation-light async proxy that controls access to an async subject.
/// Build once, then call <see cref="ExecuteAsync"/> to invoke the subject through the proxy.
/// </summary>
/// <typeparam name="TIn">Input type passed to the subject.</typeparam>
/// <typeparam name="TOut">Output type produced by the subject.</typeparam>
/// <remarks>
/// <para>
/// This is the async counterpart to <see cref="Proxy{TIn,TOut}"/>.
/// Use when the subject performs async operations.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var proxy = AsyncProxy&lt;string, int&gt;.Create(async (s, ct) => await ComputeAsync(s, ct))
///     .Before(async (s, ct) => await LogAsync(s, ct))
///     .Build();
/// var result = await proxy.ExecuteAsync("input");
/// </code>
/// </example>
public sealed class AsyncProxy<TIn, TOut> where TIn : notnull
{
    /// <summary>
    /// Async subject delegate.
    /// </summary>
    public delegate ValueTask<TOut> Subject(TIn input, CancellationToken ct);

    /// <summary>
    /// Async interceptor delegate.
    /// </summary>
    public delegate ValueTask<TOut> Interceptor(TIn input, CancellationToken ct, Subject next);

    /// <summary>
    /// Async access validator delegate.
    /// </summary>
    public delegate ValueTask<bool> AccessValidator(TIn input, CancellationToken ct);

    /// <summary>
    /// Async subject factory delegate.
    /// </summary>
    public delegate ValueTask<Subject> SubjectFactory(CancellationToken ct);

    /// <summary>
    /// Async before action delegate.
    /// </summary>
    public delegate ValueTask BeforeAction(TIn input, CancellationToken ct);

    /// <summary>
    /// Async after action delegate.
    /// </summary>
    public delegate ValueTask AfterAction(TIn input, TOut result, CancellationToken ct);

    private enum InterceptorType : byte { Direct, Before, After, Intercept, Protection, Virtual }

    private readonly Subject? _subject;
    private readonly SubjectFactory? _subjectFactory;
    private readonly InterceptorType _type;
    private readonly object? _interceptor;
    private readonly bool _isVirtual;

    private Subject? _cachedSubject;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private AsyncProxy(Subject? subject, SubjectFactory? subjectFactory, InterceptorType type, object? interceptor)
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
    public async ValueTask<TOut> ExecuteAsync(TIn input, CancellationToken ct = default)
    {
        if (_isVirtual)
            return await ExecuteVirtualAsync(input, ct).ConfigureAwait(false);

        return _type switch
        {
            InterceptorType.Direct => await _subject!(input, ct).ConfigureAwait(false),
            InterceptorType.Intercept => await ((Interceptor)_interceptor!)(input, ct, _subject!).ConfigureAwait(false),
            InterceptorType.Before => await ExecuteBeforeAsync(input, ct).ConfigureAwait(false),
            InterceptorType.After => await ExecuteAfterAsync(input, ct).ConfigureAwait(false),
            InterceptorType.Protection => await ExecuteProtectionAsync(input, ct).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unknown interceptor type.")
        };
    }

    private async ValueTask<TOut> ExecuteVirtualAsync(TIn input, CancellationToken ct)
    {
        if (_cachedSubject is not null)
            return await _cachedSubject(input, ct).ConfigureAwait(false);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _cachedSubject ??= await _subjectFactory!(ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        return await _cachedSubject(input, ct).ConfigureAwait(false);
    }

    private async ValueTask<TOut> ExecuteBeforeAsync(TIn input, CancellationToken ct)
    {
        await ((BeforeAction)_interceptor!)(input, ct).ConfigureAwait(false);
        return await _subject!(input, ct).ConfigureAwait(false);
    }

    private async ValueTask<TOut> ExecuteAfterAsync(TIn input, CancellationToken ct)
    {
        var result = await _subject!(input, ct).ConfigureAwait(false);
        await ((AfterAction)_interceptor!)(input, result, ct).ConfigureAwait(false);
        return result;
    }

    private async ValueTask<TOut> ExecuteProtectionAsync(TIn input, CancellationToken ct)
    {
        if (!await ((AccessValidator)_interceptor!)(input, ct).ConfigureAwait(false))
            throw new UnauthorizedAccessException("Access denied by protection proxy.");
        return await _subject!(input, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a new builder.
    /// </summary>
    public static Builder Create(Subject? subject = null) => new(subject);

    /// <summary>
    /// Fluent builder for <see cref="AsyncProxy{TIn, TOut}"/>.
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
        public Builder Before(BeforeAction action)
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
        public Builder After(AfterAction action)
        {
            if (_subject is null) throw new InvalidOperationException("After requires a subject.");
            _interceptor = action ?? throw new ArgumentNullException(nameof(action));
            _type = InterceptorType.After;
            return this;
        }

        /// <summary>
        /// Adds a sync action to execute after the subject.
        /// </summary>
        public Builder After(Action<TIn, TOut> action)
        {
            return After(Adapter);
            ValueTask Adapter(TIn x, TOut r, CancellationToken _)
            {
                action(x, r);
                return default;
            }
        }

        /// <summary>
        /// Builds the async proxy.
        /// </summary>
        public AsyncProxy<TIn, TOut> Build()
        {
            if (_type != InterceptorType.Virtual && _subject is null)
                throw new InvalidOperationException("Proxy requires a subject unless using VirtualProxy.");
            return new AsyncProxy<TIn, TOut>(_subject, _subjectFactory, _type, _interceptor);
        }
    }
}
