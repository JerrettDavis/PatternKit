# Interpreter Generator

The Interpreter generator turns annotated rule methods into an immutable `Interpreter<TContext, TResult>` factory. Use it when a DSL grammar is stable enough to describe in code and you want compile-time diagnostics, deterministic registration order, and a factory that can be imported through `IServiceCollection`.

## Quick Start

```csharp
using PatternKit.Generators.Interpreter;

[GenerateInterpreter(typeof(PricingContext), typeof(decimal), FactoryMethodName = "Build")]
public static partial class PricingRules
{
    [InterpreterTerminal("number")]
    private static decimal Number(string token) => decimal.Parse(token);

    [InterpreterTerminal("cart_total")]
    private static decimal CartTotal(string token, PricingContext context) => context.CartTotal;

    [InterpreterNonTerminal("add")]
    private static decimal Add(decimal[] args) => args[0] + args[1];

    [InterpreterNonTerminal("round")]
    private static decimal Round(decimal[] args) => Math.Round(args[0], 2);
}

var interpreter = PricingRules.Build();
```

The generated method composes the existing fluent runtime:

```csharp
var builder = Interpreter.Create<PricingContext, decimal>();
builder.Terminal("number", static (token, context) => Number(token));
builder.Terminal("cart_total", static (token, context) => CartTotal(token, context));
builder.NonTerminal("add", static (args, context) => Add(args));
builder.NonTerminal("round", static (args, context) => Round(args));
return builder.Build();
```

## Attribute Model

| Attribute | Target | Purpose |
| --- | --- | --- |
| `[GenerateInterpreter(typeof(TContext), typeof(TResult))]` | partial class or struct | Generates an `Interpreter<TContext, TResult>` factory. |
| `[InterpreterTerminal("name")]` | static method | Registers a terminal handler for token values. |
| `[InterpreterNonTerminal("name")]` | static method | Registers a non-terminal handler for child results. |

## Valid Rule Signatures

Terminal handlers return `TResult` and accept either:

```csharp
static TResult Rule(string token)
static TResult Rule(string token, TContext context)
```

Non-terminal handlers return `TResult` and accept either:

```csharp
static TResult Rule(TResult[] args)
static TResult Rule(TResult[] args, TContext context)
```

Rules can be private because the generated factory is emitted into the same partial type.

## Diagnostics

| Id | Meaning |
| --- | --- |
| `PKINT001` | The host type is marked with `[GenerateInterpreter]` but is not partial. |
| `PKINT002` | The host has no terminal or non-terminal rules. |
| `PKINT003` | A rule method is not static, returns the wrong type, or has an invalid parameter list. |
| `PKINT004` | A terminal or non-terminal rule name is registered more than once. |

## IServiceCollection Integration

Register the generated interpreter as a singleton. The interpreter is immutable after build and can be safely shared by request handlers, hosted services, ASP.NET Core endpoints, and background workers.

```csharp
public static IServiceCollection AddPricingRules(this IServiceCollection services)
{
    services.AddSingleton(_ => PricingRules.Build());
    return services;
}
```

PatternKit's e-commerce Interpreter example exposes both fluent and generated pricing/eligibility interpreters and registers the generated path through `AddPatternKitExamples`.

## See Also

- [Interpreter Pattern](../patterns/behavioral/interpreter/index.md)
- [Interpreter Real-World Examples](../patterns/behavioral/interpreter/real-world-examples.md)
- [Pattern Coverage Guide](../guides/pattern-coverage.md)
