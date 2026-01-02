namespace PatternKit.Behavioral.Interpreter;

/// <summary>
/// Async Interpreter pattern implementation for evaluating expressions in a defined grammar.
/// Provides a fluent API for defining terminal and non-terminal expressions with async handlers.
/// </summary>
/// <remarks>
/// <para>
/// This is the async counterpart to <see cref="Interpreter{TContext,TResult}"/>.
/// Use when expression handlers need to perform async operations (database lookups, API calls, etc.).
/// </para>
/// <para>
/// <b>Thread-safety:</b> Interpreters built via <see cref="AsyncInterpreterBuilder{TContext,TResult}.Build"/>
/// are immutable and thread-safe. The builder is not thread-safe.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Async expression interpreter with external data lookups
/// var interpreter = AsyncInterpreter.Create&lt;DbContext, decimal&gt;()
///     .Terminal("price", async (productId, ctx, ct) => await ctx.GetPriceAsync(productId, ct))
///     .Binary("discount", async (price, pct, ctx, ct) => price * (1 - pct / 100))
///     .Build();
///
/// var expr = new NonTerminalExpression("discount",
///     new TerminalExpression("price", "SKU-123"),
///     new TerminalExpression("number", "10"));
///
/// decimal result = await interpreter.InterpretAsync(expr, dbContext);
/// </code>
/// </example>
public static class AsyncInterpreter
{
    /// <summary>Creates a new fluent builder for an async interpreter.</summary>
    /// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
    /// <typeparam name="TResult">The type of result produced by interpretation.</typeparam>
    public static AsyncInterpreterBuilder<TContext, TResult> Create<TContext, TResult>() => new();
}

/// <summary>
/// Fluent builder for configuring an async interpreter.
/// </summary>
/// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
/// <typeparam name="TResult">The type of result produced by interpretation.</typeparam>
public sealed class AsyncInterpreterBuilder<TContext, TResult>
{
    /// <summary>
    /// Async terminal handler delegate.
    /// </summary>
    public delegate ValueTask<TResult> TerminalHandler(string token, TContext context, CancellationToken ct);

    /// <summary>
    /// Async non-terminal handler delegate.
    /// </summary>
    public delegate ValueTask<TResult> NonTerminalHandler(TResult[] args, TContext context, CancellationToken ct);

    private readonly Dictionary<string, TerminalHandler> _terminals = new();
    private readonly Dictionary<string, NonTerminalHandler> _nonTerminals = new();

    /// <summary>
    /// Registers an async terminal expression handler.
    /// Terminal expressions are leaf nodes that produce values from literal tokens.
    /// </summary>
    /// <param name="name">The name of the terminal type.</param>
    /// <param name="handler">Async function that interprets a token value into a result.</param>
    /// <returns>This builder for method chaining.</returns>
    public AsyncInterpreterBuilder<TContext, TResult> Terminal(string name, TerminalHandler handler)
    {
        _terminals[name] = handler;
        return this;
    }

    /// <summary>
    /// Registers an async terminal expression handler without context.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> Terminal(string name, Func<string, CancellationToken, ValueTask<TResult>> handler)
        => Terminal(name, (token, _, ct) => handler(token, ct));

    /// <summary>
    /// Registers a sync terminal expression handler.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> Terminal(string name, Func<string, TContext, TResult> handler)
        => Terminal(name, (token, ctx, _) => new ValueTask<TResult>(handler(token, ctx)));

    /// <summary>
    /// Registers a sync terminal expression handler without context.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> Terminal(string name, Func<string, TResult> handler)
        => Terminal(name, (token, _, _) => new ValueTask<TResult>(handler(token)));

    /// <summary>
    /// Registers an async non-terminal expression handler.
    /// Non-terminal expressions combine child results to produce a new result.
    /// </summary>
    /// <param name="name">The name of the non-terminal type.</param>
    /// <param name="handler">Async function that combines child results into a result.</param>
    /// <returns>This builder for method chaining.</returns>
    public AsyncInterpreterBuilder<TContext, TResult> NonTerminal(string name, NonTerminalHandler handler)
    {
        _nonTerminals[name] = handler;
        return this;
    }

    /// <summary>
    /// Registers an async non-terminal expression handler without context.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> NonTerminal(string name, Func<TResult[], CancellationToken, ValueTask<TResult>> handler)
        => NonTerminal(name, (args, _, ct) => handler(args, ct));

    /// <summary>
    /// Registers a sync non-terminal expression handler.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> NonTerminal(string name, Func<TResult[], TContext, TResult> handler)
        => NonTerminal(name, (args, ctx, _) => new ValueTask<TResult>(handler(args, ctx)));

    /// <summary>
    /// Registers a sync non-terminal expression handler without context.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> NonTerminal(string name, Func<TResult[], TResult> handler)
        => NonTerminal(name, (args, _, _) => new ValueTask<TResult>(handler(args)));

    /// <summary>
    /// Registers an async binary non-terminal expression handler.
    /// Convenience method for operations that take exactly two operands.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> Binary(string name, Func<TResult, TResult, TContext, CancellationToken, ValueTask<TResult>> handler)
        => NonTerminal(name, async (args, ctx, ct) =>
        {
            if (args.Length != 2)
                throw new InvalidOperationException($"Binary operator '{name}' requires exactly 2 operands, got {args.Length}");
            return await handler(args[0], args[1], ctx, ct).ConfigureAwait(false);
        });

    /// <summary>
    /// Registers a sync binary non-terminal expression handler.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> Binary(string name, Func<TResult, TResult, TResult> handler)
        => Binary(name, (left, right, _, _) => new ValueTask<TResult>(handler(left, right)));

    /// <summary>
    /// Registers an async unary non-terminal expression handler.
    /// Convenience method for operations that take exactly one operand.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> Unary(string name, Func<TResult, TContext, CancellationToken, ValueTask<TResult>> handler)
        => NonTerminal(name, async (args, ctx, ct) =>
        {
            if (args.Length != 1)
                throw new InvalidOperationException($"Unary operator '{name}' requires exactly 1 operand, got {args.Length}");
            return await handler(args[0], ctx, ct).ConfigureAwait(false);
        });

    /// <summary>
    /// Registers a sync unary non-terminal expression handler.
    /// </summary>
    public AsyncInterpreterBuilder<TContext, TResult> Unary(string name, Func<TResult, TResult> handler)
        => Unary(name, (arg, _, _) => new ValueTask<TResult>(handler(arg)));

    /// <summary>
    /// Builds the immutable async interpreter.
    /// </summary>
    /// <returns>A new async interpreter instance.</returns>
    public AsyncInterpreter<TContext, TResult> Build()
        => new(
            new Dictionary<string, TerminalHandler>(_terminals),
            new Dictionary<string, NonTerminalHandler>(_nonTerminals));
}

/// <summary>
/// An async interpreter for evaluating expressions in a defined grammar.
/// </summary>
/// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
/// <typeparam name="TResult">The type of result produced by interpretation.</typeparam>
public sealed class AsyncInterpreter<TContext, TResult>
{
    private readonly IReadOnlyDictionary<string, AsyncInterpreterBuilder<TContext, TResult>.TerminalHandler> _terminals;
    private readonly IReadOnlyDictionary<string, AsyncInterpreterBuilder<TContext, TResult>.NonTerminalHandler> _nonTerminals;

    internal AsyncInterpreter(
        IReadOnlyDictionary<string, AsyncInterpreterBuilder<TContext, TResult>.TerminalHandler> terminals,
        IReadOnlyDictionary<string, AsyncInterpreterBuilder<TContext, TResult>.NonTerminalHandler> nonTerminals)
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
    /// <returns>The result of interpretation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the expression type is not registered.</exception>
    public async ValueTask<TResult> InterpretAsync(IExpression expression, TContext context, CancellationToken ct = default)
    {
        return expression switch
        {
            TerminalExpression terminal => await InterpretTerminalAsync(terminal, context, ct).ConfigureAwait(false),
            NonTerminalExpression nonTerminal => await InterpretNonTerminalAsync(nonTerminal, context, ct).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown expression type: {expression.GetType().Name}")
        };
    }

    /// <summary>
    /// Interprets an expression asynchronously with a default context.
    /// </summary>
    public ValueTask<TResult> InterpretAsync(IExpression expression, CancellationToken ct = default)
        => InterpretAsync(expression, default!, ct);

    /// <summary>
    /// Tries to interpret an expression asynchronously.
    /// </summary>
    /// <param name="expression">The expression to interpret.</param>
    /// <param name="context">The context for interpretation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple indicating success and the result if successful.</returns>
    public async ValueTask<(bool success, TResult? result)> TryInterpretAsync(IExpression expression, TContext context, CancellationToken ct = default)
    {
        try
        {
            var result = await InterpretAsync(expression, context, ct).ConfigureAwait(false);
            return (true, result);
        }
        catch
        {
            return (false, default);
        }
    }

    private async ValueTask<TResult> InterpretTerminalAsync(TerminalExpression terminal, TContext context, CancellationToken ct)
    {
        if (_terminals.TryGetValue(terminal.Type, out var handler))
            return await handler(terminal.Value, context, ct).ConfigureAwait(false);

        throw new InvalidOperationException($"No terminal handler registered for '{terminal.Type}'");
    }

    private async ValueTask<TResult> InterpretNonTerminalAsync(NonTerminalExpression nonTerminal, TContext context, CancellationToken ct)
    {
        if (!_nonTerminals.TryGetValue(nonTerminal.Type, out var handler))
            throw new InvalidOperationException($"No non-terminal handler registered for '{nonTerminal.Type}'");

        var childResults = new TResult[nonTerminal.Children.Length];
        for (var i = 0; i < nonTerminal.Children.Length; i++)
            childResults[i] = await InterpretAsync(nonTerminal.Children[i], context, ct).ConfigureAwait(false);

        return await handler(childResults, context, ct).ConfigureAwait(false);
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
