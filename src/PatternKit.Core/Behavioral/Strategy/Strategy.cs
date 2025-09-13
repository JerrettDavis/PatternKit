using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Strategy;

/// <summary>
/// Represents a "first-match-wins" strategy pipeline built from predicate/handler pairs.
/// </summary>
/// <typeparam name="TIn">The input type accepted by each predicate and handler.</typeparam>
/// <typeparam name="TOut">The output type produced by a matching handler.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Strategy{TIn, TOut}"/> is a simpler, non-<c>out</c>-based variant of
/// <see cref="TryStrategy{TIn,TOut}"/>. It uses <see cref="Predicate"/> delegates to
/// decide whether a handler applies, and <see cref="Handler"/> delegates to produce the
/// result. The first predicate that returns <see langword="true"/> determines which
/// handler is executed.
/// </para>
/// <para>
/// If no predicates match and no default handler is configured, an
/// <see cref="InvalidOperationException"/> is thrown via <see cref="Throw.NoStrategyMatched{T}()"/>.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var strategy = Strategy&lt;int, string&gt;.Create()
///     .When(static i =&gt; i &gt; 0).Then(static i =&gt; "positive")
///     .When(static i =&gt; i &lt; 0).Then(static i =&gt; "negative")
///     .Default(static _ =&gt; "zero")
///     .Build();
///
/// var label = strategy.Execute(5); // "positive"
/// </code>
/// </example>
public sealed class Strategy<TIn, TOut>
{
    /// <summary>
    /// Delegate representing a predicate used to test the input value.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <returns><see langword="true"/> if this predicate matches; otherwise <see langword="false"/>.</returns>
    public delegate bool Predicate(in TIn input);

    /// <summary>
    /// Delegate representing a handler that produces an output value when its corresponding predicate matches.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <returns>The computed result.</returns>
    public delegate TOut Handler(in TIn input);

    private readonly Predicate[] _predicates;
    private readonly Handler[] _handlers;
    private readonly bool _hasDefault;
    private readonly Handler _default;

    private static Handler DefaultResult => static (in TIn _) => default!;

    private Strategy(Predicate[] predicates, Handler[] handlers, bool hasDefault, Handler @default)
        => (_predicates, _handlers, _hasDefault, _default) = (predicates, handlers, hasDefault, @default);

    /// <summary>
    /// Executes the strategy pipeline against the given <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <returns>
    /// The result from the first matching handler, or the <see cref="Builder.Default(Handler)"/> result if provided.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no predicates match and no default handler is configured.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Iterates the registered predicates in the order they were added. The first predicate that returns
    /// <see langword="true"/> causes its corresponding handler to execute and the method returns immediately.
    /// </para>
    /// <para>
    /// If no predicate matches:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>If a default handler is set, its result is returned.</description></item>
    ///   <item><description>Otherwise, <see cref="Throw.NoStrategyMatched{T}()"/> is called, which always throws.</description></item>
    /// </list>
    /// </remarks>
    public TOut Execute(in TIn input)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (predicates[i](in input))
                return _handlers[i](in input);

        return _hasDefault
            ? _default(in input)
            : Throw.NoStrategyMatched<TOut>();
    }

    /// <summary>
    /// Provides a fluent API for constructing a <see cref="Strategy{TIn, TOut}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder collects <see cref="Predicate"/> / <see cref="Handler"/> pairs and
    /// an optional default handler. After calling <see cref="Build"/>, an immutable
    /// <see cref="Strategy{TIn, TOut}"/> instance is returned.
    /// </para>
    /// </remarks>
    public sealed class Builder
    {
        private readonly BranchBuilder<Predicate, Handler> _core = BranchBuilder<Predicate, Handler>.Create();

        public WhenBuilder When(Predicate predicate) => new(this, predicate);

        public Builder Default(Handler handler)
        {
            _core.Default(handler);
            return this;
        }

        public Strategy<TIn, TOut> Build()
            => _core.Build(
                fallbackDefault: DefaultResult,
                projector: static (predicates, handlers, hasDefault, @default)
                    => new Strategy<TIn, TOut>(predicates, handlers, hasDefault, @default));

        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly Predicate _pred;
            internal WhenBuilder(Builder owner, Predicate pred) => (_owner, _pred) = (owner, pred);

            public Builder Then(Handler handler)
            {
                _owner._core.Add(_pred, handler);
                return _owner;
            }
        }
    }

    /// <summary>
    /// Creates a new <see cref="Builder"/> for constructing a <see cref="Strategy{TIn, TOut}"/>.
    /// </summary>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    /// <example>
    /// <code language="csharp">
    /// var s = Strategy&lt;string, string&gt;.Create()
    ///     .When(s =&gt; string.IsNullOrEmpty(s)).Then(_ =&gt; "empty")
    ///     .Default(_ =&gt; "other")
    ///     .Build();
    ///
    /// var result = s.Execute(""); // "empty"
    /// </code>
    /// </example>
    public static Builder Create() => new();
}