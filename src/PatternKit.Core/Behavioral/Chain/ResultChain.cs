using System.Runtime.CompilerServices;

namespace PatternKit.Behavioral.Chain;

/// <summary>
/// A first-match-wins chain that can <b>produce a value</b>.
/// Each handler receives <c>(in TIn input, out TOut? result, Next next)</c> and may either:
/// <list type="bullet">
///   <item><description><b>Produce</b> a value (set result and return <see langword="true"/>) to short-circuit, or</description></item>
///   <item><description><b>Delegate</b> to <c>next(input, out result)</c> so later handlers can attempt to produce.</description></item>
/// </list>
/// </summary>
/// <typeparam name="TIn">The input type threaded through the chain.</typeparam>
/// <typeparam name="TOut">The potential output type produced by handlers.</typeparam>
/// <remarks>
/// <para>
/// Use <see cref="ResultChain{TIn, TOut}"/> when you want ordered rules that compute and return a value
/// (e.g., routing to an <c>HttpResponse</c>, choosing a price/promotion, parsing commands).
/// If you only need side effects, prefer <see cref="ActionChain{TCtx}"/>.
/// </para>
/// <para><b>Execution semantics</b></para>
/// <list type="number">
///   <item><description>Handlers are evaluated in registration order.</description></item>
///   <item><description>The first handler that returns <see langword="true"/> wins and the chain stops.</description></item>
///   <item><description>If no handler produces and no <see cref="Builder.Finally(TryHandler)"/> is configured, <see cref="Execute(in TIn, out TOut)"/> returns <see langword="false"/> and result is <see langword="default"/>.</description></item>
///   <item><description>If a <c>Finally</c> tail is set, it runs only if the chain reaches the tail (i.e., nobody produced earlier). It typically acts as a default/NotFound.</description></item>
/// </list>
/// <para>
/// <b>Performance:</b> The chain composes to a single delegate at <see cref="Builder.Build"/> time; the built chain is immutable and thread-safe.
/// </para>
/// </remarks>
/// <example>
/// <para>A tiny router that returns a value:</para>
/// <code language="csharp"><![CDATA[
/// using PatternKit.Behavioral.Chain;
///
/// public readonly record struct Request(string Method, string Path);
/// public readonly record struct Response(int Status, string Body);
///
/// var router = ResultChain<Request, Response>.Create()
///     .When(static (in r) => r.Method == "GET" && r.Path == "/health")
///         .Then(r => new Response(200, "OK"))
///     .When(static (in r) => r.Method == "GET" && r.Path.StartsWith("/users/"))
///         .Then(r => new Response(200, $"user:{r.Path[7..]}"))
///     // default / not found
///     .Finally(static (in _, out Response? res, _) => { res = new(404, "not found"); return true; })
///     .Build();
///
/// var ok1 = router.Execute(in new Request("GET", "/health"), out var res1);  // true, 200 OK
/// var ok2 = router.Execute(in new Request("GET", "/nope"),   out var res2);  // true, 404
/// ]]></code>
/// </example>
public sealed class ResultChain<TIn, TOut>
{
    /// <summary>
    /// Delegate representing the continuation of the chain.
    /// Implementations should return <see langword="true"/> when a downstream handler produced a result.
    /// </summary>
    /// <param name="input">The current input value.</param>
    /// <param name="result">The produced result when any downstream handler succeeds.</param>
    /// <returns><see langword="true"/> if a result was produced; otherwise <see langword="false"/>.</returns>
    public delegate bool Next(in TIn input, out TOut? result);

    /// <summary>
    /// Delegate representing a chain handler that may produce a value or delegate to <paramref name="next"/>.
    /// </summary>
    /// <param name="input">The current input value.</param>
    /// <param name="result">The result produced by this handler or by a downstream handler.</param>
    /// <param name="next">The continuation to invoke if this handler chooses not to (or cannot) produce.</param>
    /// <returns>
    /// <see langword="true"/> if this handler (or a downstream handler) produced a result; otherwise <see langword="false"/>.
    /// Returning <see langword="true"/> short-circuits the chain.
    /// </returns>
    public delegate bool TryHandler(in TIn input, out TOut? result, Next next);

    /// <summary>
    /// Delegate representing a predicate over the input.
    /// </summary>
    /// <param name="input">The current input value.</param>
    /// <returns><see langword="true"/> if the condition holds; otherwise <see langword="false"/>.</returns>
    public delegate bool Predicate(in TIn input);

    private readonly Next _entry;

    private ResultChain(Next entry) => _entry = entry;

    /// <summary>
    /// Executes the composed chain.
    /// </summary>
    /// <param name="input">The input value to evaluate.</param>
    /// <param name="result">When the method returns <see langword="true"/>, contains the produced result; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if any handler (or the tail) produced a result; otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// The terminal continuation returns <see langword="false"/> and leaves <paramref name="result"/> as <see langword="default"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Execute(in TIn input, out TOut? result) => _entry(in input, out result);

    /// <summary>
    /// Fluent builder for <see cref="ResultChain{TIn, TOut}"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Use(TryHandler)"/> to append raw handlers, <see cref="When(Predicate)"/> to start a conditional section,
    /// and <see cref="Finally(TryHandler)"/> to set a terminal fallback that runs only when no prior handler produced a result.
    /// Call <see cref="Build"/> to compose an immutable, thread-safe chain.
    /// </remarks>
    public sealed class Builder
    {
        private readonly List<TryHandler> _handlers = new(8);
        private TryHandler? _tail;

        /// <summary>
        /// Appends a handler to the chain.
        /// </summary>
        /// <param name="handler">The handler to add. It may produce a result or delegate to the provided <c>next</c>.</param>
        /// <returns>The same builder for chaining.</returns>
        public Builder Use(TryHandler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        /// <summary>
        /// Starts a conditional block guarded by <paramref name="predicate"/>.
        /// </summary>
        /// <param name="predicate">The condition to evaluate for the current input.</param>
        /// <returns>A <see cref="WhenBuilder"/> that configures behavior when the predicate is <see langword="true"/>.</returns>
        /// <remarks>
        /// When the predicate is <see langword="false"/>, the generated handler automatically delegates to the next step.
        /// </remarks>
        public WhenBuilder When(Predicate predicate) => new(this, predicate);

        /// <summary>
        /// Sets a terminal fallback (e.g., default/NotFound) that runs only if the chain reaches the tail.
        /// </summary>
        /// <param name="tail">The tail handler to execute at the end of the chain.</param>
        /// <returns>The same builder for chaining.</returns>
        /// <remarks>
        /// If any earlier handler produced a result (returned <see langword="true"/>), the tail is not invoked.
        /// </remarks>
        public Builder Finally(TryHandler tail)
        {
            _tail = tail;
            return this;
        }

        /// <summary>
        /// Composes all registered handlers (and optional tail) into a single immutable chain.
        /// </summary>
        /// <returns>An executable <see cref="ResultChain{TIn, TOut}"/> instance.</returns>
        /// <remarks>
        /// Handlers are folded from last to first so that runtime execution preserves registration order.
        /// The terminal continuation returns <see langword="false"/> and <see langword="default"/> result.
        /// </remarks>
        public ResultChain<TIn, TOut> Build()
        {
            // terminal: no result
            Next next = static (in _, out r) =>
            {
                r = default;
                return false;
            };

            if (_tail is not null)
            {
                var t = _tail;
                var prev = next;
                next = (in x, out r) => t(in x, out r, prev);
            }

            for (var i = _handlers.Count - 1; i >= 0; i--)
            {
                var h = _handlers[i];
                var prev = next;
                next = (in x, out r) => h(in x, out r, prev);
            }

            return new ResultChain<TIn, TOut>(next);
        }

        /// <summary>
        /// Builder for a conditional branch created via <see cref="Builder.When(Predicate)"/>.
        /// </summary>
        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly Predicate _pred;

            /// <summary>
            /// Initializes a new <see cref="WhenBuilder"/>.
            /// </summary>
            /// <param name="owner">The parent builder.</param>
            /// <param name="pred">The predicate guarding the conditional handler(s).</param>
            internal WhenBuilder(Builder owner, Predicate pred) => (_owner, _pred) = (owner, pred);

            /// <summary>
            /// Adds a conditional handler that may <b>produce</b> a value or <b>delegate</b> to the next step.
            /// When the predicate is <see langword="false"/>, the chain automatically delegates.
            /// </summary>
            /// <param name="handler">The conditional handler.</param>
            /// <returns>The parent builder for chaining.</returns>
            public Builder Do(TryHandler handler)
            {
                var pred = _pred;
                _owner.Use((in x, out r, next)
                    => pred(in x)
                        ? handler(in x, out r, next)
                        : next(in x, out r));
                return _owner;
            }

            /// <summary>
            /// Adds a conditional producer that returns a value and stops the chain when the predicate is <see langword="true"/>.
            /// </summary>
            /// <param name="produce">A function that maps the input to the produced result.</param>
            /// <returns>The parent builder for chaining.</returns>
            /// <remarks>
            /// When the predicate is <see langword="false"/>, the chain automatically delegates to the next step.
            /// </remarks>
            public Builder Then(Func<TIn, TOut> produce)
            {
                var pred = _pred;
                _owner.Use((in x, out r, next) =>
                {
                    if (!pred(in x))
                        return next(in x, out r);

                    r = produce(x);
                    return true;
                });
                return _owner;
            }
        }
    }

    /// <summary>
    /// Starts a new <see cref="Builder"/> for configuring a <see cref="ResultChain{TIn, TOut}"/>.
    /// </summary>
    /// <returns>A fresh builder instance.</returns>
    /// <example>
    /// <code language="csharp"><![CDATA[
    /// var chain = ResultChain<int, string>.Create()
    ///     .When(static (in i) => i > 0).Then(i => $"+{i}")
    ///     .Finally(static (in _, out string? r, _) => { r = "default"; return true; })
    ///     .Build();
    /// ]]></code>
    /// </example>
    public static Builder Create() => new();
}