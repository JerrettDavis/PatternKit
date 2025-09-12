namespace PatternKit.Behavioral.Strategy;

/// <summary>
/// Represents a chain of <see cref="TryHandler"/> delegates that are executed in order
/// until one succeeds (returns <see langword="true"/>).
/// </summary>
/// <typeparam name="TIn">The input type accepted by each handler.</typeparam>
/// <typeparam name="TOut">The output type produced by a successful handler.</typeparam>
/// <remarks>
/// <para>
/// <see cref="TryStrategy{TIn, TOut}"/> is designed for allocation-free, hot-path usage
/// where you want a first-match-wins execution model without exceptions.
/// </para>
/// <para>
/// The strategy is immutable once built. Typical construction is via the
/// <see cref="Builder"/> fluent DSL:
/// </para>
/// <code language="csharp">
/// var strategy = TryStrategy&lt;object, string&gt;.Create()
///     .Always(FirstHandler)
///     .Or.When(() =&gt; condition).Add(ConditionalHandler)
///     .Finally(FallbackHandler)
///     .Build();
///
/// if (strategy.Execute(input, out var result))
///     Console.WriteLine($"Matched: {result}");
/// else
///     Console.WriteLine("No handler matched.");
/// </code>
/// </remarks>
public sealed class TryStrategy<TIn, TOut>
{
    /// <summary>
    /// Delegate type representing a handler that attempts to produce a result.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="result">
    /// When the method returns <see langword="true"/>, contains the output result.
    /// Otherwise set to <see langword="default"/>.
    /// </param>
    /// <returns><see langword="true"/> if this handler successfully produced a result.</returns>
    public delegate bool TryHandler(in TIn input, out TOut? result);

    private readonly TryHandler[] _handlers;

    private TryStrategy(TryHandler[] handlers) => _handlers = handlers;

    /// <summary>
    /// Executes the strategy by invoking each handler in order until one succeeds.
    /// </summary>
    /// <param name="input">The input value passed to each handler.</param>
    /// <param name="result">
    /// The first successfully produced result. Undefined when the method returns <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if any handler produced a result; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Stops execution as soon as a handler returns <see langword="true"/>. Remaining handlers
    /// are not evaluated.
    /// </para>
    /// </remarks>
    public bool Execute(in TIn input, out TOut? result)
    {
        foreach (var h in _handlers)
            if (h(in input, out result))
                return true;

        result = default;
        return false;
    }

    /// <summary>
    /// Provides a fluent API to construct a <see cref="TryStrategy{TIn, TOut}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Handlers are evaluated in the order they are added. Use <see cref="When(Func{bool})"/>
    /// to add handlers conditionally, and <see cref="Finally(TryHandler)"/> to append
    /// fallback handlers at the end of the chain.
    /// </para>
    /// <para>
    /// The builder is mutable until <see cref="Build"/> is called, after which an
    /// immutable <see cref="TryStrategy{TIn, TOut}"/> instance is returned.
    /// </para>
    /// </remarks>
    public sealed class Builder
    {
        private readonly List<TryHandler> _handlers = new(8);

        /// <summary>
        /// Adds a handler that is always included in the pipeline.
        /// </summary>
        /// <param name="handler">The handler delegate to add.</param>
        /// <returns>The current <see cref="Builder"/> instance for chaining.</returns>
        /// <remarks>Handlers are evaluated in the order they are added.</remarks>
        public Builder Always(TryHandler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        /// <summary>
        /// Starts a conditional block where handlers are added only if the <paramref name="condition"/> evaluates to <see langword="true"/>.
        /// </summary>
        /// <param name="condition">
        /// A function evaluated once during build time to determine whether
        /// handlers inside the block should be added.
        /// </param>
        /// <returns>A <see cref="WhenBuilder"/> for adding conditional handlers.</returns>
        public WhenBuilder When(Func<bool> condition) => new(this, condition());

        /// <summary>
        /// Adds a handler to the end of the pipeline, typically as a fallback.
        /// </summary>
        /// <param name="handler">The handler to append.</param>
        /// <returns>The current <see cref="Builder"/> instance for chaining.</returns>
        public Builder Finally(TryHandler handler)
        {
            _handlers.Add(handler);
            return this;
        }

        /// <summary>
        /// Provides syntactic sugar for chaining fluent calls.
        /// </summary>
        public Builder Or => this;

        /// <summary>
        /// Builds the immutable <see cref="TryStrategy{TIn, TOut}"/> from the collected handlers.
        /// </summary>
        /// <returns>A compiled <see cref="TryStrategy{TIn, TOut}"/> ready for execution.</returns>
        public TryStrategy<TIn, TOut> Build() => new(_handlers.ToArray());

        /// <summary>
        /// Represents a conditional builder context for adding handlers when a
        /// <see cref="When(Func{bool})"/> condition is <see langword="true"/>.
        /// </summary>
        public readonly struct WhenBuilder
        {
            private readonly Builder _owner;
            private readonly bool _cond;

            internal WhenBuilder(Builder owner, bool cond) => (_owner, _cond) = (owner, cond);

            /// <summary>
            /// Adds a handler to the conditional block.
            /// </summary>
            /// <param name="handler">The handler to add.</param>
            /// <returns>The current <see cref="WhenBuilder"/> instance for further chaining.</returns>
            /// <remarks>Ignored if the condition supplied to <see cref="Builder.When(Func{bool})"/> was <see langword="false"/>.</remarks>
            public WhenBuilder Add(TryHandler handler)
            {
                if (_cond) _owner._handlers.Add(handler);
                return this;
            }

            /// <summary>
            /// Adds an additional handler in the same conditional block.
            /// </summary>
            /// <param name="handler">The handler to add.</param>
            /// <returns>The current <see cref="WhenBuilder"/> instance for further chaining.</returns>
            public WhenBuilder And(TryHandler handler) => Add(handler);

            /// <summary>
            /// Returns control back to the parent <see cref="Builder"/> to continue chaining.
            /// </summary>
            public Builder End => _owner;

            /// <summary>
            /// Alias for <see cref="End"/> for more natural chaining.
            /// </summary>
            public Builder Or => _owner;

            /// <summary>
            /// Adds a final (fallback) handler and returns the parent <see cref="Builder"/>.
            /// </summary>
            /// <param name="handler">The handler to add.</param>
            /// <returns>The parent <see cref="Builder"/>.</returns>
            public Builder Finally(TryHandler handler) => _owner.Finally(handler);
        }
    }

    /// <summary>
    /// Creates a new <see cref="Builder"/> for constructing a <see cref="TryStrategy{TIn, TOut}"/>.
    /// </summary>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    /// <example>
    /// <code language="csharp">
    /// var strategy = TryStrategy&lt;string, int&gt;.Create()
    ///     .Always((in string s, out int r) =&gt; int.TryParse(s, out r))
    ///     .Finally((in string _, out int r) =&gt; { r = 0; return true; })
    ///     .Build();
    /// </code>
    /// </example>
    public static Builder Create() => new();
}