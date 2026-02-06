# Strategy Generator

## Overview

The **Strategy Generator** creates strongly-typed strategy dispatchers from attribute declarations. It generates predicate-based routing with fluent builder APIs, eliminating boilerplate for conditional logic patterns like routing, parsing, and scoring.

## When to Use

Use the Strategy generator when you need to:

- **Route requests conditionally**: Dispatch based on input properties
- **Build rule engines**: Execute different actions based on predicates
- **Create parsers**: Try multiple parsing strategies until one succeeds
- **Implement scoring/labeling**: Map inputs to outputs based on rules

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

### Action Strategy (no return value)

```csharp
using PatternKit.Generators;

[GenerateStrategy(nameof(OrderRouter), typeof(Order), StrategyKind.Action)]
public partial class OrderRouter { }
```

Generated and usage:
```csharp
var router = OrderRouter.Create()
    .When(o => o.Priority == Priority.High)
    .Then(o => Console.WriteLine($"Rush order: {o.Id}"))
    .When(o => o.Total > 1000)
    .Then(o => Console.WriteLine($"Large order: {o.Id}"))
    .Default(o => Console.WriteLine($"Standard order: {o.Id}"))
    .Build();

router.Execute(order); // Routes to first matching predicate
```

### Result Strategy (returns value)

```csharp
[GenerateStrategy(nameof(ScoreLabeler), typeof(int), typeof(string), StrategyKind.Result)]
public partial class ScoreLabeler { }
```

Usage:
```csharp
var labeler = ScoreLabeler.Create()
    .When(s => s >= 90).Then(_ => "A")
    .When(s => s >= 80).Then(_ => "B")
    .When(s => s >= 70).Then(_ => "C")
    .Default(_ => "F")
    .Build();

var grade = labeler.Execute(85); // Returns "B"
```

### Try Strategy (returns bool + out value)

```csharp
[GenerateStrategy(nameof(IntParser), typeof(string), typeof(int), StrategyKind.Try)]
public partial class IntParser { }
```

Usage:
```csharp
var parser = IntParser.Create()
    .Always((in string s, out int? result) =>
    {
        if (int.TryParse(s, out var v)) { result = v; return true; }
        result = null; return false;
    })
    .Build();

if (parser.Execute("42", out var value))
    Console.WriteLine($"Parsed: {value}");
```

## Strategy Kinds

### `StrategyKind.Action`

For strategies that execute actions without returning a value.

**Constructor:**
```csharp
[GenerateStrategy(name, inputType, StrategyKind.Action)]
```

**Generated API:**
```csharp
public void Execute(in TInput input);
public bool TryExecute(in TInput input); // Returns false if no match
```

**Delegates:**
```csharp
public delegate bool Predicate(in TInput input);
public delegate void ActionHandler(in TInput input);
```

### `StrategyKind.Result`

For strategies that return a value based on predicates.

**Constructor:**
```csharp
[GenerateStrategy(name, inputType, outputType, StrategyKind.Result)]
```

**Generated API:**
```csharp
public TOutput Execute(in TInput input); // Throws if no match
```

**Delegates:**
```csharp
public delegate bool Predicate(in TInput input);
public delegate TOutput Handler(in TInput input);
```

### `StrategyKind.Try`

For strategies that try handlers in sequence until one succeeds.

**Constructor:**
```csharp
[GenerateStrategy(name, inputType, outputType, StrategyKind.Try)]
```

**Generated API:**
```csharp
public bool Execute(in TInput input, out TOutput? result);
```

**Delegate:**
```csharp
public delegate bool TryHandler(in TInput input, out TOutput? result);
```

## Attributes

### `[GenerateStrategy]`

Main attribute for declaring strategy types.

| Constructor | Description |
|---|---|
| `(string name, Type inType, StrategyKind kind)` | Action strategy (no output) |
| `(string name, Type inType, Type outType, StrategyKind kind)` | Result/Try strategy |

| Parameter | Type | Description |
|---|---|---|
| `name` | `string` | Name of generated strategy class |
| `inType` | `Type` | Input type for predicates and handlers |
| `outType` | `Type?` | Output type (required for Result/Try) |
| `kind` | `StrategyKind` | Strategy behavior kind |

## Builder API

All strategies use a fluent builder pattern:

### Action Strategy Builder

```csharp
var strategy = MyStrategy.Create()
    .When(predicate).Then(action)    // Conditional action
    .When(predicate).Then(action)    // Multiple conditions
    .Default(action)                  // Fallback (optional)
    .Build();
```

### Result Strategy Builder

```csharp
var strategy = MyStrategy.Create()
    .When(predicate).Then(handler)   // Conditional handler
    .When(predicate).Then(handler)   // Multiple conditions
    .Default(handler)                 // Fallback (optional, but Execute throws if no match)
    .Build();
```

### Try Strategy Builder

```csharp
var strategy = MyStrategy.Create()
    .Always(tryHandler)              // Always try this handler
    .When(condition).Add(handler)    // Conditional handler
    .Finally(tryHandler)             // Always runs last
    .Build();
```

## Execution Behavior

### Predicate Matching (Action/Result)

- Predicates are evaluated **in registration order**
- First matching predicate's handler is executed
- If no predicate matches:
  - Action: `Execute` uses default (if set) or throws; `TryExecute` returns `false`
  - Result: `Execute` uses default (if set) or throws

### Try Handlers

- Handlers are executed **in registration order**
- First handler returning `true` wins
- If no handler succeeds, returns `false` with default output

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **PKGEN001** | Warning | Unable to find matching attribute instance |
| **PKGEN002** | Warning | Invalid attribute arguments |

## Best Practices

### 1. Order Predicates from Most to Least Specific

```csharp
var router = OrderRouter.Create()
    // Most specific first
    .When(o => o.Priority == Priority.Critical && o.Total > 10000)
    .Then(HandleVIPRush)
    // Less specific
    .When(o => o.Priority == Priority.Critical)
    .Then(HandleRush)
    // Least specific / default
    .Default(HandleStandard)
    .Build();
```

### 2. Always Provide a Default When Possible

```csharp
var labeler = ScoreLabeler.Create()
    .When(s => s >= 90).Then(_ => "A")
    .When(s => s >= 80).Then(_ => "B")
    .Default(_ => "F")  // Don't throw for unexpected scores
    .Build();
```

### 3. Use TryExecute for Optional Matching

```csharp
var router = OrderRouter.Create()
    .When(o => o.Type == OrderType.Special)
    .Then(HandleSpecial)
    .Build();

// Don't crash if order isn't special
if (!router.TryExecute(order))
{
    HandleDefault(order);
}
```

### 4. Combine with DI for Handlers

```csharp
public class OrderProcessor
{
    private readonly OrderRouter _router;

    public OrderProcessor(IOrderService orderService, INotifier notifier)
    {
        _router = OrderRouter.Create()
            .When(o => o.IsUrgent)
            .Then(o => orderService.ProcessUrgent(o).GetAwaiter().GetResult())
            .Default(o => orderService.ProcessNormal(o).GetAwaiter().GetResult())
            .Build();
    }

    public void Process(Order order) => _router.Execute(order);
}
```

## Examples

### Request Router

```csharp
[GenerateStrategy(nameof(RequestRouter), typeof(HttpRequest), StrategyKind.Action)]
public partial class RequestRouter { }

var router = RequestRouter.Create()
    .When(r => r.Method == "GET" && r.Path.StartsWith("/api/users"))
    .Then(HandleGetUsers)
    .When(r => r.Method == "POST" && r.Path == "/api/users")
    .Then(HandleCreateUser)
    .When(r => r.Method == "GET" && r.Path == "/health")
    .Then(_ => Console.WriteLine("OK"))
    .Default(_ => throw new HttpException(404, "Not Found"))
    .Build();
```

### Grade Calculator

```csharp
[GenerateStrategy(nameof(GradeCalculator), typeof(StudentScore), typeof(Grade), StrategyKind.Result)]
public partial class GradeCalculator { }

public record StudentScore(int Score, bool ExtraCredit);

var calculator = GradeCalculator.Create()
    .When(s => s.Score >= 90 || (s.Score >= 85 && s.ExtraCredit))
    .Then(_ => Grade.A)
    .When(s => s.Score >= 80)
    .Then(_ => Grade.B)
    .When(s => s.Score >= 70)
    .Then(_ => Grade.C)
    .When(s => s.Score >= 60)
    .Then(_ => Grade.D)
    .Default(_ => Grade.F)
    .Build();

var grade = calculator.Execute(new StudentScore(87, true)); // Grade.A
```

### Multi-Format Parser

```csharp
[GenerateStrategy(nameof(DateParser), typeof(string), typeof(DateTime), StrategyKind.Try)]
public partial class DateParser { }

var parser = DateParser.Create()
    .Always((in string s, out DateTime? result) =>
    {
        if (DateTime.TryParseExact(s, "yyyy-MM-dd", null, default, out var d))
        { result = d; return true; }
        result = null; return false;
    })
    .Always((in string s, out DateTime? result) =>
    {
        if (DateTime.TryParseExact(s, "MM/dd/yyyy", null, default, out var d))
        { result = d; return true; }
        result = null; return false;
    })
    .Finally((in string s, out DateTime? result) =>
    {
        // Last resort: try any format
        if (DateTime.TryParse(s, out var d))
        { result = d; return true; }
        result = null; return false;
    })
    .Build();

if (parser.Execute("2024-01-15", out var date))
    Console.WriteLine($"Parsed: {date:d}");
```

### Discount Calculator

```csharp
[GenerateStrategy(nameof(DiscountCalculator), typeof(Order), typeof(decimal), StrategyKind.Result)]
public partial class DiscountCalculator { }

var calculator = DiscountCalculator.Create()
    .When(o => o.Customer.IsPremium && o.Total > 500)
    .Then(_ => 0.20m)  // 20% for premium with large order
    .When(o => o.Customer.IsPremium)
    .Then(_ => 0.10m)  // 10% for premium
    .When(o => o.Total > 1000)
    .Then(_ => 0.05m)  // 5% for large orders
    .Default(_ => 0m)  // No discount
    .Build();

var discount = calculator.Execute(order);
var finalPrice = order.Total * (1 - discount);
```

## Troubleshooting

### PKGEN002: Invalid attribute arguments

**Cause:** Attribute constructor arguments are incorrect.

**Fix:** Ensure correct constructor is used:
```csharp
// ❌ Wrong: missing output type for Result
[GenerateStrategy("MyStrategy", typeof(int), StrategyKind.Result)]

// ✅ Correct: include output type
[GenerateStrategy("MyStrategy", typeof(int), typeof(string), StrategyKind.Result)]

// ✅ Correct: Action doesn't need output type
[GenerateStrategy("MyStrategy", typeof(int), StrategyKind.Action)]
```

### No predicates match

**Cause:** Input doesn't match any registered predicate.

**Fix:** Add a default handler or use `TryExecute`:
```csharp
// Option 1: Add default
.Default(input => HandleDefault(input))
.Build();

// Option 2: Use TryExecute (Action only)
if (!strategy.TryExecute(input))
    HandleNoMatch(input);
```

## See Also

- [Patterns: Strategy](../patterns/behavioral/strategy/index.md)
- [Builder Generator](builder.md) — For complex object construction
- [Visitor Generator](visitor-generator.md) — For type-based dispatch
