# Observer<TEvent>

A thread-safe, fluent event hub for broadcasting events of type `TEvent` to multiple subscribers. Observers can opt-in via an optional predicate filter.

Use it when you need a decoupled, in-process publish/subscribe mechanism: UI events, domain notifications, pipeline hooks, or instrumentation.

---

## What it is

- Typed: `Observer<TEvent>` delivers strongly-typed events.
- Fluent builder: configure error handling, then `Build()`.
- Lock-free copy-on-write subscriptions: publish reads a snapshot array; subscribe/unsubscribe are atomic.
- Predicate filters: deliver to a handler only when a condition matches.
- Immutable & thread-safe after `Build()`.

---

## TL;DR example

```csharp
using PatternKit.Behavioral.Observer;

var hub = Observer<string>.Create()
    .OnError(static (ex, in msg) => Console.Error.WriteLine($"handler failed: {ex.Message} for '{msg}'"))
    .ThrowAggregate() // default; collect all exceptions and throw one AggregateException
    .Build();

// subscribe everyone
var subAll = hub.Subscribe(static (in string msg) => Console.WriteLine($"ALL: {msg}"));

// subscribe with a filter
var subWarn = hub.Subscribe(static (in string msg) => msg.StartsWith("warn:", StringComparison.Ordinal),
                            static (in string msg) => Console.WriteLine($"WARN: {msg}"));

hub.Publish("hello");
hub.Publish("warn: low-disk");

subWarn.Dispose(); // unsubscribe
```

---

## API shape

```csharp
var hub = Observer<TEvent>.Create()
    .OnError(static (Exception ex, in TEvent evt) => /* log */) // optional
    .ThrowAggregate()   // default
    // .ThrowFirstError() // stop at first exception
    // .SwallowErrors()   // never throw to caller
    .Build();

IDisposable s1 = hub.Subscribe(static (in TEvent evt) => /* handler */);
IDisposable s2 = hub.Subscribe(static (in TEvent evt) => /* bool filter */, static (in TEvent evt) => /* handler */);

hub.Publish(in evt);

s1.Dispose(); // unsubscribe
```

Delegates use `in TEvent` to avoid copying large structs. Subscriptions return `IDisposable`; disposing is idempotent.

---

## Error handling policies

Choose via the builder (defaults to `ThrowAggregate`):

- ThrowAggregate: run all matching handlers; collect exceptions and throw a single `AggregateException` at the end. The error sink still receives each failure.
- ThrowFirstError: throw immediately on the first failing handler; remaining handlers do not run.
- SwallowErrors: never throw from `Publish`; all exceptions are sent to the error sink if provided.

You can configure an error sink with `.OnError((ex, in evt) => ...)`. The sink should not throw; if it does, it is swallowed to protect the publisher.

---

## Design notes

- Copy-on-write subscriptions: publishing is contention-free; subscribe/unsubscribe perform an atomic array swap.
- Ordering: handlers are invoked in registration order.
- Filters: evaluated per-subscriber; if `false`, the handler is skipped.
- Reentrancy: handlers may subscribe/unsubscribe during `Publish`; such changes affect subsequent publishes (not the current one).
- Thread-safety: built instances are safe to share across threads; builder is not thread-safe.

---

## When to use

- Decouple producers and consumers inside a process.
- Many readers, few writers: telemetry, UI, domain events.
- Need simple filtering at the subscriber side.

If you need cross-process or durable messaging, integrate with a message bus (RabbitMQ, Azure Service Bus) and consider using Observer as an in-process fan-out.

---

## See also

- [Mediator](../mediator/mediator.md): coordinate interactions via a central mediator.
- [ActionChain](../chain/actionchain.md): middleware pipeline for conditional processing.
- Examples: [Observer demo](../../../examples/observer-demo.md)

