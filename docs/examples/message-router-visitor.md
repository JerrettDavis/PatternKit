# Visitor — Message Router (Background Worker)

Dispatch messages pulled from a queue to processors keyed by runtime type. Scale out by sharing a single immutable visitor across workers.

---

## Messages

```csharp
abstract record Message(string Id);
record UserCreated(string Id, string Email) : Message(Id);
record OrderSubmitted(string Id, string OrderId) : Message(Id);
record InventoryLow(string Id, string Sku, int Qty) : Message(Id);
```

---

## Router (Async Action Visitor)

```csharp
public interface IMessageHandlers
{
    Task On(UserCreated m, CancellationToken ct);
    Task On(OrderSubmitted m, CancellationToken ct);
    Task On(InventoryLow m, CancellationToken ct);
    Task OnUnknown(Message m, CancellationToken ct);
}

public static class MessageRouter
{
    public static AsyncActionVisitor<Message> Build(IMessageHandlers h)
        => AsyncActionVisitor<Message>
            .Create()
            .On<UserCreated>(h.On)
            .On<OrderSubmitted>(h.On)
            .On<InventoryLow>(h.On)
            .Default(h.OnUnknown)
            .Build();
}
```

---

## Worker Loop

```csharp
public sealed class Worker(AsyncActionVisitor<Message> router, ILogger<Worker> log)
{
    public async Task RunAsync(IAsyncEnumerable<Message> stream, CancellationToken ct)
    {
        await foreach (var m in stream.WithCancellation(ct))
        {
            try { await router.VisitAsync(m, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "Message failed: {Id}", m.Id); }
        }
    }
}
```

Operational notes
- Handlers receive a `CancellationToken` for graceful shutdown.
- Add metrics/logging to the default branch to analyze unknown message types.
- For high volume, prefer minimal allocations in handlers.

