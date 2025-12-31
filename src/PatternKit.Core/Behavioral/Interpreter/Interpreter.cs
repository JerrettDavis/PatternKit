namespace PatternKit.Behavioral.Interpreter;

/// <summary>
/// Interpreter pattern implementation for evaluating expressions in a defined grammar.
/// Provides a fluent API for defining terminal and non-terminal expressions.
/// </summary>
/// <remarks>
/// <para>
/// The Interpreter pattern is used to define a grammar for a simple language and
/// provide an interpreter that evaluates sentences in that language.
/// </para>
/// <para>
/// <b>Key concepts:</b>
/// <list type="bullet">
/// <item><b>Terminal Expression:</b> Leaf nodes that represent literal values (numbers, strings, etc.)</item>
/// <item><b>Non-terminal Expression:</b> Composite nodes that combine other expressions (add, multiply, etc.)</item>
/// <item><b>Context:</b> State passed during interpretation (variables, environment)</item>
/// </list>
/// </para>
/// <para>
/// <b>Example use cases:</b>
/// <list type="bullet">
/// <item>Mathematical expression evaluation</item>
/// <item>Boolean expression parsing</item>
/// <item>Query/filter DSLs</item>
/// <item>Business rule engines</item>
/// <item>Configuration languages</item>
/// </list>
/// </para>
/// <para>
/// <b>Thread-safety:</b> Interpreters built via <see cref="Builder{TContext, TResult}.Build"/>
/// are immutable and thread-safe. The builder is not thread-safe.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Simple arithmetic interpreter
/// var calc = Interpreter&lt;object, double&gt;.Create()
///     .Terminal("number", (token, _) => double.Parse(token))
///     .NonTerminal("add", (args, _) => args[0] + args[1])
///     .NonTerminal("mul", (args, _) => args[0] * args[1])
///     .Build();
///
/// // Interpret expression tree
/// var expr = new NonTerminalExpr("add",
///     new TerminalExpr("number", "1"),
///     new NonTerminalExpr("mul",
///         new TerminalExpr("number", "2"),
///         new TerminalExpr("number", "3")));
///
/// double result = calc.Interpret(expr); // 7.0
/// </code>
/// </example>
public static class Interpreter
{
    /// <summary>Creates a new fluent builder for an interpreter.</summary>
    /// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
    /// <typeparam name="TResult">The type of result produced by interpretation.</typeparam>
    public static Builder<TContext, TResult> Create<TContext, TResult>() => new();
}

/// <summary>
/// Fluent builder for configuring an interpreter.
/// </summary>
/// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
/// <typeparam name="TResult">The type of result produced by interpretation.</typeparam>
public sealed class Builder<TContext, TResult>
{
    private readonly Dictionary<string, Func<string, TContext, TResult>> _terminals = new();
    private readonly Dictionary<string, Func<TResult[], TContext, TResult>> _nonTerminals = new();

    /// <summary>
    /// Registers a terminal expression handler.
    /// Terminal expressions are leaf nodes that produce values from literal tokens.
    /// </summary>
    /// <param name="name">The name of the terminal type.</param>
    /// <param name="handler">Function that interprets a token value into a result.</param>
    /// <returns>This builder for method chaining.</returns>
    public Builder<TContext, TResult> Terminal(string name, Func<string, TContext, TResult> handler)
    {
        _terminals[name] = handler;
        return this;
    }

    /// <summary>
    /// Registers a terminal expression handler without context.
    /// </summary>
    public Builder<TContext, TResult> Terminal(string name, Func<string, TResult> handler)
        => Terminal(name, (token, _) => handler(token));

    /// <summary>
    /// Registers a non-terminal expression handler.
    /// Non-terminal expressions combine child results to produce a new result.
    /// </summary>
    /// <param name="name">The name of the non-terminal type.</param>
    /// <param name="handler">Function that combines child results into a result.</param>
    /// <returns>This builder for method chaining.</returns>
    public Builder<TContext, TResult> NonTerminal(string name, Func<TResult[], TContext, TResult> handler)
    {
        _nonTerminals[name] = handler;
        return this;
    }

    /// <summary>
    /// Registers a non-terminal expression handler without context.
    /// </summary>
    public Builder<TContext, TResult> NonTerminal(string name, Func<TResult[], TResult> handler)
        => NonTerminal(name, (args, _) => handler(args));

    /// <summary>
    /// Registers a binary non-terminal expression handler.
    /// Convenience method for operations that take exactly two operands.
    /// </summary>
    public Builder<TContext, TResult> Binary(string name, Func<TResult, TResult, TContext, TResult> handler)
        => NonTerminal(name, (args, ctx) =>
        {
            if (args.Length != 2)
                throw new InvalidOperationException($"Binary operator '{name}' requires exactly 2 operands, got {args.Length}");
            return handler(args[0], args[1], ctx);
        });

    /// <summary>
    /// Registers a binary non-terminal expression handler without context.
    /// </summary>
    public Builder<TContext, TResult> Binary(string name, Func<TResult, TResult, TResult> handler)
        => Binary(name, (left, right, _) => handler(left, right));

    /// <summary>
    /// Registers a unary non-terminal expression handler.
    /// Convenience method for operations that take exactly one operand.
    /// </summary>
    public Builder<TContext, TResult> Unary(string name, Func<TResult, TContext, TResult> handler)
        => NonTerminal(name, (args, ctx) =>
        {
            if (args.Length != 1)
                throw new InvalidOperationException($"Unary operator '{name}' requires exactly 1 operand, got {args.Length}");
            return handler(args[0], ctx);
        });

    /// <summary>
    /// Registers a unary non-terminal expression handler without context.
    /// </summary>
    public Builder<TContext, TResult> Unary(string name, Func<TResult, TResult> handler)
        => Unary(name, (arg, _) => handler(arg));

    /// <summary>
    /// Builds the immutable interpreter.
    /// </summary>
    /// <returns>A new interpreter instance.</returns>
    public Interpreter<TContext, TResult> Build()
        => new(
            new Dictionary<string, Func<string, TContext, TResult>>(_terminals),
            new Dictionary<string, Func<TResult[], TContext, TResult>>(_nonTerminals));
}

/// <summary>
/// An interpreter for evaluating expressions in a defined grammar.
/// </summary>
/// <typeparam name="TContext">The type of context passed during interpretation.</typeparam>
/// <typeparam name="TResult">The type of result produced by interpretation.</typeparam>
public sealed class Interpreter<TContext, TResult>
{
    private readonly IReadOnlyDictionary<string, Func<string, TContext, TResult>> _terminals;
    private readonly IReadOnlyDictionary<string, Func<TResult[], TContext, TResult>> _nonTerminals;

    internal Interpreter(
        IReadOnlyDictionary<string, Func<string, TContext, TResult>> terminals,
        IReadOnlyDictionary<string, Func<TResult[], TContext, TResult>> nonTerminals)
    {
        _terminals = terminals;
        _nonTerminals = nonTerminals;
    }

    /// <summary>
    /// Interprets an expression with the given context.
    /// </summary>
    /// <param name="expression">The expression to interpret.</param>
    /// <param name="context">The context for interpretation.</param>
    /// <returns>The result of interpretation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the expression type is not registered.</exception>
    public TResult Interpret(IExpression expression, TContext context)
    {
        return expression switch
        {
            TerminalExpression terminal => InterpretTerminal(terminal, context),
            NonTerminalExpression nonTerminal => InterpretNonTerminal(nonTerminal, context),
            _ => throw new InvalidOperationException($"Unknown expression type: {expression.GetType().Name}")
        };
    }

    /// <summary>
    /// Interprets an expression with a default context.
    /// </summary>
    public TResult Interpret(IExpression expression)
        => Interpret(expression, default!);

    /// <summary>
    /// Tries to interpret an expression.
    /// </summary>
    /// <param name="expression">The expression to interpret.</param>
    /// <param name="context">The context for interpretation.</param>
    /// <param name="result">The result if successful.</param>
    /// <returns><see langword="true"/> if interpretation succeeded; otherwise <see langword="false"/>.</returns>
    public bool TryInterpret(IExpression expression, TContext context, out TResult result)
    {
        try
        {
            result = Interpret(expression, context);
            return true;
        }
        catch
        {
            result = default!;
            return false;
        }
    }

    private TResult InterpretTerminal(TerminalExpression terminal, TContext context)
    {
        if (_terminals.TryGetValue(terminal.Type, out var handler))
            return handler(terminal.Value, context);

        throw new InvalidOperationException($"No terminal handler registered for '{terminal.Type}'");
    }

    private TResult InterpretNonTerminal(NonTerminalExpression nonTerminal, TContext context)
    {
        if (!_nonTerminals.TryGetValue(nonTerminal.Type, out var handler))
            throw new InvalidOperationException($"No non-terminal handler registered for '{nonTerminal.Type}'");

        var childResults = new TResult[nonTerminal.Children.Length];
        for (var i = 0; i < nonTerminal.Children.Length; i++)
            childResults[i] = Interpret(nonTerminal.Children[i], context);

        return handler(childResults, context);
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

/// <summary>
/// Base interface for all expressions in the interpreter grammar.
/// </summary>
public interface IExpression
{
    /// <summary>The type/name of this expression.</summary>
    string Type { get; }
}

/// <summary>
/// A terminal expression representing a literal value.
/// </summary>
public sealed class TerminalExpression : IExpression
{
    /// <summary>The type of terminal (e.g., "number", "string", "identifier").</summary>
    public string Type { get; }

    /// <summary>The literal value as a string.</summary>
    public string Value { get; }

    /// <summary>Creates a new terminal expression.</summary>
    /// <param name="type">The type of terminal.</param>
    /// <param name="value">The literal value as a string.</param>
    public TerminalExpression(string type, string value)
    {
        Type = type;
        Value = value;
    }
}

/// <summary>
/// A non-terminal expression representing a composite operation.
/// </summary>
public sealed class NonTerminalExpression : IExpression
{
    /// <summary>The type of operation (e.g., "add", "mul", "if").</summary>
    public string Type { get; }

    /// <summary>The child expressions to combine.</summary>
    public IExpression[] Children { get; }

    /// <summary>Creates a new non-terminal expression.</summary>
    /// <param name="type">The type of operation.</param>
    /// <param name="children">The child expressions to combine.</param>
    public NonTerminalExpression(string type, params IExpression[] children)
    {
        Type = type;
        Children = children;
    }
}

/// <summary>
/// Extension methods for building expressions fluently.
/// </summary>
public static class ExpressionExtensions
{
    /// <summary>Creates a terminal expression.</summary>
    public static TerminalExpression Terminal(string type, string value) => new(type, value);

    /// <summary>Creates a non-terminal expression.</summary>
    public static NonTerminalExpression NonTerminal(string type, params IExpression[] children) => new(type, children);

    /// <summary>Creates a number terminal expression.</summary>
    public static TerminalExpression Number(double value) => new("number", value.ToString());

    /// <summary>Creates a number terminal expression.</summary>
    public static TerminalExpression Number(int value) => new("number", value.ToString());

    /// <summary>Creates a string terminal expression.</summary>
    public static TerminalExpression String(string value) => new("string", value);

    /// <summary>Creates an identifier terminal expression.</summary>
    public static TerminalExpression Identifier(string name) => new("identifier", name);

    /// <summary>Creates a boolean terminal expression.</summary>
    public static TerminalExpression Boolean(bool value) => new("boolean", value.ToString().ToLowerInvariant());
}
