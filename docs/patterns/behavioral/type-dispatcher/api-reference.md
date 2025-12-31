# TypeDispatcher Pattern API Reference

Complete API documentation for the TypeDispatcher pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Behavioral.TypeDispatcher;
```

---

## TypeDispatcher<TBase, TResult>

Type dispatcher that maps runtime types to result-producing handlers.

```csharp
public sealed class TypeDispatcher<TBase, TResult>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TBase` | The base type for dispatchable elements |
| `TResult` | The return type of dispatch operations |

### Delegates

#### `Predicate`

```csharp
public delegate bool Predicate(in TBase node);
```

Determines if a handler applies to a node.

#### `Handler`

```csharp
public delegate TResult Handler(in TBase node);
```

Processes a node and returns a result.

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Dispatch(in TBase node)` | `TResult` | Dispatches to the first matching handler |
| `TryDispatch(in TBase node, out TResult result)` | `bool` | Attempts to dispatch; returns false if no match |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Exceptions

| Method | Exception | Condition |
|--------|-----------|-----------|
| `Dispatch` | `InvalidOperationException` | No handler matches and no default configured |

### Example

```csharp
var dispatcher = TypeDispatcher<Shape, double>.Create()
    .On<Circle>(c => Math.PI * c.Radius * c.Radius)
    .On<Rectangle>(r => r.Width * r.Height)
    .Default(_ => 0)
    .Build();

double area = dispatcher.Dispatch(shape);

if (dispatcher.TryDispatch(shape, out var result))
    Console.WriteLine($"Area: {result}");
```

---

## TypeDispatcher<TBase, TResult>.Builder

Fluent builder for configuring a type dispatcher.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `On<T>(Func<T, TResult> handler)` | `Builder` | Registers a handler for type `T` |
| `On<T>(TResult constant)` | `Builder` | Registers a constant result for type `T` |
| `Default(Handler handler)` | `Builder` | Sets the default handler for unmatched types |
| `Default(Func<TBase, TResult> handler)` | `Builder` | Sets the default handler (convenience overload) |
| `Build()` | `TypeDispatcher<TBase, TResult>` | Builds the immutable dispatcher |

### Type Constraints

- `T` in `On<T>()` must derive from `TBase`

### Example

```csharp
var builder = TypeDispatcher<Message, string>.Create()
    .On<TextMessage>(m => m.Content)
    .On<ImageMessage>("[Image]")  // Constant
    .Default(m => m.ToString()!);

var dispatcher = builder.Build();
```

---

## ActionTypeDispatcher<TBase>

Type dispatcher that executes side effects without returning a value.

```csharp
public sealed class ActionTypeDispatcher<TBase>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TBase` | The base type for dispatchable elements |

### Delegates

#### `Handler`

```csharp
public delegate void Handler(in TBase node);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Dispatch(in TBase node)` | `void` | Dispatches to the first matching handler |
| `TryDispatch(in TBase node)` | `bool` | Attempts to dispatch; returns false if no match |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `On<T>(Action<T> handler)` | `Builder` | Registers an action for type `T` |
| `Default(Handler handler)` | `Builder` | Sets the default action |
| `Build()` | `ActionTypeDispatcher<TBase>` | Builds the immutable dispatcher |

### Example

```csharp
var logger = ActionTypeDispatcher<Event>.Create()
    .On<UserEvent>(e => Log.Info($"User: {e.UserId}"))
    .On<SystemEvent>(e => Log.Debug($"System: {e.Code}"))
    .Default(_ => Log.Trace("Unknown event"))
    .Build();

logger.Dispatch(event);
```

---

## AsyncTypeDispatcher<TBase, TResult>

Async type dispatcher for operations requiring I/O.

```csharp
public sealed class AsyncTypeDispatcher<TBase, TResult>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TBase` | The base type for dispatchable elements |
| `TResult` | The return type of async dispatch operations |

### Delegates

#### `AsyncHandler`

```csharp
public delegate ValueTask<TResult> AsyncHandler(TBase node, CancellationToken ct);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `DispatchAsync(TBase node, CancellationToken ct)` | `ValueTask<TResult>` | Dispatches asynchronously |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `On<T>(Func<T, CancellationToken, ValueTask<TResult>> handler)` | `Builder` | Registers an async handler |
| `On<T>(Func<T, TResult> handler)` | `Builder` | Registers a sync handler (wrapped) |
| `Default(AsyncHandler handler)` | `Builder` | Sets the default async handler |
| `Build()` | `AsyncTypeDispatcher<TBase, TResult>` | Builds the dispatcher |

### Example

```csharp
var processor = AsyncTypeDispatcher<Command, Result>.Create()
    .On<CreateOrder>(async (cmd, ct) =>
    {
        var order = await orderService.CreateAsync(cmd, ct);
        return Result.Success(order.Id);
    })
    .On<GetOrder>(cmd => Result.Success(cmd.OrderId))  // Sync
    .Build();

var result = await processor.DispatchAsync(command, cancellationToken);
```

---

## AsyncActionTypeDispatcher<TBase>

Async type dispatcher for side effects.

```csharp
public sealed class AsyncActionTypeDispatcher<TBase>
```

### Delegates

#### `AsyncHandler`

```csharp
public delegate ValueTask AsyncHandler(TBase node, CancellationToken ct);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `DispatchAsync(TBase node, CancellationToken ct)` | `ValueTask` | Dispatches asynchronously |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `On<T>(Func<T, CancellationToken, ValueTask> handler)` | `Builder` | Registers an async action |
| `On<T>(Action<T> handler)` | `Builder` | Registers a sync action (wrapped) |
| `Default(AsyncHandler handler)` | `Builder` | Sets the default async action |
| `Build()` | `AsyncActionTypeDispatcher<TBase>` | Builds the dispatcher |

### Example

```csharp
var notifier = AsyncActionTypeDispatcher<Event>.Create()
    .On<OrderPlaced>(async (e, ct) =>
    {
        await emailService.SendOrderConfirmationAsync(e.OrderId, ct);
    })
    .On<OrderShipped>(async (e, ct) =>
    {
        await smsService.SendShippingUpdateAsync(e.OrderId, e.TrackingNumber, ct);
    })
    .Build();

await notifier.DispatchAsync(event, cancellationToken);
```

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` | No - use from single thread |
| `TypeDispatcher` | Yes - immutable after build |
| `ActionTypeDispatcher` | Yes - immutable after build |
| `AsyncTypeDispatcher` | Yes - immutable after build |
| `AsyncActionTypeDispatcher` | Yes - immutable after build |

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
