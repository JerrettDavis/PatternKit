# Interpreter Pattern API Reference

Complete API documentation for the Interpreter pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Behavioral.Interpreter;
```

---

## Static Entry Point

### `Interpreter`

Static factory class for creating interpreter builders.

```csharp
public static class Interpreter
{
    public static Builder<TContext, TResult> Create<TContext, TResult>();
}
```

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create<TContext, TResult>()` | `Builder<TContext, TResult>` | Creates a new fluent builder for an interpreter |

---

## Builder Class

### `Builder<TContext, TResult>`

Fluent builder for configuring an interpreter.

```csharp
public sealed class Builder<TContext, TResult>
```

#### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TContext` | The type of context passed during interpretation |
| `TResult` | The type of result produced by interpretation |

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Terminal(string name, Func<string, TContext, TResult> handler)` | `Builder<TContext, TResult>` | Registers a terminal expression handler with context |
| `Terminal(string name, Func<string, TResult> handler)` | `Builder<TContext, TResult>` | Registers a terminal expression handler without context |
| `NonTerminal(string name, Func<TResult[], TContext, TResult> handler)` | `Builder<TContext, TResult>` | Registers a non-terminal expression handler with context |
| `NonTerminal(string name, Func<TResult[], TResult> handler)` | `Builder<TContext, TResult>` | Registers a non-terminal expression handler without context |
| `Binary(string name, Func<TResult, TResult, TContext, TResult> handler)` | `Builder<TContext, TResult>` | Registers a binary (2 operand) non-terminal with context |
| `Binary(string name, Func<TResult, TResult, TResult> handler)` | `Builder<TContext, TResult>` | Registers a binary non-terminal without context |
| `Unary(string name, Func<TResult, TContext, TResult> handler)` | `Builder<TContext, TResult>` | Registers a unary (1 operand) non-terminal with context |
| `Unary(string name, Func<TResult, TResult> handler)` | `Builder<TContext, TResult>` | Registers a unary non-terminal without context |
| `Build()` | `Interpreter<TContext, TResult>` | Builds the immutable interpreter |

#### Example

```csharp
var interpreter = Interpreter.Create<MyContext, double>()
    .Terminal("number", token => double.Parse(token))
    .Terminal("var", (name, ctx) => ctx.Variables[name])
    .Binary("add", (left, right) => left + right)
    .Binary("mul", (left, right) => left * right)
    .Unary("negate", value => -value)
    .NonTerminal("sum", args => args.Sum())
    .Build();
```

---

## Interpreter Class

### `Interpreter<TContext, TResult>`

An immutable, thread-safe interpreter for evaluating expressions.

```csharp
public sealed class Interpreter<TContext, TResult>
```

#### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TContext` | The type of context passed during interpretation |
| `TResult` | The type of result produced by interpretation |

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Interpret(IExpression expression, TContext context)` | `TResult` | Interprets an expression with the given context |
| `Interpret(IExpression expression)` | `TResult` | Interprets an expression with default context |
| `TryInterpret(IExpression expression, TContext context, out TResult result)` | `bool` | Tries to interpret; returns false on failure |
| `HasTerminal(string type)` | `bool` | Checks if a terminal type is registered |
| `HasNonTerminal(string type)` | `bool` | Checks if a non-terminal type is registered |

#### Exceptions

| Exception | Condition |
|-----------|-----------|
| `InvalidOperationException` | Expression type not registered, or operand count mismatch |

#### Example

```csharp
var expr = NonTerminal("add", Terminal("number", "1"), Terminal("number", "2"));

// With context
var result = interpreter.Interpret(expr, myContext);

// Without context (uses default)
var result = interpreter.Interpret(expr);

// Safe evaluation
if (interpreter.TryInterpret(expr, context, out var value))
{
    Console.WriteLine($"Result: {value}");
}
```

---

## Expression Interfaces

### `IExpression`

Base interface for all expressions in the interpreter grammar.

```csharp
public interface IExpression
{
    string Type { get; }
}
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `string` | The type/name of this expression (e.g., "number", "add") |

---

## Expression Classes

### `TerminalExpression`

A terminal expression representing a literal value.

```csharp
public sealed class TerminalExpression : IExpression
{
    public string Type { get; }
    public string Value { get; }

    public TerminalExpression(string type, string value);
}
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `string` | The type of terminal (e.g., "number", "string", "identifier") |
| `Value` | `string` | The literal value as a string |

#### Constructor

| Parameter | Type | Description |
|-----------|------|-------------|
| `type` | `string` | The type of terminal |
| `value` | `string` | The literal value |

---

### `NonTerminalExpression`

A non-terminal expression representing a composite operation.

```csharp
public sealed class NonTerminalExpression : IExpression
{
    public string Type { get; }
    public IExpression[] Children { get; }

    public NonTerminalExpression(string type, params IExpression[] children);
}
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Type` | `string` | The type of operation (e.g., "add", "mul", "if") |
| `Children` | `IExpression[]` | The child expressions to combine |

#### Constructor

| Parameter | Type | Description |
|-----------|------|-------------|
| `type` | `string` | The type of operation |
| `children` | `params IExpression[]` | The child expressions |

---

## Expression Extensions

### `ExpressionExtensions`

Static helper methods for building expressions fluently.

```csharp
public static class ExpressionExtensions
```

#### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Terminal(string type, string value)` | `TerminalExpression` | Creates a terminal expression |
| `NonTerminal(string type, params IExpression[] children)` | `NonTerminalExpression` | Creates a non-terminal expression |
| `Number(double value)` | `TerminalExpression` | Creates a number terminal |
| `Number(int value)` | `TerminalExpression` | Creates a number terminal |
| `String(string value)` | `TerminalExpression` | Creates a string terminal |
| `Identifier(string name)` | `TerminalExpression` | Creates an identifier terminal |
| `Boolean(bool value)` | `TerminalExpression` | Creates a boolean terminal |

#### Example

```csharp
using static PatternKit.Behavioral.Interpreter.ExpressionExtensions;

var expr = NonTerminal("add",
    Number(5),
    NonTerminal("mul",
        Identifier("x"),
        Number(3)));
```

---

## Async Variants

### `AsyncInterpreter`

Static factory for async interpreter builders.

```csharp
public static class AsyncInterpreter
{
    public static AsyncBuilder<TContext, TResult> Create<TContext, TResult>();
}
```

### `AsyncInterpreter<TContext, TResult>`

Async interpreter for expressions requiring I/O operations.

```csharp
public sealed class AsyncInterpreter<TContext, TResult>
{
    public ValueTask<TResult> InterpretAsync(IExpression expression, TContext context, CancellationToken ct = default);
    public ValueTask<TResult> InterpretAsync(IExpression expression, CancellationToken ct = default);
}
```

### `AsyncBuilder<TContext, TResult>`

Builder for async interpreters with additional async terminal/non-terminal registration.

```csharp
public sealed class AsyncBuilder<TContext, TResult>
{
    // Sync handlers (wrapped to async)
    public AsyncBuilder<TContext, TResult> Terminal(string name, Func<string, TResult> handler);
    public AsyncBuilder<TContext, TResult> Binary(string name, Func<TResult, TResult, TResult> handler);

    // Async handlers
    public AsyncBuilder<TContext, TResult> Terminal(string name, Func<string, TContext, CancellationToken, ValueTask<TResult>> handler);
    public AsyncBuilder<TContext, TResult> NonTerminal(string name, Func<TResult[], TContext, CancellationToken, ValueTask<TResult>> handler);

    public AsyncInterpreter<TContext, TResult> Build();
}
```

---

## Action Variants

### `ActionInterpreter<TContext>`

Interpreter that executes side effects without returning a value.

```csharp
public sealed class ActionInterpreter<TContext>
{
    public void Interpret(IExpression expression, TContext context);
    public void Interpret(IExpression expression);
}
```

### `AsyncActionInterpreter<TContext>`

Async interpreter for side effects.

```csharp
public sealed class AsyncActionInterpreter<TContext>
{
    public ValueTask InterpretAsync(IExpression expression, TContext context, CancellationToken ct = default);
}
```

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` | No - use from single thread |
| `Interpreter` | Yes - immutable after build |
| `AsyncInterpreter` | Yes - immutable after build |
| `TerminalExpression` | Yes - immutable |
| `NonTerminalExpression` | Yes - immutable |

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
