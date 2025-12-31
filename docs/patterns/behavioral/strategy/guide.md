# Strategy Pattern Guide

This guide covers everything you need to know about using the Strategy pattern in PatternKit.

## Overview

Strategy implements first-match-wins predicate dispatch. It evaluates conditions in registration order and executes the handler for the first matching condition. This pattern replaces complex `if-else` cascades with a clean, composable API.

## Getting Started

### Installation

The Strategy pattern is included in the core PatternKit package:

```csharp
using PatternKit.Behavioral.Strategy;
```

### Basic Usage

Create a strategy in three steps:

```csharp
// 1. Create the builder
var strategy = Strategy<int, string>.Create()
    // 2. Add conditional branches
    .When(n => n > 0).Then(_ => "positive")
    .When(n => n < 0).Then(_ => "negative")
    // 3. Add default (optional but recommended)
    .Default(_ => "zero")
    .Build();

// Execute
string result = strategy.Execute(42); // "positive"
```

## Core Concepts

### First-Match-Wins

Predicates are evaluated in registration order. The first match wins:

```csharp
var strategy = Strategy<int, string>.Create()
    .When(n => n > 100).Then(_ => "large")   // Checked first
    .When(n => n > 50).Then(_ => "medium")   // Checked second
    .When(n => n > 0).Then(_ => "small")     // Checked third
    .Default(_ => "zero or negative")
    .Build();

strategy.Execute(150); // "large" - first match wins
strategy.Execute(75);  // "medium"
strategy.Execute(25);  // "small"
```

### Default Handler

Without a default, `Execute` throws when no predicate matches:

```csharp
// No default - throws if nothing matches
var strict = Strategy<int, string>.Create()
    .When(n => n > 0).Then(_ => "positive")
    .Build();

strict.Execute(-1); // Throws InvalidOperationException

// With default - always produces a result
var safe = Strategy<int, string>.Create()
    .When(n => n > 0).Then(_ => "positive")
    .Default(_ => "not positive")
    .Build();

safe.Execute(-1); // "not positive"
```

### The `in` Parameter

Strategy uses `in` parameters for zero-copy pass-through:

```csharp
// Predicates and handlers receive `in` parameters
.When((in LargeStruct s) => s.Flag)
.Then((in LargeStruct s) => s.Value)

// Use static lambdas to avoid closure allocations
.When(static (in n) => n > 0)
.Then(static (in n) => $"Got {n}")
```

## TryStrategy: Non-Throwing Variant

Use `TryStrategy` when "no match" is expected and shouldn't throw:

```csharp
var parser = TryStrategy<string, int>.Create()
    .Always((in string s, out int r) => int.TryParse(s, out r))
    .Always((in string s, out int r) =>
    {
        // Try hex parsing
        if (s.StartsWith("0x"))
            return int.TryParse(s[2..], NumberStyles.HexNumber, null, out r);
        r = 0;
        return false;
    })
    .Finally((in string _, out int r) => { r = 0; return true; })
    .Build();

if (parser.Execute("42", out var dec))
    Console.WriteLine(dec); // 42

if (parser.Execute("0xFF", out var hex))
    Console.WriteLine(hex); // 255

parser.Execute("not a number", out var fallback); // fallback = 0
```

### TryStrategy Methods

- `Always(TryHandler)`: Adds a handler that may succeed or fail
- `Finally(TryHandler)`: Fallback that runs if no handler succeeded

## ActionStrategy: Side Effects Only

Use `ActionStrategy` when you don't need a return value:

```csharp
var notifier = ActionStrategy<OrderEvent>.Create()
    .When(e => e.Type == OrderEventType.Placed)
        .Then(e => emailService.SendOrderConfirmation(e.OrderId))
    .When(e => e.Type == OrderEventType.Shipped)
        .Then(e => smsService.SendShippingNotification(e.OrderId))
    .When(e => e.Type == OrderEventType.Delivered)
        .Then(e => emailService.SendDeliveryConfirmation(e.OrderId))
    .Default(_ => { }) // No-op for other events
    .Build();

notifier.Execute(orderEvent);
```

## Async Strategies

Use async variants for I/O-bound operations:

### AsyncStrategy

```csharp
var asyncRouter = AsyncStrategy<Request, Response>.Create()
    .When(r => r.Path == "/users")
        .Then(async (r, ct) => await userService.GetAllAsync(ct))
    .When(r => r.Path.StartsWith("/users/"))
        .Then(async (r, ct) =>
        {
            var id = r.Path.Split('/').Last();
            return await userService.GetByIdAsync(id, ct);
        })
    .Default(async (_, _) => Response.NotFound())
    .Build();

var response = await asyncRouter.ExecuteAsync(request, cancellationToken);
```

### AsyncActionStrategy

```csharp
var asyncNotifier = AsyncActionStrategy<Event>.Create()
    .When(e => e.IsUrgent)
        .Then(async (e, ct) => await smsService.SendUrgentAlertAsync(e, ct))
    .When(e => e.RequiresEmail)
        .Then(async (e, ct) => await emailService.SendNotificationAsync(e, ct))
    .Default(async (e, ct) => await logService.LogEventAsync(e, ct))
    .Build();

await asyncNotifier.ExecuteAsync(event, cancellationToken);
```

## Common Patterns

### Content Negotiation

```csharp
var serializerSelector = Strategy<string, ISerializer>.Create()
    .When(ct => ct == "application/json").Then(_ => new JsonSerializer())
    .When(ct => ct == "application/xml").Then(_ => new XmlSerializer())
    .When(ct => ct == "text/csv").Then(_ => new CsvSerializer())
    .Default(_ => new PlainTextSerializer())
    .Build();

var contentType = request.Headers["Accept"];
var serializer = serializerSelector.Execute(contentType);
```

### Pricing Rules

```csharp
var priceCalculator = Strategy<Item, decimal>.Create()
    .When(i => i.IsOnClearance).Then(i => i.BasePrice * 0.5m)
    .When(i => i.IsOnSale).Then(i => i.BasePrice * 0.8m)
    .When(i => i.IsWholesale).Then(i => i.BasePrice * 0.9m)
    .Default(i => i.BasePrice)
    .Build();

var finalPrice = priceCalculator.Execute(item);
```

### Feature Flags

```csharp
var featureHandler = Strategy<FeatureContext, IFeature>.Create()
    .When(ctx => ctx.Flags.Contains("new-checkout"))
        .Then(_ => new NewCheckoutFeature())
    .When(ctx => ctx.Flags.Contains("beta-dashboard"))
        .Then(_ => new BetaDashboardFeature())
    .Default(_ => new DefaultFeature())
    .Build();
```

### Multi-Format Parsing

```csharp
var dateParser = TryStrategy<string, DateTime>.Create()
    .Always((in string s, out DateTime d) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", null, DateTimeStyles.None, out d))
    .Always((in string s, out DateTime d) =>
        DateTime.TryParseExact(s, "MM/dd/yyyy", null, DateTimeStyles.None, out d))
    .Always((in string s, out DateTime d) =>
        DateTime.TryParse(s, out d))
    .Build();

if (dateParser.Execute("2024-01-15", out var date))
    Console.WriteLine(date);
```

## Combining with Other Patterns

### With Factory

Use Factory to create strategies dynamically:

```csharp
var strategyFactory = Factory<string, Strategy<Order, decimal>>.Create()
    .Map("us", () => CreateUSPricingStrategy())
    .Map("eu", () => CreateEUPricingStrategy())
    .Map("apac", () => CreateAPACPricingStrategy())
    .Build();

var strategy = strategyFactory.Create(region);
var price = strategy.Execute(order);
```

### With TypeDispatcher

Combine type-based and predicate-based dispatch:

```csharp
var typeDispatcher = TypeDispatcher<Payment, IProcessor>.Create()
    .On<CardPayment>(_ => cardProcessor)
    .On<BankTransfer>(_ => bankProcessor)
    .Build();

var conditionalProcessor = Strategy<PaymentContext, decimal>.Create()
    .When(ctx => ctx.Amount > 10000)
        .Then(ctx => ProcessLargePayment(ctx))
    .Default(ctx =>
    {
        var processor = typeDispatcher.Dispatch(ctx.Payment);
        return processor.Process(ctx);
    })
    .Build();
```

## Performance Tips

1. **Order by frequency**: Put most common matches first
2. **Use static lambdas**: Avoids closure allocations
3. **Cache strategies**: Build once, reuse many times
4. **Simple predicates first**: Fast-fail on cheap checks

```csharp
// Good: static lambdas, no captures
.When(static (in n) => n > 0)
.Then(static (in n) => "positive")

// Good: cheap check first
.When(n => n == 0)        // Fast equality check
.When(n => IsPrime(n))    // Expensive computation second
```

## Troubleshooting

### "No strategy matched"

No predicate matched and no default was provided:

```csharp
// Problem
var s = Strategy<int, string>.Create()
    .When(n => n > 0).Then(_ => "positive")
    .Build();
s.Execute(-1); // Throws!

// Solution: Add default
.Default(_ => "not positive")
```

### Wrong handler executing

Check predicate order - first match wins:

```csharp
// Problem: Order matters
.When(n => n > 0).Then(_ => "positive")    // Matches 100
.When(n => n > 100).Then(_ => "large")     // Never reached for n > 100

// Solution: Most specific first
.When(n => n > 100).Then(_ => "large")     // Check this first
.When(n => n > 0).Then(_ => "positive")    // Then this
```

## Best Practices

1. **Always provide Default**: Unless you want exceptions on no match
2. **Order matters**: Put specific predicates before general ones
3. **Use TryStrategy for parsing**: When failure is expected
4. **Keep predicates pure**: No side effects in conditions
5. **Use meaningful names**: Extract complex predicates to methods

## FAQ

**Q: Can I modify the strategy after building?**
A: No. Strategies are immutable after `Build()`. Create a new one if needed.

**Q: How does this differ from switch expressions?**
A: Strategies are built at runtime, composable, and support complex predicates. Switch is compile-time only.

**Q: When should I use TryStrategy vs Strategy?**
A: Use TryStrategy when "no match" is normal (parsing, lookups). Use Strategy when no match is an error.

**Q: Can predicates have side effects?**
A: Technically yes, but avoid it. Predicates should be pure for predictability.
