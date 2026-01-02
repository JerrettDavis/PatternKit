namespace PatternKit.Behavioral.Interpreter;

/// <summary>
/// Async Action Interpreter pattern implementation for executing expressions with async side effects.
/// Provides a fluent API for defining terminal and non-terminal expressions that perform async actions.
/// </summary>
/// <remarks>
/// <para>
/// This combines features of <see cref="AsyncInterpreter{TContext,TResult}"/> and
/// <see cref="ActionInterpreter{TContext}"/> for async void-returning operations.
/// Use when expressions trigger async side effects (API calls, file I/O, etc.).
/// </para>
/// <para>
/// <b>Thread-safety:</b> Interpreters built via <see cref="AsyncActionInterpreterBuilder{TContext}.Build"/>
/// are immutable and thread-safe. The builder is not thread-safe.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Async action interpreter for notification workflows
/// var interpreter = AsyncActionInterpreter.Create&lt;WorkflowContext&gt;()
///     .Terminal("email", async (address, ctx, ct) => await ctx.SendEmailAsync(address, ct))
///     .Terminal("sms", async (phone, ctx, ct) => await ctx.SendSmsAsync(phone, ct))
///     .Parallel("broadcast") // Execute all notifications concurrently
///     .Build();
///
/// var expr = new NonTerminalExpression("broadcast",
///     new TerminalExpression("email", "user@example.com"),
///     new TerminalExpression("sms", "+1234567890"));
///
/// await interpreter.InterpretAsync(expr, workflowContext);
/// </code>
/// </example>
public static class AsyncActionInterpreter
{
    /// <summary>Creates a new fluent builder for an async action interpreter.</summary>
    /// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
    public static AsyncActionInterpreterBuilder<TContext> Create<TContext>() => new();
}

/// <summary>
/// Fluent builder for configuring an async action interpreter.
/// </summary>
/// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
public sealed class AsyncActionInterpreterBuilder<TContext>
{
    /// <summary>
    /// Async terminal handler delegate.
    /// </summary>
    public delegate ValueTask TerminalHandler(string token, TContext context, CancellationToken ct);

    /// <summary>
    /// Async non-terminal handler delegate.
    /// </summary>
    public delegate ValueTask NonTerminalHandler(TContext context, Func<ValueTask>[] childActions, CancellationToken ct);

    private readonly Dictionary<string, TerminalHandler> _terminals = new();
    private readonly Dictionary<string, NonTerminalHandler> _nonTerminals = new();

    /// <summary>
    /// Registers an async terminal expression handler.
    /// Terminal expressions are leaf nodes that execute async actions based on literal tokens.
    /// </summary>
    /// <param name="name">The name of the terminal type.</param>
    /// <param name="handler">Async action that executes based on a token value.</param>
    /// <returns>This builder for method chaining.</returns>
    public AsyncActionInterpreterBuilder<TContext> Terminal(string name, TerminalHandler handler)
    {
        _terminals[name] = handler;
        return this;
    }

    /// <summary>
    /// Registers an async terminal expression handler without context.
    /// </summary>
    public AsyncActionInterpreterBuilder<TContext> Terminal(string name, Func<string, CancellationToken, ValueTask> handler)
        => Terminal(name, (token, _, ct) => handler(token, ct));

    /// <summary>
    /// Registers a sync terminal expression handler.
    /// </summary>
    public AsyncActionInterpreterBuilder<TContext> Terminal(string name, Action<string, TContext> handler)
        => Terminal(name, (token, ctx, _) =>
        {
            handler(token, ctx);
            return default;
        });

    /// <summary>
    /// Registers a sync terminal expression handler without context.
    /// </summary>
    public AsyncActionInterpreterBuilder<TContext> Terminal(string name, Action<string> handler)
        => Terminal(name, (token, _, _) =>
        {
            handler(token);
            return default;
        });

    /// <summary>
    /// Registers an async non-terminal expression handler.
    /// Non-terminal expressions coordinate child action execution.
    /// </summary>
    /// <param name="name">The name of the non-terminal type.</param>
    /// <param name="handler">Async action that coordinates child actions.</param>
    /// <returns>This builder for method chaining.</returns>
    public AsyncActionInterpreterBuilder<TContext> NonTerminal(string name, NonTerminalHandler handler)
    {
        _nonTerminals[name] = handler;
        return this;
    }

    /// <summary>
    /// Registers an async non-terminal expression handler without context.
    /// </summary>
    public AsyncActionInterpreterBuilder<TContext> NonTerminal(string name, Func<Func<ValueTask>[], CancellationToken, ValueTask> handler)
        => NonTerminal(name, (_, children, ct) => handler(children, ct));

    /// <summary>
    /// Registers a sequence non-terminal that executes children in order.
    /// </summary>
    public AsyncActionInterpreterBuilder<TContext> Sequence(string name = "sequence")
        => NonTerminal(name, async (_, children, _) =>
        {
            foreach (var child in children)
                await child().ConfigureAwait(false);
        });

    /// <summary>
    /// Registers a parallel non-terminal that executes all children concurrently.
    /// </summary>
    public AsyncActionInterpreterBuilder<TContext> Parallel(string name = "parallel")
        => NonTerminal(name, async (_, children, _) =>
        {
            var tasks = new ValueTask[children.Length];
            for (var i = 0; i < children.Length; i++)
                tasks[i] = children[i]();

            foreach (var task in tasks)
                await task.ConfigureAwait(false);
        });

    /// <summary>
    /// Registers a conditional non-terminal that executes children based on a condition.
    /// Expects 2 or 3 children: condition-eval (ignored), then-branch, optional else-branch.
    /// </summary>
    public AsyncActionInterpreterBuilder<TContext> Conditional(string name, Func<TContext, CancellationToken, ValueTask<bool>> condition)
        => NonTerminal(name, async (ctx, children, ct) =>
        {
            if (children.Length < 2)
                throw new InvalidOperationException($"Conditional '{name}' requires at least 2 children");

            if (await condition(ctx, ct).ConfigureAwait(false))
                await children[1]().ConfigureAwait(false);
            else if (children.Length > 2)
                await children[2]().ConfigureAwait(false);
        });

    /// <summary>
    /// Registers a sync conditional non-terminal.
    /// </summary>
    public AsyncActionInterpreterBuilder<TContext> Conditional(string name, Func<TContext, bool> condition)
        => Conditional(name, (ctx, _) => new ValueTask<bool>(condition(ctx)));

    /// <summary>
    /// Builds the immutable async action interpreter.
    /// </summary>
    /// <returns>A new async action interpreter instance.</returns>
    public AsyncActionInterpreter<TContext> Build()
        => new(
            new Dictionary<string, TerminalHandler>(_terminals),
            new Dictionary<string, NonTerminalHandler>(_nonTerminals));
}

/// <summary>
/// An async action interpreter for executing expressions with async side effects.
/// </summary>
/// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
public sealed class AsyncActionInterpreter<TContext>
{
    private readonly IReadOnlyDictionary<string, AsyncActionInterpreterBuilder<TContext>.TerminalHandler> _terminals;
    private readonly IReadOnlyDictionary<string, AsyncActionInterpreterBuilder<TContext>.NonTerminalHandler> _nonTerminals;

    internal AsyncActionInterpreter(
        IReadOnlyDictionary<string, AsyncActionInterpreterBuilder<TContext>.TerminalHandler> terminals,
        IReadOnlyDictionary<string, AsyncActionInterpreterBuilder<TContext>.NonTerminalHandler> nonTerminals)
    {
        _terminals = terminals;
        _nonTerminals = nonTerminals;
    }

    /// <summary>
    /// Interprets an expression asynchronously with the given context.
    /// </summary>
    /// <param name="expression">The expression to interpret.</param>
    /// <param name="context">The context for interpretation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown if the expression type is not registered.</exception>
    public async ValueTask InterpretAsync(IExpression expression, TContext context, CancellationToken ct = default)
    {
        switch (expression)
        {
            case TerminalExpression terminal:
                await InterpretTerminalAsync(terminal, context, ct).ConfigureAwait(false);
                break;
            case NonTerminalExpression nonTerminal:
                await InterpretNonTerminalAsync(nonTerminal, context, ct).ConfigureAwait(false);
                break;
            default:
                throw new InvalidOperationException($"Unknown expression type: {expression.GetType().Name}");
        }
    }

    /// <summary>
    /// Interprets an expression asynchronously with a default context.
    /// </summary>
    public ValueTask InterpretAsync(IExpression expression, CancellationToken ct = default)
        => InterpretAsync(expression, default!, ct);

    /// <summary>
    /// Tries to interpret an expression asynchronously.
    /// </summary>
    /// <param name="expression">The expression to interpret.</param>
    /// <param name="context">The context for interpretation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple indicating success and error message if failed.</returns>
    public async ValueTask<(bool success, string? error)> TryInterpretAsync(IExpression expression, TContext context, CancellationToken ct = default)
    {
        try
        {
            await InterpretAsync(expression, context, ct).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async ValueTask InterpretTerminalAsync(TerminalExpression terminal, TContext context, CancellationToken ct)
    {
        if (_terminals.TryGetValue(terminal.Type, out var handler))
        {
            await handler(terminal.Value, context, ct).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException($"No terminal handler registered for '{terminal.Type}'");
    }

    private async ValueTask InterpretNonTerminalAsync(NonTerminalExpression nonTerminal, TContext context, CancellationToken ct)
    {
        if (!_nonTerminals.TryGetValue(nonTerminal.Type, out var handler))
            throw new InvalidOperationException($"No non-terminal handler registered for '{nonTerminal.Type}'");

        var childActions = new Func<ValueTask>[nonTerminal.Children.Length];
        for (var i = 0; i < nonTerminal.Children.Length; i++)
        {
            var childExpr = nonTerminal.Children[i];
            childActions[i] = () => InterpretAsync(childExpr, context, ct);
        }

        await handler(context, childActions, ct).ConfigureAwait(false);
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
