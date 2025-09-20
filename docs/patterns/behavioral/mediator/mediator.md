# Mediator — Mediator

An allocation-light mediator that coordinates messages between loosely coupled components.
It supports:

- Commands (request/response) with ValueTask
- Notifications (fan-out) with ValueTask
- Streaming commands via IAsyncEnumerable<T> (netstandard2.1+/netcoreapp3.0+)
- Global behaviors: Pre, Post, and Whole (around) with minimal overhead
- Sync adapters for convenience

The mediator is immutable and thread-safe after Build(). Builders are mutable and not thread-safe.

---

## Quick start

```csharp
using PatternKit.Behavioral.Mediator;

var mediator = Mediator.Create()
    // behaviors
    .Pre(static (in object req, CancellationToken _) => { Console.WriteLine($"pre:{req.GetType().Name}"); return default; })
    .Whole(static (in object req, CancellationToken ct, Mediator.MediatorNext next) => next(in req, ct))
    .Post(static (in object req, object? res, CancellationToken _) => { Console.WriteLine($"post:{res}"); return default; })

    // command: Ping -> string
    .Command<Ping, string>(static (in Ping p, CancellationToken _) => new ValueTask<string>("pong:" + p.Value))

    // notification: Note
    .Notification<Note>(static (in Note n, CancellationToken _) => { Console.WriteLine(n.Text); return default; })

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    // streaming command
    .Stream<RangeRequest, int>(static (in RangeRequest r, CancellationToken _) => Range(r.Start, r.Count))
#endif

    .Build();

var s = await mediator.Send<Ping, string>(new Ping(5)); // "pong:5"
await mediator.Publish(new Note("hello"));

#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
await foreach (var i in mediator.Stream<RangeRequest, int>(new RangeRequest(2, 3)))
    Console.WriteLine(i); // 2, 3, 4
#endif

public readonly record struct Ping(int Value);
public readonly record struct Note(string Text);
public readonly record struct RangeRequest(int Start, int Count);
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
static async IAsyncEnumerable<int> Range(int start, int count)
{
    for (int i = 0; i < count; i++) { await Task.Yield(); yield return start + i; }
}
#endif
```

---

## Behaviors

- Pre(in object request, CancellationToken): runs before handler execution.
- Whole(in object request, CancellationToken, MediatorNext next): wraps the handler; can short-circuit or decorate.
- Post(in object request, object? response, CancellationToken): runs after handler completion.

Notifications are not wrapped by Whole behaviors (fire-and-forget fan-out semantics), but Pre/Post still run.

---

## API (at a glance)

```csharp
public sealed class Mediator
{
    // Build
    public static Mediator.Builder Create();

    // Use
    public ValueTask<TResponse> Send<TRequest, TResponse>(in TRequest request, CancellationToken ct = default);
    public ValueTask Publish<TNotification>(in TNotification notification, CancellationToken ct = default);
#if NETSTANDARD2_1 || NETCOREAPP3_0_OR_GREATER
    public IAsyncEnumerable<TItem> Stream<TRequest, TItem>(in TRequest request, CancellationToken ct = default);
#endif

    public sealed class Builder
    {
        // Behaviors
        public Builder Pre(PreBehavior behavior);
        public Builder Post(PostBehavior behavior);
        public Builder Whole(WholeBehavior behavior);

        // Commands
        public Builder Command<TRequest, TResponse>(CommandHandlerTyped<TRequest, TResponse> handler);
        public Builder Command<TRequest, TResponse>(SyncCommandHandlerTyped<TRequest, TResponse> handler);

        // Notifications
        public Builder Notification<TNotification>(NotificationHandlerTyped<TNotification> handler);
        public Builder Notification<TNotification>(SyncNotificationHandlerTyped<TNotification> handler);

        // Streams (netstandard2.1+/netcoreapp3.0+)
        public Builder Stream<TRequest, TItem>(StreamHandlerTyped<TRequest, TItem> handler);

        public Mediator Build();
    }
}
```

### Design notes

- Typed delegates adapt to object-typed internal handlers once at Build time to keep hot paths tight.
- ValueTask everywhere; sync adapters avoid allocation when the work completes synchronously.
- Streaming requires netstandard2.1+/netcoreapp3.0+; the build conditionally includes the feature.

### Error behavior

- Send throws if no command handler is registered for the request type or if the handler returns an incompatible result type.
- Publish without handlers is a no-op.
- Stream throws if no stream handler is registered for the request type.

---

## Testing

See PatternKit.Tests/Behavioral/Mediator/MediatorTests.cs for TinyBDD scenarios covering Send, Publish, and Stream with behaviors.

