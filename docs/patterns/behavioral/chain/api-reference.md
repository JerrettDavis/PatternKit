# Chain of Responsibility Pattern API Reference

Complete API documentation for the Chain of Responsibility pattern in PatternKit.

## Namespace

```csharp
using PatternKit.Behavioral.Chain;
```

---

## ActionChain\<TCtx\>

Middleware-style pipeline for side effects.

```csharp
public sealed class ActionChain<TCtx>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TCtx` | The context type threaded through the chain |

### Delegates

#### `Next`

```csharp
public delegate void Next(in TCtx ctx);
```

Continuation delegate passed to handlers.

#### `Handler`

```csharp
public delegate void Handler(in TCtx ctx, Next next);
```

Handler that receives context and continuation.

#### `Predicate`

```csharp
public delegate bool Predicate(in TCtx ctx);
```

Determines if a conditional handler should run.

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Execute(in TCtx ctx)` | `void` | Executes the chain |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Example

```csharp
var chain = ActionChain<Request>.Create()
    .When(r => r.IsAdmin)
        .ThenContinue(r => Log("Admin request"))
    .When(r => !r.IsAuthenticated)
        .ThenStop(r => r.Deny())
    .Finally((in r, next) => { Process(r); next(in r); })
    .Build();

chain.Execute(in request);
```

---

## ActionChain\<TCtx\>.Builder

Fluent builder for configuring the action chain.

```csharp
public sealed class Builder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Use(Handler handler)` | `Builder` | Adds an unconditional handler |
| `When(Predicate predicate)` | `WhenBuilder` | Starts a conditional block |
| `Finally(Handler tail)` | `Builder` | Sets the terminal handler |
| `Build()` | `ActionChain<TCtx>` | Builds the immutable chain |

---

## ActionChain\<TCtx\>.WhenBuilder

Builder for conditional handlers.

```csharp
public sealed class WhenBuilder
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Do(Handler handler)` | `Builder` | Adds handler with full control |
| `ThenContinue(Action<TCtx> action)` | `Builder` | Executes action and continues |
| `ThenStop(Action<TCtx> action)` | `Builder` | Executes action and stops |

---

## ResultChain\<TIn, TOut\>

First-match-wins chain that produces a result.

```csharp
public sealed class ResultChain<TIn, TOut>
```

### Type Parameters

| Parameter | Description |
|-----------|-------------|
| `TIn` | The input type |
| `TOut` | The result type |

### Delegates

#### `Next`

```csharp
public delegate bool Next(in TIn input, out TOut? result);
```

Continuation that may produce a result.

#### `TryHandler`

```csharp
public delegate bool TryHandler(in TIn input, out TOut? result, Next next);
```

Handler that may produce or delegate.

#### `Predicate`

```csharp
public delegate bool Predicate(in TIn input);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Execute(in TIn input, out TOut? result)` | `bool` | Executes; returns true if result produced |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new fluent builder |

### Example

```csharp
var router = ResultChain<Request, Response>.Create()
    .When(r => r.Path == "/health")
        .Then(r => new Response(200, "OK"))
    .Finally((in r, out Response? res, _) => { res = new(404, "Not Found"); return true; })
    .Build();

if (router.Execute(in request, out var response))
    SendResponse(response!);
```

---

## ResultChain\<TIn, TOut\>.Builder

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Use(TryHandler handler)` | `Builder` | Adds a handler |
| `When(Predicate predicate)` | `WhenBuilder` | Starts conditional block |
| `Finally(TryHandler tail)` | `Builder` | Sets terminal fallback |
| `Build()` | `ResultChain<TIn, TOut>` | Builds the chain |

---

## ResultChain\<TIn, TOut\>.WhenBuilder

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Do(TryHandler handler)` | `Builder` | May produce or delegate |
| `Then(Func<TIn, TOut> produce)` | `Builder` | Produces result and stops |

---

## AsyncActionChain\<TCtx\>

Async middleware-style pipeline.

```csharp
public sealed class AsyncActionChain<TCtx>
```

### Delegates

#### `Next`

```csharp
public delegate ValueTask Next(TCtx ctx, CancellationToken ct);
```

#### `Handler`

```csharp
public delegate ValueTask Handler(TCtx ctx, CancellationToken ct, Next next);
```

#### `Predicate`

```csharp
public delegate ValueTask<bool> Predicate(TCtx ctx, CancellationToken ct);
```

#### `Action`

```csharp
public delegate ValueTask Action(TCtx ctx, CancellationToken ct);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ExecuteAsync(TCtx ctx, CancellationToken ct)` | `ValueTask` | Executes asynchronously |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new builder |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Use(Handler handler)` | `Builder` | Adds async handler |
| `When(Predicate predicate)` | `WhenBuilder` | Async conditional |
| `When(Func<TCtx, bool> predicate)` | `WhenBuilder` | Sync predicate wrapper |
| `Finally(Action tail)` | `Builder` | Async terminal handler |
| `Finally(System.Action<TCtx> tail)` | `Builder` | Sync terminal wrapper |
| `Build()` | `AsyncActionChain<TCtx>` | Builds the chain |

### WhenBuilder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ThenContinue(Action action)` | `Builder` | Async action, continue |
| `ThenContinue(System.Action<TCtx> action)` | `Builder` | Sync action wrapper |
| `ThenStop(Action action)` | `Builder` | Async action, stop |
| `ThenStop(System.Action<TCtx> action)` | `Builder` | Sync action wrapper |

### Example

```csharp
var chain = AsyncActionChain<Request>.Create()
    .When(r => r.RequiresAuth)
        .ThenStop(async (r, ct) => await ValidateAsync(r, ct))
    .Finally(async (r, ct) => await ProcessAsync(r, ct))
    .Build();

await chain.ExecuteAsync(request, cancellationToken);
```

---

## AsyncResultChain\<TIn, TOut\>

Async first-match-wins chain with result.

```csharp
public sealed class AsyncResultChain<TIn, TOut>
```

### Delegates

#### `Predicate`

```csharp
public delegate ValueTask<bool> Predicate(TIn input, CancellationToken ct);
```

#### `TryHandler`

```csharp
public delegate ValueTask<(bool success, TOut? result)> TryHandler(TIn input, CancellationToken ct);
```

#### `Producer`

```csharp
public delegate ValueTask<TOut> Producer(TIn input, CancellationToken ct);
```

### Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `ExecuteAsync(TIn input, CancellationToken ct)` | `ValueTask<(bool, TOut?)>` | Executes asynchronously |

### Static Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Create()` | `Builder` | Creates a new builder |

### Builder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Use(TryHandler handler)` | `Builder` | Adds handler |
| `When(Predicate predicate)` | `WhenBuilder` | Async conditional |
| `When(Func<TIn, bool> predicate)` | `WhenBuilder` | Sync predicate wrapper |
| `Finally(TryHandler tail)` | `Builder` | Async fallback |
| `Finally(Producer producer)` | `Builder` | Always-succeed fallback |
| `Finally(Func<TIn, TOut> producer)` | `Builder` | Sync fallback wrapper |
| `Build()` | `AsyncResultChain<TIn, TOut>` | Builds the chain |

### WhenBuilder Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Then(Producer produce)` | `Builder` | Async producer |
| `Then(Func<TIn, TOut> produce)` | `Builder` | Sync producer wrapper |

### Example

```csharp
var router = AsyncResultChain<Request, Response>.Create()
    .When(r => r.Path == "/users")
        .Then(async (r, ct) => await GetUsersAsync(ct))
    .Finally(async (r, ct) => new Response(404, "Not Found"))
    .Build();

var (success, response) = await router.ExecuteAsync(request, ct);
```

---

## Thread Safety

| Component | Thread-Safe |
|-----------|-------------|
| `Builder` (all variants) | No - use from single thread |
| `ActionChain` | Yes - immutable after build |
| `ResultChain` | Yes - immutable after build |
| `AsyncActionChain` | Yes - immutable after build |
| `AsyncResultChain` | Yes - immutable after build |

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [Real-World Examples](real-world-examples.md)
