using PatternKit.Common;
using PatternKit.Creational.Builder;

namespace PatternKit.Behavioral.Strategy;

/// <summary>
/// Represents a "first-match-wins" strategy pipeline built from predicate/action pairs,
/// where actions perform side effects and do not return a value.
/// </summary>
/// <typeparam name="TIn">The input type accepted by each predicate and action.</typeparam>
/// <remarks>
/// <para>
/// <see cref="ActionStrategy{TIn}"/> is the action-only counterpart to
/// <see cref="Strategy{TIn,TOut}"/> and <see cref="TryStrategy{TIn,TOut}"/>.
/// It uses <see cref="Predicate"/> delegates to decide whether an <see cref="ActionHandler"/>
/// applies. The first predicate that returns <see langword="true"/> determines which action is executed.
/// </para>
/// <para>
/// If no predicates match:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Execute(in TIn)"/> calls the configured default action if present; otherwise throws via <see cref="Throw.NoStrategyMatched()"/>.</description></item>
///   <item><description><see cref="TryExecute(in TIn)"/> returns <see langword="true"/> if any action ran (including default); otherwise <see langword="false"/>.</description></item>
/// </list>
/// <para><b>Thread-safety:</b> The built strategy is immutable and thread-safe. The <see cref="Builder"/> is not thread-safe.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var log = new List&lt;string&gt;();
///
/// void Log(string msg) => log.Add(msg);
///
/// var s = ActionStrategy&lt;int&gt;.Create()
///     .When(static i =&gt; i &gt; 0).Then(static i =&gt; Log($"+{i}"))
///     .When(static i =&gt; i &lt; 0).Then(static i =&gt; Log($"-{i}"))
///     .Default(static _ =&gt; Log("zero"))
///     .Build();
///
/// s.Execute(5);   // logs "+5"
/// s.Execute(-3);  // logs "-3"
/// s.Execute(0);   // logs "zero"
/// </code>
/// </example>
public sealed class ActionStrategy<TIn>
{
    /// <summary>
    /// Delegate representing a predicate used to test the input value.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <returns><see langword="true"/> if this predicate matches; otherwise <see langword="false"/>.</returns>
    public delegate bool Predicate(in TIn input);

    /// <summary>
    /// Delegate representing an action that runs when its corresponding predicate matches.
    /// </summary>
    /// <param name="input">The input value.</param>
    public delegate void ActionHandler(in TIn input);

    private readonly Predicate[] _predicates;
    private readonly ActionHandler[] _actions;
    private readonly bool _hasDefault;
    private readonly ActionHandler _default;

    private static ActionHandler Noop => static (in _) => { };


    private ActionStrategy(Predicate[] predicates, ActionHandler[] actions, bool hasDefault, ActionHandler @default)
        => (_predicates, _actions, _hasDefault, _default) = (predicates, actions, hasDefault, @default);

    /// <summary>
    /// Executes the first matching action for the given <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <remarks>
    /// Iterates predicates in registration order; runs the corresponding action for the first match and returns.
    /// If no predicate matches and a default was configured via <see cref="Builder.Default(ActionHandler)"/>,
    /// the default action runs. Otherwise, throws via <see cref="Throw.NoStrategyMatched()"/>.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no predicates match and no default action is configured.
    /// </exception>
    public void Execute(in TIn input)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (predicates[i](in input))
            {
                _actions[i](in input);
                return;
            }

        if (_hasDefault)
        {
            _default(in input);
            return;
        }

        Throw.NoStrategyMatched();
    }

    /// <summary>
    /// Attempts to execute the first matching action for the given <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <returns>
    /// <see langword="true"/> if an action (or default) executed; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="Execute(in TIn)"/>, this method never throws due to no matches.
    /// </remarks>
    public bool TryExecute(in TIn input)
    {
        var predicates = _predicates;
        for (var i = 0; i < predicates.Length; i++)
            if (predicates[i](in input))
            {
                _actions[i](in input);
                return true;
            }

        if (!_hasDefault)
            return false;
        
        _default(in input);
        return true;

    }

    /// <summary>
    /// Provides a fluent API for constructing an <see cref="ActionStrategy{TIn}"/>.
    /// </summary>
    /// <remarks>
    /// Use <see cref="When(Predicate)"/> to start a branch and <see cref="WhenBuilder.Then(ActionHandler)"/> to attach an action.
    /// Optionally add a <see cref="Default(ActionHandler)"/> that runs when no predicates match.
    /// Call <see cref="Build"/> to produce an immutable, thread-safe strategy.
    /// </remarks>
    public sealed class Builder
    {
        private readonly BranchBuilder<Predicate, ActionHandler> _core = BranchBuilder<Predicate, ActionHandler>.Create();

        public WhenBuilder When(Predicate predicate) => new(this, predicate);

        public Builder Default(ActionHandler action)
        {
            _core.Default(action);
            return this;
        }

        public ActionStrategy<TIn> Build()
            => _core.Build(
                fallbackDefault: Noop,
                projector: static (predicates, handlers, hasDefault, @default)
                    => new ActionStrategy<TIn>(predicates, handlers, hasDefault, @default));

        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly Predicate _pred;
            internal WhenBuilder(Builder owner, Predicate pred) => (_owner, _pred) = (owner, pred);

            public Builder Then(ActionHandler action)
            {
                _owner._core.Add(_pred, action);
                return _owner;
            }
        }
    }


    /// <summary>
    /// Creates a new <see cref="Builder"/> for constructing an <see cref="ActionStrategy{TIn}"/>.
    /// </summary>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    /// <example>
    /// <code language="csharp">
    /// var s = ActionStrategy&lt;string&gt;.Create()
    ///     .When(static s =&gt; string.IsNullOrEmpty(s)).Then(static _ =&gt; Console.WriteLine("empty"))
    ///     .Default(static _ =&gt; Console.WriteLine("other"))
    ///     .Build();
    ///
    /// s.Execute("");  // prints "empty"
    /// s.TryExecute("x"); // prints "other", returns true
    /// </code>
    /// </example>
    public static Builder Create() => new();
}