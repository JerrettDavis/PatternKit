# Channel Purger

Channel Purger removes stale, invalid, or operationally obsolete messages from a `MessageChannel<TPayload>` under an explicit maintenance workflow. It is useful for back-office cleanup, replay-window reset flows, and bounded in-memory channels where abandoned work should be audited before removal.

## Fluent

```csharp
var purger = ChannelPurger<InventoryMaintenanceCommand>.Create("inventory-maintenance-purger")
    .From(channel)
    .When(message => message.Payload.Reason.StartsWith("stale-", StringComparison.Ordinal))
    .AuditWith(record => audit.Record(record))
    .Build();

var result = purger.Purge();
```

The predicate is optional. Without a predicate, the purger drains the full channel. Messages that do not match the predicate remain in their original order.

## Source Generator

Use `[GenerateChannelPurger]` when the purger factory should be owned by generated code and imported through dependency injection:

```csharp
[GenerateChannelPurger(typeof(InventoryMaintenanceCommand), PurgerName = "inventory-maintenance-purger")]
public static partial class GeneratedInventoryChannelPurger;
```

The generated `Create(MessageChannel<TPayload> channel)` method returns a configured `ChannelPurger<TPayload>`.
