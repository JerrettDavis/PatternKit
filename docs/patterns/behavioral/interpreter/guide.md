# Interpreter Pattern Guide

This guide covers everything you need to know about using the Interpreter pattern in PatternKit.

## Overview

The Interpreter pattern lets you define a grammar for a language and provide an interpreter that evaluates sentences in that language. It's ideal for:

- Building domain-specific languages (DSLs)
- Evaluating mathematical expressions
- Implementing business rule engines
- Creating query/filter languages

## Getting Started

### Installation

The Interpreter pattern is included in the core PatternKit package:

```csharp
using PatternKit.Behavioral.Interpreter;
using static PatternKit.Behavioral.Interpreter.ExpressionExtensions;
```

### Basic Usage

Create an interpreter in three steps:

```csharp
// 1. Create the builder
var interpreter = Interpreter.Create<MyContext, double>()

    // 2. Register expression handlers
    .Terminal("number", token => double.Parse(token))
    .Binary("add", (left, right) => left + right)

    // 3. Build the immutable interpreter
    .Build();

// 4. Evaluate expressions
var expr = NonTerminal("add",
    Terminal("number", "5"),
    Terminal("number", "3"));

double result = interpreter.Interpret(expr); // 8.0
```

## Core Concepts

### Terminal Expressions

Terminal expressions are leaf nodes that produce values from literal tokens. They don't have children.

```csharp
// Simple terminal - parse a number
.Terminal("number", token => double.Parse(token))

// Terminal with context - read a variable
.Terminal("var", (token, ctx) => ctx.Variables[token])

// Terminal with validation
.Terminal("positive", token =>
{
    var value = double.Parse(token);
    if (value < 0) throw new ArgumentException("Must be positive");
    return value;
})
```

### Non-Terminal Expressions

Non-terminal expressions combine child results to produce new values.

```csharp
// Binary operation (exactly 2 children)
.Binary("add", (left, right) => left + right)

// Unary operation (exactly 1 child)
.Unary("negate", value => -value)

// General non-terminal (any number of children)
.NonTerminal("sum", args => args.Sum())

// Conditional (3 children: condition, then, else)
.NonTerminal("if", (args, _) =>
    args[0] > 0 ? args[1] : args[2])
```

### Context

Context provides state during interpretation. Use it for variables, configuration, or external data.

```csharp
public class PricingContext
{
    public decimal CartTotal { get; set; }
    public string CustomerTier { get; set; }
    public Dictionary<string, decimal> Variables { get; set; }
}

var interpreter = Interpreter.Create<PricingContext, decimal>()
    .Terminal("cart", (_, ctx) => ctx.CartTotal)
    .Terminal("var", (name, ctx) => ctx.Variables[name])
    .Build();

var context = new PricingContext { CartTotal = 100m };
var result = interpreter.Interpret(expr, context);
```

## Building Expressions

PatternKit provides helper methods for building expression trees:

```csharp
using static PatternKit.Behavioral.Interpreter.ExpressionExtensions;

// Terminal expressions
var num = Terminal("number", "42");
var str = Terminal("string", "hello");
var id = Terminal("identifier", "x");

// Convenience methods
var n = Number(42);        // Terminal("number", "42")
var s = String("hello");   // Terminal("string", "hello")
var b = Boolean(true);     // Terminal("boolean", "true")
var x = Identifier("x");   // Terminal("identifier", "x")

// Non-terminal expressions
var add = NonTerminal("add", Number(1), Number(2));
var nested = NonTerminal("mul",
    NonTerminal("add", Number(1), Number(2)),
    Number(3));
```

## Async Interpreters

Use `AsyncInterpreter` when expressions need to perform async operations:

```csharp
var asyncInterpreter = AsyncInterpreter.Create<Context, decimal>()
    // Sync terminal (still works)
    .Terminal("number", token => decimal.Parse(token))

    // Async terminal - database lookup
    .Terminal("price", async (sku, ctx, ct) =>
    {
        return await db.GetPriceAsync(sku, ct);
    })

    // Async non-terminal
    .NonTerminal("discount", async (args, ctx, ct) =>
    {
        var rate = await GetDiscountRateAsync(ctx.CustomerId, ct);
        return args[0] * rate;
    })
    .Build();

// Evaluate asynchronously
var result = await asyncInterpreter.InterpretAsync(expr, context);
```

## Common Patterns

### Arithmetic Calculator

```csharp
var calc = Interpreter.Create<object, double>()
    .Terminal("number", token => double.Parse(token))
    .Binary("add", (l, r) => l + r)
    .Binary("sub", (l, r) => l - r)
    .Binary("mul", (l, r) => l * r)
    .Binary("div", (l, r) => r != 0 ? l / r : throw new DivideByZeroException())
    .Unary("neg", v => -v)
    .Unary("abs", Math.Abs)
    .Build();
```

### Boolean Logic

```csharp
var logic = Interpreter.Create<Context, bool>()
    .Terminal("true", _ => true)
    .Terminal("false", _ => false)
    .Terminal("var", (name, ctx) => ctx.GetBool(name))
    .Binary("and", (l, r) => l && r)
    .Binary("or", (l, r) => l || r)
    .Unary("not", v => !v)
    .Build();
```

### Business Rules Engine

```csharp
var rules = Interpreter.Create<OrderContext, decimal>()
    // Variables
    .Terminal("var", (name, ctx) => name switch
    {
        "subtotal" => ctx.Subtotal,
        "item_count" => ctx.Items.Count,
        "tier_discount" => ctx.Customer.TierDiscount,
        _ => ctx.Variables.GetValueOrDefault(name, 0m)
    })

    // Arithmetic
    .Binary("add", (l, r) => l + r)
    .Binary("mul", (l, r) => l * r)
    .Binary("min", Math.Min)
    .Binary("max", Math.Max)

    // Comparisons (return 1 for true, 0 for false)
    .Binary("gt", (l, r) => l > r ? 1m : 0m)
    .Binary("gte", (l, r) => l >= r ? 1m : 0m)

    // Conditional
    .NonTerminal("if", (args, _) =>
        args[0] > 0 ? args[1] : args[2])

    .Build();
```

## Extending the Pattern

### Custom Expression Types

You can create custom expression classes for domain-specific needs:

```csharp
public class DiscountExpression : IExpression
{
    public string Type => "discount";
    public string DiscountCode { get; }
    public IExpression Amount { get; }

    public DiscountExpression(string code, IExpression amount)
    {
        DiscountCode = code;
        Amount = amount;
    }
}
```

### Expression Builders

Create fluent builders for complex expressions:

```csharp
public static class RuleBuilder
{
    public static IExpression PercentOff(decimal percent, IExpression baseAmount)
        => NonTerminal("mul", baseAmount, Terminal("number", (percent / 100).ToString()));

    public static IExpression IfOver(decimal threshold, IExpression thenExpr, IExpression elseExpr)
        => NonTerminal("if",
            NonTerminal("gt", Terminal("var", "subtotal"), Terminal("number", threshold.ToString())),
            thenExpr,
            elseExpr);
}

// Usage
var rule = RuleBuilder.IfOver(100m,
    RuleBuilder.PercentOff(10m, Terminal("var", "subtotal")),
    Terminal("number", "0"));
```

## Combining with Other Patterns

### With Factory

Use Factory to create interpreters based on configuration:

```csharp
var interpreterFactory = Factory<string, Interpreter<Context, decimal>>.Create()
    .Map("pricing", () => CreatePricingInterpreter())
    .Map("tax", () => CreateTaxInterpreter())
    .Map("shipping", () => CreateShippingInterpreter())
    .Build();

var interpreter = interpreterFactory.Create("pricing");
```

### With Strategy

Use Strategy to select different evaluation strategies:

```csharp
var evaluator = Strategy<EvalRequest, decimal>.Create()
    .When(req => req.Type == "pricing")
        .Then(req => pricingInterpreter.Interpret(req.Expression, req.Context))
    .When(req => req.Type == "tax")
        .Then(req => taxInterpreter.Interpret(req.Expression, req.Context))
    .Default(req => 0m)
    .Build();
```

### With Chain

Use Chain for multi-stage interpretation:

```csharp
var pipeline = ResultChain<EvalContext, decimal>.Create()
    .When(ctx => ctx.HasDiscounts)
        .Then(ctx => discountInterpreter.Interpret(ctx.DiscountRule, ctx))
    .When(ctx => ctx.HasTax)
        .Then(ctx => taxInterpreter.Interpret(ctx.TaxRule, ctx))
    .Finally((ctx, _) => ctx.BaseAmount)
    .Build();
```

## Best Practices

1. **Keep grammars simple**: Complex grammars are hard to maintain. Consider parser generators for complex languages.

2. **Validate expressions early**: Check expression structure before interpretation to provide clear error messages.

3. **Use context sparingly**: Pass only what's needed. Large contexts slow down evaluation.

4. **Cache interpreters**: Build once, reuse many times. Interpreters are immutable and thread-safe.

5. **Prefer binary/unary helpers**: They validate operand counts and provide better error messages.

6. **Document your grammar**: Create a grammar specification for users of your DSL.

## Troubleshooting

### "No terminal handler registered for 'xyz'"

You're trying to interpret an expression type that wasn't registered:

```csharp
// Wrong: using "num" when "number" was registered
Terminal("num", "42")

// Right: use the registered name
Terminal("number", "42")
```

### "Binary operator requires exactly 2 operands"

Your expression has the wrong number of children:

```csharp
// Wrong: 3 children for a binary operator
NonTerminal("add", Number(1), Number(2), Number(3))

// Right: use a general non-terminal for n-ary operations
.NonTerminal("sum", args => args.Sum())
```

### Stack overflow with recursive expressions

Be careful with self-referential expressions. Add depth limits:

```csharp
.NonTerminal("recurse", (args, ctx) =>
{
    if (ctx.Depth++ > 100)
        throw new InvalidOperationException("Max recursion depth exceeded");
    return Interpret(args[0], ctx);
})
```

## FAQ

**Q: Can I modify an interpreter after building?**
A: No. Interpreters are immutable. Create a new one if you need different behavior.

**Q: How do I handle errors gracefully?**
A: Use `TryInterpret` which returns `false` instead of throwing:

```csharp
if (interpreter.TryInterpret(expr, context, out var result))
    Console.WriteLine($"Result: {result}");
else
    Console.WriteLine("Interpretation failed");
```

**Q: Can I serialize expressions?**
A: Yes. Expressions are plain objects. Use your preferred serialization (JSON, etc.) and rebuild the tree on deserialization.

**Q: What's the performance overhead?**
A: Each expression node involves a dictionary lookup and delegate invocation. For hot paths, consider compiling expressions to IL or caching results.
