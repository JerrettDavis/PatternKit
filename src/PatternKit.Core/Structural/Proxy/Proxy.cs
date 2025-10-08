using System.Runtime.CompilerServices;

namespace PatternKit.Structural.Proxy;

/// <summary>
/// Fluent, allocation-light proxy that controls access to a subject and intercepts method invocations.
/// Build once, then call <see cref="Execute"/> to invoke the subject through the proxy pipeline.
/// </summary>
/// <typeparam name="TIn">Input type passed to the subject.</typeparam>
/// <typeparam name="TOut">Output type produced by the subject.</typeparam>
/// <remarks>
/// <para>
/// <b>Mental model</b>: A <i>proxy</i> acts as a surrogate or placeholder for another object (the <i>subject</i>).
/// The proxy controls access to the subject and can add behavior before, after, or instead of delegating to it.
/// </para>
/// <para>
/// <b>Use cases</b>:
/// <list type="bullet">
///   <item><description><b>Virtual Proxy</b>: Lazy initialization - defer creating expensive objects until needed.</description></item>
///   <item><description><b>Protection Proxy</b>: Access control - validate permissions before allowing access.</description></item>
///   <item><description><b>Remote Proxy</b>: Local representative for remote object - handle network calls.</description></item>
///   <item><description><b>Caching Proxy</b>: Cache results to avoid redundant expensive operations.</description></item>
///   <item><description><b>Logging Proxy</b>: Track method invocations for debugging or auditing.</description></item>
///   <item><description><b>Smart Reference</b>: Reference counting, synchronization, or additional bookkeeping.</description></item>
///   <item><description><b>Mock/Test Double</b>: Substitute real objects with test-friendly implementations.</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Difference from Decorator</b>: While both wrap objects, Decorator <i>enhances</i> functionality while
/// maintaining the same interface contract. Proxy <i>controls access</i> to the subject and may provide a
/// completely different implementation or skip delegation entirely.
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/>, the proxy is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// // Virtual Proxy - Lazy initialization
/// var proxy = Proxy&lt;string, string&gt;.Create()
///     .VirtualProxy(() => ExpensiveResourceLoader())
///     .Build();
///
/// var result = proxy.Execute("request"); // Initializes on first call
///
/// // Protection Proxy - Access control
/// var proxy = Proxy&lt;User, bool&gt;.Create(user => DeleteUser(user))
///     .Intercept((user, next) => {
///         if (!user.IsAdmin) return false;
///         return next(user);
///     })
///     .Build();
/// </code>
/// </example>
public sealed class Proxy<TIn, TOut> where TIn : notnull
{
    /// <summary>
    /// Delegate representing the real subject operation.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <returns>The result from the subject.</returns>
    public delegate TOut Subject(TIn input);

    /// <summary>
    /// Delegate for intercepting calls and controlling access to the subject.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="next">Delegate to invoke the real subject (or next interceptor).</param>
    /// <returns>The result, potentially modified or short-circuited.</returns>
    /// <remarks>
    /// The interceptor has full control: it can modify input, skip calling <paramref name="next"/>,
    /// modify output, or add cross-cutting concerns like logging, caching, or access control.
    /// </remarks>
    public delegate TOut Interceptor(TIn input, Subject next);

    /// <summary>
    /// Delegate for validating access before allowing the subject to execute.
    /// </summary>
    /// <param name="input">The input value to validate.</param>
    /// <returns><see langword="true"/> if access is allowed; otherwise <see langword="false"/>.</returns>
    public delegate bool AccessValidator(TIn input);

    /// <summary>
    /// Delegate for lazy initialization of the real subject.
    /// </summary>
    /// <returns>The initialized subject delegate.</returns>
    public delegate Subject SubjectFactory();

    private enum InterceptorType : byte
    {
        Direct,           // Direct invocation
        Before,           // Action before delegation
        After,            // Action after delegation
        Intercept,        // Full interception
        Cache,            // Caching proxy
        Protection,       // Access control
        Virtual,          // Lazy initialization
        Logging           // Logging proxy
    }

    private readonly Subject? _subject;
    private readonly SubjectFactory? _subjectFactory;
    private readonly InterceptorType _type;
    private readonly object? _interceptor;
    private readonly bool _isVirtual;

    // For virtual proxy
    private Subject? _cachedSubject;
    private readonly object _lock = new();

    private Proxy(Subject? subject, SubjectFactory? subjectFactory, InterceptorType type, object? interceptor)
    {
        _subject = subject;
        _subjectFactory = subjectFactory;
        _type = type;
        _interceptor = interceptor;
        _isVirtual = type == InterceptorType.Virtual;
    }

    /// <summary>
    /// Executes the proxy, potentially intercepting or controlling access to the real subject.
    /// </summary>
    /// <param name="input">The input value (readonly via <c>in</c>).</param>
    /// <returns>The result after applying proxy logic.</returns>
    /// <remarks>
    /// <para>
    /// Depending on the proxy configuration:
    /// <list type="bullet">
    ///   <item><description>Direct proxies simply delegate to the subject.</description></item>
    ///   <item><description>Virtual proxies initialize the subject on first access.</description></item>
    ///   <item><description>Protection proxies validate access before delegating.</description></item>
    ///   <item><description>Interceptors can modify behavior at any point.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public TOut Execute(in TIn input)
    {
        if (_isVirtual)
            return ExecuteVirtual(in input);

        return _type switch
        {
            InterceptorType.Direct => _subject!(input),
            InterceptorType.Intercept => ((Interceptor)_interceptor!)(input, _subject!),
            InterceptorType.Before => ExecuteBefore(in input),
            InterceptorType.After => ExecuteAfter(in input),
            InterceptorType.Protection => ExecuteProtection(in input),
            InterceptorType.Cache => ExecuteCache(in input),
            InterceptorType.Logging => ExecuteLogging(in input),
            _ => throw new InvalidOperationException("Unknown interceptor type.")
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TOut ExecuteVirtual(in TIn input)
    {
        if (_cachedSubject is not null)
            return _cachedSubject(input);
        
        lock (_lock)
        {
            _cachedSubject ??= _subjectFactory!();
        }

        return _cachedSubject(input);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TOut ExecuteBefore(in TIn input)
    {
        var action = (Action<TIn>)_interceptor!;
        action(input);
        return _subject!(input);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TOut ExecuteAfter(in TIn input)
    {
        var result = _subject!(input);
        var action = (Action<TIn, TOut>)_interceptor!;
        action(input, result);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TOut ExecuteProtection(in TIn input)
    {
        var validator = (AccessValidator)_interceptor!;
        if (!validator(input))
            throw new UnauthorizedAccessException("Access denied by protection proxy.");
        return _subject!(input);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TOut ExecuteCache(in TIn input)
    {
        var cache = (Dictionary<TIn, TOut>)_interceptor!;
        if (cache.TryGetValue(input, out var cached))
            return cached;

        var result = _subject!(input);
        cache[input] = result;
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TOut ExecuteLogging(in TIn input)
    {
        var logger = (Action<string>)_interceptor!;
        logger($"Proxy invoked with input: {input}");
        var result = _subject!(input);
        logger($"Proxy returned output: {result}");
        return result;
    }

    /// <summary>
    /// Creates a new <see cref="Builder"/> for constructing a proxy.
    /// </summary>
    /// <param name="subject">The real subject to proxy (optional if using virtual proxy).</param>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    /// <example>
    /// <code language="csharp">
    /// var proxy = Proxy&lt;int, int&gt;.Create(static x => x * 2)
    ///     .Before(x => Console.WriteLine($"Input: {x}"))
    ///     .Build();
    /// </code>
    /// </example>
    public static Builder Create(Subject? subject = null) => new(subject);

    /// <summary>
    /// Fluent builder for <see cref="Proxy{TIn, TOut}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder supports various proxy patterns:
    /// <list type="bullet">
    ///   <item><description><see cref="VirtualProxy"/> - Lazy initialization</description></item>
    ///   <item><description><see cref="ProtectionProxy"/> - Access control</description></item>
    ///   <item><description><see cref="CachingProxy()"/> - Result caching</description></item>
    ///   <item><description><see cref="LoggingProxy"/> - Invocation logging</description></item>
    ///   <item><description><see cref="Intercept"/> - Custom interception</description></item>
    ///   <item><description><see cref="Before"/> / <see cref="After"/> - Simple pre/post actions</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Builders are mutable and not thread-safe. Each call to <see cref="Build"/> creates an immutable proxy instance.
    /// </para>
    /// </remarks>
    public sealed class Builder
    {
        private Subject? _subject;
        private SubjectFactory? _subjectFactory;
        private InterceptorType _type = InterceptorType.Direct;
        private object? _interceptor;

        internal Builder(Subject? subject)
        {
            _subject = subject;
        }

        /// <summary>
        /// Configures a virtual proxy that lazily initializes the subject on first access.
        /// </summary>
        /// <param name="factory">Factory function that creates the real subject.</param>
        /// <returns>This builder for chaining.</returns>
        /// <remarks>
        /// <para>
        /// <b>Virtual Proxy Pattern</b>: Delays expensive object creation until the object is actually needed.
        /// The factory is invoked only once, on the first call to <see cref="Execute"/>, and the result is cached.
        /// Thread-safe initialization is guaranteed.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var proxy = Proxy&lt;string, Database&gt;.Create()
        ///     .VirtualProxy(() => new Database("connection-string"))
        ///     .Build();
        /// // Database is not created until first Execute call
        /// </code>
        /// </example>
        public Builder VirtualProxy(SubjectFactory factory)
        {
            _subjectFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            _type = InterceptorType.Virtual;
            _subject = null;
            return this;
        }

        /// <summary>
        /// Configures a protection proxy that validates access before delegating to the subject.
        /// </summary>
        /// <param name="validator">Function that returns <see langword="true"/> if access is allowed.</param>
        /// <returns>This builder for chaining.</returns>
        /// <remarks>
        /// <para>
        /// <b>Protection Proxy Pattern</b>: Controls access to the subject based on validation logic.
        /// If the validator returns <see langword="false"/>, an <see cref="UnauthorizedAccessException"/> is thrown.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var proxy = Proxy&lt;User, bool&gt;.Create(user => DeleteUser(user))
        ///     .ProtectionProxy(user => user.IsAdmin)
        ///     .Build();
        /// </code>
        /// </example>
        public Builder ProtectionProxy(AccessValidator validator)
        {
            if (_subject is null) throw new InvalidOperationException("Protection proxy requires a subject.");
            _interceptor = validator ?? throw new ArgumentNullException(nameof(validator));
            _type = InterceptorType.Protection;
            return this;
        }

        /// <summary>
        /// Configures a caching proxy that memoizes results to avoid redundant subject invocations.
        /// </summary>
        /// <returns>This builder for chaining.</returns>
        /// <remarks>
        /// <para>
        /// <b>Caching Proxy Pattern</b>: Stores results in a dictionary keyed by input.
        /// Subsequent calls with the same input return the cached result without invoking the subject.
        /// </para>
        /// <para>
        /// <b>Note</b>: This requires <typeparamref>
        ///         <name>TIn</name>
        ///     </typeparamref>
        ///     to have proper equality semantics.
        /// For reference types, consider implementing <see cref="IEquatable{T}"/> or providing a custom comparer.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var proxy = Proxy&lt;int, int&gt;.Create(x => ExpensiveCalculation(x))
        ///     .CachingProxy()
        ///     .Build();
        /// var r1 = proxy.Execute(5); // Calls subject
        /// var r2 = proxy.Execute(5); // Returns cached result
        /// </code>
        /// </example>
        public Builder CachingProxy()
        {
            if (_subject is null) throw new InvalidOperationException("Caching proxy requires a subject.");
            _interceptor = new Dictionary<TIn, TOut>();
            _type = InterceptorType.Cache;
            return this;
        }

        /// <summary>
        /// Configures a caching proxy with a custom equality comparer.
        /// </summary>
        /// <param name="comparer">The equality comparer for cache keys.</param>
        /// <returns>This builder for chaining.</returns>
        /// <example>
        /// <code language="csharp">
        /// var proxy = Proxy&lt;string, int&gt;.Create(s => s.Length)
        ///     .CachingProxy(StringComparer.OrdinalIgnoreCase)
        ///     .Build();
        /// </code>
        /// </example>
        public Builder CachingProxy(IEqualityComparer<TIn> comparer)
        {
            if (_subject is null) throw new InvalidOperationException("Caching proxy requires a subject.");
            if (comparer is null) throw new ArgumentNullException(nameof(comparer));
            _interceptor = new Dictionary<TIn, TOut>(comparer);
            _type = InterceptorType.Cache;
            return this;
        }

        /// <summary>
        /// Configures a logging proxy that logs method invocations and results.
        /// </summary>
        /// <param name="logger">Action to invoke for logging (receives log messages).</param>
        /// <returns>This builder for chaining.</returns>
        /// <remarks>
        /// <para>
        /// <b>Logging Proxy Pattern</b>: Intercepts calls to log input and output for debugging or auditing.
        /// The logger is invoked before and after the subject execution.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var proxy = Proxy&lt;int, int&gt;.Create(x => x * 2)
        ///     .LoggingProxy(Console.WriteLine)
        ///     .Build();
        /// </code>
        /// </example>
        public Builder LoggingProxy(Action<string> logger)
        {
            if (_subject is null) throw new InvalidOperationException("Logging proxy requires a subject.");
            _interceptor = logger ?? throw new ArgumentNullException(nameof(logger));
            _type = InterceptorType.Logging;
            return this;
        }

        /// <summary>
        /// Adds a custom interceptor that has full control over subject invocation.
        /// </summary>
        /// <param name="interceptor">The interceptor delegate.</param>
        /// <returns>This builder for chaining.</returns>
        /// <remarks>
        /// <para>
        /// The interceptor receives the input and a delegate to the subject (or next layer).
        /// It can:
        /// <list type="bullet">
        ///   <item><description>Modify the input before calling the subject</description></item>
        ///   <item><description>Skip calling the subject entirely (short-circuit)</description></item>
        ///   <item><description>Modify the output before returning</description></item>
        ///   <item><description>Add cross-cutting concerns (logging, timing, error handling)</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var proxy = Proxy&lt;int, int&gt;.Create(x => x * 2)
        ///     .Intercept((in int x, next) => {
        ///         if (x &lt; 0) return 0; // Short-circuit negative inputs
        ///         return next(in x);
        ///     })
        ///     .Build();
        /// </code>
        /// </example>
        public Builder Intercept(Interceptor interceptor)
        {
            if (_subject is null) throw new InvalidOperationException("Intercept requires a subject.");
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
            _type = InterceptorType.Intercept;
            return this;
        }

        /// <summary>
        /// Adds an action to execute before delegating to the subject.
        /// </summary>
        /// <param name="action">Action to execute with the input.</param>
        /// <returns>This builder for chaining.</returns>
        /// <remarks>
        /// Useful for side effects like logging, validation, or notifications that don't affect the result.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var proxy = Proxy&lt;string, int&gt;.Create(s => s.Length)
        ///     .Before(s => Console.WriteLine($"Processing: {s}"))
        ///     .Build();
        /// </code>
        /// </example>
        public Builder Before(Action<TIn> action)
        {
            if (_subject is null) throw new InvalidOperationException("Before requires a subject.");
            _interceptor = action ?? throw new ArgumentNullException(nameof(action));
            _type = InterceptorType.Before;
            return this;
        }

        /// <summary>
        /// Adds an action to execute after the subject returns.
        /// </summary>
        /// <param name="action">Action to execute with input and output.</param>
        /// <returns>This builder for chaining.</returns>
        /// <remarks>
        /// Useful for side effects like logging results, notifications, or post-processing that doesn't modify the result.
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// var proxy = Proxy&lt;int, int&gt;.Create(x => x * 2)
        ///     .After((input, result) => Console.WriteLine($"{input} -> {result}"))
        ///     .Build();
        /// </code>
        /// </example>
        public Builder After(Action<TIn, TOut> action)
        {
            if (_subject is null) throw new InvalidOperationException("After requires a subject.");
            _interceptor = action ?? throw new ArgumentNullException(nameof(action));
            _type = InterceptorType.After;
            return this;
        }

        /// <summary>
        /// Builds an immutable <see cref="Proxy{TIn, TOut}"/> with the configured behavior.
        /// </summary>
        /// <returns>A new <see cref="Proxy{TIn, TOut}"/> instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the configuration is invalid.</exception>
        public Proxy<TIn, TOut> Build()
        {
            if (_type != InterceptorType.Virtual && _subject is null)
                throw new InvalidOperationException("Proxy requires a subject unless using VirtualProxy.");

            return new Proxy<TIn, TOut>(_subject, _subjectFactory, _type, _interceptor);
        }
    }
}
