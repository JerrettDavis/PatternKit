namespace PatternKit.Behavioral.Interpreter;

/// <summary>
/// Action Interpreter pattern implementation for executing expressions with side effects.
/// Provides a fluent API for defining terminal and non-terminal expressions that perform actions.
/// </summary>
/// <remarks>
/// <para>
/// This is the action (void-returning) counterpart to <see cref="Interpreter{TContext,TResult}"/>.
/// Use when expressions trigger side effects rather than computing values (logging, notifications, etc.).
/// </para>
/// <para>
/// <b>Thread-safety:</b> Interpreters built via <see cref="ActionInterpreterBuilder{TContext}.Build"/>
/// are immutable and thread-safe. The builder is not thread-safe.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Action interpreter for executing UI commands
/// var interpreter = ActionInterpreter.Create&lt;UIContext&gt;()
///     .Terminal("show", (elementId, ctx) => ctx.ShowElement(elementId))
///     .Terminal("hide", (elementId, ctx) => ctx.HideElement(elementId))
///     .NonTerminal("sequence", (ctx, children) => { foreach (var c in children) c(); })
///     .Build();
///
/// var expr = new NonTerminalExpression("sequence",
///     new TerminalExpression("show", "dialog"),
///     new TerminalExpression("hide", "spinner"));
///
/// interpreter.Interpret(expr, uiContext);
/// </code>
/// </example>
public static class ActionInterpreter
{
    /// <summary>Creates a new fluent builder for an action interpreter.</summary>
    /// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
    public static ActionInterpreterBuilder<TContext> Create<TContext>() => new();
}

/// <summary>
/// Fluent builder for configuring an action interpreter.
/// </summary>
/// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
public sealed class ActionInterpreterBuilder<TContext>
{
    /// <summary>
    /// Terminal handler delegate.
    /// </summary>
    public delegate void TerminalHandler(string token, TContext context);

    /// <summary>
    /// Non-terminal handler delegate.
    /// </summary>
    public delegate void NonTerminalHandler(TContext context, Action[] childActions);

    private readonly Dictionary<string, TerminalHandler> _terminals = new();
    private readonly Dictionary<string, NonTerminalHandler> _nonTerminals = new();

    /// <summary>
    /// Registers a terminal expression handler.
    /// Terminal expressions are leaf nodes that execute actions based on literal tokens.
    /// </summary>
    /// <param name="name">The name of the terminal type.</param>
    /// <param name="handler">Action that executes based on a token value.</param>
    /// <returns>This builder for method chaining.</returns>
    public ActionInterpreterBuilder<TContext> Terminal(string name, TerminalHandler handler)
    {
        _terminals[name] = handler;
        return this;
    }

    /// <summary>
    /// Registers a terminal expression handler without context.
    /// </summary>
    public ActionInterpreterBuilder<TContext> Terminal(string name, Action<string> handler)
        => Terminal(name, (token, _) => handler(token));

    /// <summary>
    /// Registers a non-terminal expression handler.
    /// Non-terminal expressions coordinate child action execution.
    /// </summary>
    /// <param name="name">The name of the non-terminal type.</param>
    /// <param name="handler">Action that coordinates child actions.</param>
    /// <returns>This builder for method chaining.</returns>
    public ActionInterpreterBuilder<TContext> NonTerminal(string name, NonTerminalHandler handler)
    {
        _nonTerminals[name] = handler;
        return this;
    }

    /// <summary>
    /// Registers a non-terminal expression handler without context.
    /// </summary>
    public ActionInterpreterBuilder<TContext> NonTerminal(string name, Action<Action[]> handler)
        => NonTerminal(name, (_, children) => handler(children));

    /// <summary>
    /// Registers a sequence non-terminal that executes children in order.
    /// </summary>
    public ActionInterpreterBuilder<TContext> Sequence(string name = "sequence")
        => NonTerminal(name, (_, children) =>
        {
            foreach (var child in children)
                child();
        });

    /// <summary>
    /// Registers a parallel non-terminal that executes all children.
    /// Note: This still executes synchronously; for true parallelism use AsyncActionInterpreter.
    /// </summary>
    public ActionInterpreterBuilder<TContext> Parallel(string name = "parallel")
        => NonTerminal(name, (_, children) =>
        {
            foreach (var child in children)
                child();
        });

    /// <summary>
    /// Registers a conditional non-terminal that executes the first child if condition passes.
    /// Expects 2 or 3 children: condition (executed), then-branch, optional else-branch.
    /// </summary>
    public ActionInterpreterBuilder<TContext> Conditional(string name, Func<TContext, bool> condition)
        => NonTerminal(name, (ctx, children) =>
        {
            if (children.Length < 2)
                throw new InvalidOperationException($"Conditional '{name}' requires at least 2 children");

            if (condition(ctx))
                children[1]();
            else if (children.Length > 2)
                children[2]();
        });

    /// <summary>
    /// Builds the immutable action interpreter.
    /// </summary>
    /// <returns>A new action interpreter instance.</returns>
    public ActionInterpreter<TContext> Build()
        => new(
            new Dictionary<string, TerminalHandler>(_terminals),
            new Dictionary<string, NonTerminalHandler>(_nonTerminals));
}

/// <summary>
/// An action interpreter for executing expressions with side effects.
/// </summary>
/// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
public sealed class ActionInterpreter<TContext>
{
    private readonly IReadOnlyDictionary<string, ActionInterpreterBuilder<TContext>.TerminalHandler> _terminals;
    private readonly IReadOnlyDictionary<string, ActionInterpreterBuilder<TContext>.NonTerminalHandler> _nonTerminals;

    internal ActionInterpreter(
        IReadOnlyDictionary<string, ActionInterpreterBuilder<TContext>.TerminalHandler> terminals,
        IReadOnlyDictionary<string, ActionInterpreterBuilder<TContext>.NonTerminalHandler> nonTerminals)
    {
        _terminals = terminals;
        _nonTerminals = nonTerminals;
    }

    /// <summary>
    /// Interprets an expression with the given context.
    /// </summary>
    /// <param name="expression">The expression to interpret.</param>
    /// <param name="context">The context for interpretation.</param>
    /// <exception cref="InvalidOperationException">Thrown if the expression type is not registered.</exception>
    public void Interpret(IExpression expression, TContext context)
    {
        switch (expression)
        {
            case TerminalExpression terminal:
                InterpretTerminal(terminal, context);
                break;
            case NonTerminalExpression nonTerminal:
                InterpretNonTerminal(nonTerminal, context);
                break;
            default:
                throw new InvalidOperationException($"Unknown expression type: {expression.GetType().Name}");
        }
    }

    /// <summary>
    /// Interprets an expression with a default context.
    /// </summary>
    public void Interpret(IExpression expression)
        => Interpret(expression, default!);

    /// <summary>
    /// Tries to interpret an expression.
    /// </summary>
    /// <param name="expression">The expression to interpret.</param>
    /// <param name="context">The context for interpretation.</param>
    /// <param name="error">Error message if interpretation failed.</param>
    /// <returns><see langword="true"/> if interpretation succeeded; otherwise <see langword="false"/>.</returns>
    public bool TryInterpret(IExpression expression, TContext context, out string? error)
    {
        try
        {
            Interpret(expression, context);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void InterpretTerminal(TerminalExpression terminal, TContext context)
    {
        if (_terminals.TryGetValue(terminal.Type, out var handler))
        {
            handler(terminal.Value, context);
            return;
        }

        throw new InvalidOperationException($"No terminal handler registered for '{terminal.Type}'");
    }

    private void InterpretNonTerminal(NonTerminalExpression nonTerminal, TContext context)
    {
        if (!_nonTerminals.TryGetValue(nonTerminal.Type, out var handler))
            throw new InvalidOperationException($"No non-terminal handler registered for '{nonTerminal.Type}'");

        var childActions = new Action[nonTerminal.Children.Length];
        for (var i = 0; i < nonTerminal.Children.Length; i++)
        {
            var childExpr = nonTerminal.Children[i];
            childActions[i] = () => Interpret(childExpr, context);
        }

        handler(context, childActions);
    }

    /// <summary>
    /// Checks if a terminal type is registered.
    /// </summary>
    public bool HasTerminal(string type) => _terminals.ContainsKey(type);

    /// <summary>
    /// Checks if a non-terminal type is registered.
    /// </summary>
    public bool HasNonTerminal(string type) => _nonTerminals.ContainsKey(type);
}
