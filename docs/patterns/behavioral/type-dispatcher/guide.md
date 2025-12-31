# TypeDispatcher Pattern Guide

This guide covers everything you need to know about using the TypeDispatcher pattern in PatternKit.

## Overview

TypeDispatcher provides type-based routing for polymorphic object hierarchies. When you have a base type with multiple derived types and need to perform different operations based on the concrete type, TypeDispatcher offers a clean, fluent alternative to switch statements or visitor patterns.

## Getting Started

### Installation

The TypeDispatcher pattern is included in the core PatternKit package:

```csharp
using PatternKit.Behavioral.TypeDispatcher;
```

### Basic Usage

Create a dispatcher in three steps:

```csharp
// 1. Define your type hierarchy
public abstract record Message;
public record TextMessage(string Content) : Message;
public record ImageMessage(byte[] Data) : Message;
public record VideoMessage(string Url) : Message;

// 2. Create the dispatcher
var renderer = TypeDispatcher<Message, string>.Create()
    .On<TextMessage>(m => $"<p>{m.Content}</p>")
    .On<ImageMessage>(m => $"<img src=\"data:image/png;base64,{Convert.ToBase64String(m.Data)}\" />")
    .On<VideoMessage>(m => $"<video src=\"{m.Url}\"></video>")
    .Default(_ => "<div>Unknown message type</div>")
    .Build();

// 3. Dispatch based on runtime type
Message msg = new TextMessage("Hello!");
string html = renderer.Dispatch(msg); // <p>Hello!</p>
```

## Core Concepts

### Handler Registration Order

Handlers are evaluated in registration order (first-match-wins). Register more specific types before base types:

```csharp
// Correct: specific types first
var dispatcher = TypeDispatcher<Animal, string>.Create()
    .On<Siamese>(s => "Siamese cat")  // Most specific
    .On<Cat>(c => "Generic cat")       // Less specific
    .On<Animal>(a => "Some animal")    // Least specific (acts as default)
    .Build();

// Wrong: base type catches everything
var bad = TypeDispatcher<Animal, string>.Create()
    .On<Animal>(a => "Some animal")    // Matches all!
    .On<Cat>(c => "Never reached")     // Dead code
    .Build();
```

### Default Handlers

Use `Default()` for unmatched types:

```csharp
var dispatcher = TypeDispatcher<Message, string>.Create()
    .On<TextMessage>(m => "text")
    .Default(_ => "unknown")  // Fallback for any other type
    .Build();
```

Without a default, `Dispatch()` throws for unmatched types:

```csharp
var dispatcher = TypeDispatcher<Message, string>.Create()
    .On<TextMessage>(m => "text")
    .Build();

// Throws InvalidOperationException for ImageMessage
dispatcher.Dispatch(new ImageMessage(...));
```

### TryDispatch for Safe Handling

Use `TryDispatch()` to avoid exceptions:

```csharp
if (dispatcher.TryDispatch(message, out var result))
{
    Console.WriteLine($"Result: {result}");
}
else
{
    Console.WriteLine("No handler matched");
}
```

## Action Dispatchers

Use `ActionTypeDispatcher` when you don't need a return value:

```csharp
var handler = ActionTypeDispatcher<Event>.Create()
    .On<UserCreated>(e => Console.WriteLine($"User {e.UserId} created"))
    .On<UserDeleted>(e => Console.WriteLine($"User {e.UserId} deleted"))
    .On<UserUpdated>(e => Console.WriteLine($"User {e.UserId} updated"))
    .Default(_ => Console.WriteLine("Unknown event"))
    .Build();

handler.Dispatch(new UserCreated("123")); // Prints: User 123 created
```

## Async Dispatchers

Use `AsyncTypeDispatcher` for async operations:

```csharp
var processor = AsyncTypeDispatcher<Command, Result>.Create()
    .On<CreateUser>(async (cmd, ct) =>
    {
        var user = await userService.CreateAsync(cmd.Data, ct);
        return new Result.Success(user.Id);
    })
    .On<DeleteUser>(async (cmd, ct) =>
    {
        await userService.DeleteAsync(cmd.UserId, ct);
        return new Result.Success();
    })
    .Default(async (_, _) => new Result.Error("Unknown command"))
    .Build();

var result = await processor.DispatchAsync(command, cancellationToken);
```

## Common Patterns

### Event Handlers

```csharp
public abstract record DomainEvent;
public record OrderPlaced(Guid OrderId, decimal Total) : DomainEvent;
public record OrderShipped(Guid OrderId, string TrackingNumber) : DomainEvent;
public record OrderCancelled(Guid OrderId, string Reason) : DomainEvent;

var eventHandler = ActionTypeDispatcher<DomainEvent>.Create()
    .On<OrderPlaced>(e => emailService.SendConfirmation(e.OrderId))
    .On<OrderShipped>(e => emailService.SendShippingNotification(e.OrderId, e.TrackingNumber))
    .On<OrderCancelled>(e => emailService.SendCancellation(e.OrderId, e.Reason))
    .Build();
```

### Expression Trees / AST Processing

```csharp
public abstract record Expr;
public record Num(int Value) : Expr;
public record Add(Expr Left, Expr Right) : Expr;
public record Mul(Expr Left, Expr Right) : Expr;

// Create evaluator
Func<Expr, int> eval = null!;
var dispatcher = TypeDispatcher<Expr, int>.Create()
    .On<Num>(n => n.Value)
    .On<Add>(a => eval(a.Left) + eval(a.Right))
    .On<Mul>(m => eval(m.Left) * eval(m.Right))
    .Build();

eval = e => dispatcher.Dispatch(e);

// Usage
var expr = new Add(new Num(1), new Mul(new Num(2), new Num(3)));
int result = eval(expr); // 7
```

### Payment Processing

```csharp
public abstract record Payment(decimal Amount);
public record CashPayment(decimal Amount) : Payment(Amount);
public record CardPayment(decimal Amount, string CardNumber) : Payment(Amount);
public record CryptoPayment(decimal Amount, string WalletAddress) : Payment(Amount);

var processor = TypeDispatcher<Payment, decimal>.Create()
    .On<CashPayment>(_ => 0m)  // No fee
    .On<CardPayment>(p => p.Amount * 0.029m + 0.30m)  // 2.9% + $0.30
    .On<CryptoPayment>(p => p.Amount * 0.01m)  // 1%
    .Build();

decimal fee = processor.Dispatch(payment);
```

## Extending the Pattern

### Composing Dispatchers

Chain multiple dispatchers for complex processing:

```csharp
var validator = TypeDispatcher<Command, ValidationResult>.Create()
    .On<CreateUser>(c => ValidateCreateUser(c))
    .On<UpdateUser>(c => ValidateUpdateUser(c))
    .Default(_ => ValidationResult.Valid)
    .Build();

var executor = AsyncTypeDispatcher<Command, CommandResult>.Create()
    .On<CreateUser>(async (c, ct) => await ExecuteCreate(c, ct))
    .On<UpdateUser>(async (c, ct) => await ExecuteUpdate(c, ct))
    .Build();

// Validate then execute
var validation = validator.Dispatch(command);
if (validation.IsValid)
{
    var result = await executor.DispatchAsync(command);
}
```

### Factory-Created Dispatchers

Use Factory to create dispatchers based on configuration:

```csharp
var dispatcherFactory = Factory<string, TypeDispatcher<Message, string>>.Create()
    .Map("html", () => CreateHtmlRenderer())
    .Map("markdown", () => CreateMarkdownRenderer())
    .Map("plain", () => CreatePlainTextRenderer())
    .Build();

var renderer = dispatcherFactory.Create(outputFormat);
```

## Combining with Other Patterns

### With Chain of Responsibility

Use Chain to add cross-cutting concerns:

```csharp
var chain = ResultChain<Message, string>.Create()
    .When(m => m.IsSpam)
        .Then(_ => "<blocked>")
    .Finally((m, _, _) => dispatcher.Dispatch(m))
    .Build();
```

### With Strategy

Use Strategy for additional conditional logic:

```csharp
var strategy = Strategy<(Message, RenderContext), string>.Create()
    .When((m, ctx) => ctx.IsPreview)
        .Then((m, _) => previewRenderer.Dispatch(m))
    .Default((m, _) => fullRenderer.Dispatch(m))
    .Build();
```

## Best Practices

1. **Order matters**: Register specific types before base types

2. **Always provide a default**: Unless you want exceptions for unmatched types

3. **Keep handlers pure**: Avoid side effects in result dispatchers

4. **Use action variant for effects**: Choose `ActionTypeDispatcher` when no result is needed

5. **Consider async for I/O**: Use async variants when handlers perform I/O

6. **Cache dispatchers**: Build once, reuse many times

## Troubleshooting

### "No strategy matched"

No handler matched and no default was configured:

```csharp
// Add a default handler
.Default(_ => defaultResult)
```

### Handler never called

Check registration order - a more general type may be matching first:

```csharp
// Wrong order
.On<Animal>(...)  // Catches all animals!
.On<Cat>(...)     // Never reached

// Correct order
.On<Cat>(...)     // Specific first
.On<Animal>(...)  // General last
```

### Type not matched despite registration

Ensure the runtime type matches exactly:

```csharp
// This won't match On<Cat> if the runtime type is Siamese
Animal animal = new Siamese();
```

## FAQ

**Q: Can I add handlers after building?**
A: No. Dispatchers are immutable. Create a new one if you need different handlers.

**Q: How does this differ from switch expressions?**
A: TypeDispatcher allows dynamic registration, is composable, and provides a consistent API. Switch expressions are compile-time only.

**Q: What's the performance overhead?**
A: Each dispatch iterates through predicates until a match. For many handlers, consider grouping by category.

**Q: Can I use interfaces instead of base classes?**
A: Yes, any base type works:

```csharp
TypeDispatcher<IMessage, string>.Create()
    .On<TextMessage>(...)
    .Build();
```
