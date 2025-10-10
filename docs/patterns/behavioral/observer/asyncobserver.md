# AsyncObserver<TEvent>

An asynchronous, thread-safe event hub for broadcasting events of type `TEvent` to multiple subscribers. Handlers and predicates are `ValueTask`-based, allowing you to await I/O without blocking.

Use it when your observers perform async work (I/O, timers, pipelines) and you want a clean, fluent API matching the synchronous `Observer<TEvent>`.

---

## What it is

- Typed: `AsyncObserver<TEvent>` delivers strongly-typed events.
- Async-first: `Predicate` and `Handler` return `ValueTask`.
- Cancellation: `PublishAsync` accepts a `CancellationToken`.
- Lock-free, copy-on-write subscriptions: publish iterates a snapshot without locks.
- Predicate filters: decide per-subscriber whether to run the handler.
- Immutable & thread-safe after `Build()`.

---

## TL;DR example

```csharp
using PatternKit.Behavioral.Observer;

var hub = AsyncObserver<string>.Create()
    .OnError(static (ex, in msg) => { Console.Error.WriteLine(ex.Message); return ValueTask.CompletedTask; })
    .ThrowAggregate() // default
    .Build();

hub.Subscribe(async (in string msg) => { await Task.Delay(1); Console.WriteLine($"ALL:{msg}"); });

hub.Subscribe(
    predicate: static (in string msg) => new ValueTask<bool>(msg.StartsWith("warn:", StringComparison.Ordinal)),
    handler:   async (in string msg) => { await Task.Yield(); Console.WriteLine($"WARN:{msg}"); });

await hub.PublishAsync("hello");
await hub.PublishAsync("warn: disk");
```

---

## API shape

```csharp
var hub = AsyncObserver<TEvent>.Create()
    .OnError(static (Exception ex, in TEvent evt) => /* log */ ValueTask.CompletedTask) // optional
    .ThrowAggregate()   // default
    // .ThrowFirstError()
    // .SwallowErrors()
    .Build();

IDisposable s1 = hub.Subscribe(static (in TEvent evt) => /* ValueTask handler */);
IDisposable s2 = hub.Subscribe(static (in TEvent evt) => /* ValueTask<bool> filter */, static (in TEvent evt) => /* ValueTask handler */);

await hub.PublishAsync(evt, cancellationToken);
```

- `PublishAsync(TEvent evt, CancellationToken ct = default)` drives the async flow; it does not take `in` because async methods cannot have `in/ref/out` parameters.
- Delegates still use `in TEvent` for zero-copy of large structs.

---

## Error handling policies

Same as `Observer<TEvent>`:

- ThrowAggregate (default): run all matching handlers; collect exceptions and throw a single `AggregateException` at the end. Error sink is awaited for each failure.
- ThrowFirstError: throw immediately on the first failing handler; remaining handlers do not run.
- SwallowErrors: never throw from `PublishAsync`; failures go only to the error sink if configured.

Error sink forms:

- Async: `.OnError((ex, in evt) => ValueTask)`
- Sync adapter: `.OnError((ex, in evt) => void)` which is adapted to `ValueTask`.

---

## Interop with synchronous Observer

You can reuse synchronous delegates via adapter overloads:

```csharp
var asyncHub = AsyncObserver<int>.Create().Build();
asyncHub.Subscribe(static (in int x) => x > 0, static (in int x) => Console.WriteLine($"+:{x}"));
asyncHub.Subscribe(static (in int x) => Console.WriteLine(x));
```

These overloads wrap sync delegates in `ValueTask` delegates with zero allocations on the fast path.

---

## Notes

- Ordering: handlers run in registration order.
- Reentrancy: subscribing/unsubscribing during `PublishAsync` affects subsequent publishes.
- Cancellation: `PublishAsync` checks the token between subscribers; a cancellation stops the loop with `OperationCanceledException`.
- Performance: copy-on-write subscriptions keep publish contention-free; avoid heavy per-event allocations in handlers.

---

## See also

- [Observer](./observer.md) for the synchronous variant
- [ActionChain](../chain/actionchain.md) and [Mediator](../mediator/mediator.md) for other orchestration styles

