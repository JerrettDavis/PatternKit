# Strategy Pattern API Reference

Complete API documentation for the Strategy pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Behavioral.Strategy;
```

---

## Strategy\<TIn, TOut\>

First-match-wins synchronous strategy that returns a result.

```csharp
public sealed class Strategy<TIn, TOut>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TIn` | The input type |
| `TOut` | The result type |

### Delegates

#### `Predicate`

```csharp
public delegate bool Predicate(in TIn input);
```

Determines if a branch should execute.

#### `Handler`

```csharp
public delegate TOut Handler(in TIn input);
```

Produces the result when the branch is selected.

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Execute(in TIn input)` | `TOut` | Executes first matching handler |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Exceptions

| Method | Exception | Condition |
|--------|-----------|-----------|
| `Execute` | `InvalidOperationException` | No predicate matched and no default |

### Example

```csharp
var strategy = Strategy<int, string>.Create()
    .When(n => n > 0).Then(_ => "positive")
    .When(n => n < 0).Then(_ => "negative")
    .Default(_ => "zero")
    .Build();

var result = strategy.Execute(42); // "positive"
```

---

## Strategy\<TIn, TOut\>.Builder

Fluent builder for configuring the strategy.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `When(Predicate predicate)` | `WhenBuilder` | Starts a conditional branch |
| `Default(Handler handler)` | `Builder` | Sets the default handler |
| `Build()` | `Strategy<TIn, TOut>` | Builds the immutable strategy |

---

## Strategy\<TIn, TOut\>.WhenBuilder

Builder for a conditional branch.

```csharp
public sealed class WhenBuilder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Then(Handler handler)` | `Builder` | Sets the handler for this branch |
| `Then(TOut constant)` | `Builder` | Returns a constant value |

---

## TryStrategy\<TIn, TOut\>

First-success non-throwing strategy.

```csharp
public sealed class TryStrategy<TIn, TOut>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TIn` | The input type |
| `TOut` | The result type |

### Delegates

#### `TryHandler`

```csharp
public delegate bool TryHandler(in TIn input, out TOut? result);
```

Attempts to produce a result; returns true on success.

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Execute(in TIn input, out TOut? result)` | `bool` | Attempts execution; returns true if handler succeeded |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Always(TryHandler handler)` | `Builder` | Adds a handler that may succeed |
| `Finally(TryHandler handler)` | `Builder` | Sets fallback handler |
| `Build()` | `TryStrategy<TIn, TOut>` | Builds the strategy |

### Example

```csharp
var parser = TryStrategy<string, int>.Create()
    .Always((in string s, out int r) => int.TryParse(s, out r))
    .Finally((in string _, out int r) => { r = 0; return true; })
    .Build();

if (parser.Execute("42", out var n))
    Console.WriteLine(n);
```

---

## ActionStrategy\<TIn\>

First-match-wins strategy for side effects only.

```csharp
public sealed class ActionStrategy<TIn>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TIn` | The input type |

### Delegates

#### `Predicate`

```csharp
public delegate bool Predicate(in TIn input);
```

#### `Handler`

```csharp
public delegate void Handler(in TIn input);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Execute(in TIn input)` | `void` | Executes first matching handler |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `When(Predicate predicate)` | `WhenBuilder` | Starts conditional branch |
| `Default(Handler handler)` | `Builder` | Sets default handler |
| `Build()` | `ActionStrategy<TIn>` | Builds the strategy |

### Example

```csharp
var logger = ActionStrategy<LogEvent>.Create()
    .When(e => e.Level == LogLevel.Error)
        .Then(e => Console.Error.WriteLine(e.Message))
    .When(e => e.Level == LogLevel.Warning)
        .Then(e => Console.WriteLine($"WARN: {e.Message}"))
    .Default(e => Console.WriteLine(e.Message))
    .Build();

logger.Execute(logEvent);
```

---

## AsyncStrategy\<TIn, TOut\>

Async first-match-wins strategy with result.

```csharp
public sealed class AsyncStrategy<TIn, TOut>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TIn` | The input type |
| `TOut` | The result type |

### Delegates

#### `Predicate`

```csharp
public delegate ValueTask<bool> Predicate(TIn input, CancellationToken ct);
```

#### `Handler`

```csharp
public delegate ValueTask<TOut> Handler(TIn input, CancellationToken ct);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ExecuteAsync(TIn input, CancellationToken ct)` | `ValueTask<TOut>` | Executes asynchronously |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new builder |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `When(Predicate predicate)` | `WhenBuilder` | Async conditional |
| `When(Func<TIn, bool> predicate)` | `WhenBuilder` | Sync predicate wrapper |
| `Default(Handler handler)` | `Builder` | Async default handler |
| `Default(Func<TIn, TOut> handler)` | `Builder` | Sync default wrapper |
| `Build()` | `AsyncStrategy<TIn, TOut>` | Builds the strategy |

### WhenBuilder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Then(Handler handler)` | `Builder` | Async handler |
| `Then(Func<TIn, TOut> handler)` | `Builder` | Sync handler wrapper |

### Example

```csharp
var router = AsyncStrategy<Request, Response>.Create()
    .When(r => r.Path == "/health")
        .Then(async (r, ct) => Response.Ok("healthy"))
    .When(r => r.Path.StartsWith("/api/"))
        .Then(async (r, ct) => await apiHandler.HandleAsync(r, ct))
    .Default(async (r, ct) => Response.NotFound())
    .Build();

var response = await router.ExecuteAsync(request, ct);
```

---

## AsyncActionStrategy\<TIn\>

Async first-match-wins strategy for side effects.

```csharp
public sealed class AsyncActionStrategy<TIn>
```

### Delegates

#### `Predicate`

```csharp
public delegate ValueTask<bool> Predicate(TIn input, CancellationToken ct);
```

#### `Handler`

```csharp
public delegate ValueTask Handler(TIn input, CancellationToken ct);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ExecuteAsync(TIn input, CancellationToken ct)` | `ValueTask` | Executes asynchronously |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new builder |

### Example

```csharp
var notifier = AsyncActionStrategy<Event>.Create()
    .When(e => e.IsUrgent)
        .Then(async (e, ct) => await smsService.SendAlertAsync(e, ct))
    .Default(async (e, ct) => await logService.LogAsync(e, ct))
    .Build();

await notifier.ExecuteAsync(event, ct);
```

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` (all variants) | No - use from single thread |
| `Strategy` | Yes - immutable after build |
| `TryStrategy` | Yes - immutable after build |
| `ActionStrategy` | Yes - immutable after build |
| `AsyncStrategy` | Yes - immutable after build |
| `AsyncActionStrategy` | Yes - immutable after build |

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
