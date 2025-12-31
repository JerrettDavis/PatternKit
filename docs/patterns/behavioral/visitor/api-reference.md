# Visitor Pattern API Reference

Complete API documentation for the Visitor pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Behavioral.Visitor;
```

---

## Visitor\<TBase, TResult\>

Fluent visitor that dispatches by runtime type and returns a result.

```csharp
public sealed class Visitor<TBase, TResult>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TBase` | The base type for visitable elements |
| `TResult` | The return type of visit operations |

### Delegates

#### `Handler`

```csharp
public delegate TResult Handler(in TBase node);
```

Processes a node and returns a result.

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Visit(in TBase node)` | `TResult` | Visits node, returns result |
| `TryVisit(in TBase node, out TResult result)` | `bool` | Attempts visit; returns false if no match |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Exceptions

| Method | Exception | Condition |
|--------|-----------|-----------|
| `Visit` | `InvalidOperationException` | No handler matched and no default |

### Example

```csharp
var visitor = Visitor<Node, string>.Create()
    .On<Add>(_ => "+")
    .On<Number>(n => n.Value.ToString())
    .Default(_ => "?")
    .Build();

var result = visitor.Visit(node);

if (visitor.TryVisit(node, out var r))
    Console.WriteLine(r);
```

---

## Visitor\<TBase, TResult\>.Builder

Fluent builder for configuring the visitor.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `On<T>(Func<T, TResult> handler)` | `Builder` | Registers handler for type T |
| `On<T>(TResult constant)` | `Builder` | Returns constant for type T |
| `Default(Handler handler)` | `Builder` | Sets default handler |
| `Default(Func<TBase, TResult> handler)` | `Builder` | Sets default (convenience) |
| `Build()` | `Visitor<TBase, TResult>` | Builds immutable visitor |

---

## ActionVisitor\<TBase\>

Visitor for side effects without a return value.

```csharp
public sealed class ActionVisitor<TBase>
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Visit(in TBase node)` | `void` | Visits node |
| `TryVisit(in TBase node)` | `bool` | Attempts visit; returns false if no match |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new builder |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `On<T>(Action<T> handler)` | `Builder` | Registers action for type T |
| `Default(Action<TBase> handler)` | `Builder` | Sets default action |
| `Build()` | `ActionVisitor<TBase>` | Builds the visitor |

### Example

```csharp
var visitor = ActionVisitor<Event>.Create()
    .On<OrderPlaced>(e => SendEmail(e.OrderId))
    .On<OrderShipped>(e => SendSms(e.TrackingNumber))
    .Default(_ => Log("Unknown event"))
    .Build();

visitor.Visit(event);
```

---

## AsyncVisitor\<TBase, TResult\>

Async visitor returning a value.

```csharp
public sealed class AsyncVisitor<TBase, TResult>
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `VisitAsync(TBase node, CancellationToken ct)` | `ValueTask<TResult>` | Visits asynchronously |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `On<T>(Func<T, CancellationToken, ValueTask<TResult>> handler)` | `Builder` | Async handler |
| `On<T>(Func<T, TResult> handler)` | `Builder` | Sync handler (wrapped) |
| `Default(...)` | `Builder` | Sets default handler |
| `Build()` | `AsyncVisitor<TBase, TResult>` | Builds the visitor |

### Example

```csharp
var visitor = AsyncVisitor<Request, Response>.Create()
    .On<GetUser>(async (r, ct) => await userService.GetAsync(r.Id, ct))
    .On<CreateUser>(async (r, ct) => await userService.CreateAsync(r.Data, ct))
    .Build();

var response = await visitor.VisitAsync(request, ct);
```

---

## AsyncActionVisitor\<TBase\>

Async visitor for side effects.

```csharp
public sealed class AsyncActionVisitor<TBase>
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `VisitAsync(TBase node, CancellationToken ct)` | `ValueTask` | Visits asynchronously |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `On<T>(Func<T, CancellationToken, ValueTask> handler)` | `Builder` | Async action |
| `On<T>(Action<T> handler)` | `Builder` | Sync action (wrapped) |
| `Default(...)` | `Builder` | Sets default action |
| `Build()` | `AsyncActionVisitor<TBase>` | Builds the visitor |

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` (all variants) | No - use from single thread |
| `Visitor` | Yes - immutable after build |
| `ActionVisitor` | Yes - immutable after build |
| `AsyncVisitor` | Yes - immutable after build |
| `AsyncActionVisitor` | Yes - immutable after build |

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
