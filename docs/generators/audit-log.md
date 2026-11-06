# Audit Log Generator

`GenerateAuditLogAttribute` creates a typed `InMemoryAuditLog<TEntry,TKey>` factory from a key selector method.

```csharp
[GenerateAuditLog(typeof(OrderAuditEntry), typeof(string), FactoryName = "CreateLog", LogName = "order-audit")]
public static partial class GeneratedOrderAuditLog
{
    [AuditLogKeySelector]
    private static string SelectKey(OrderAuditEntry entry) => entry.EntryId;
}
```

The generated factory is equivalent to:

```csharp
InMemoryAuditLog<OrderAuditEntry, string>
    .Create("order-audit", SelectKey)
    .Build();
```

Diagnostics:

- `PKAUD001`: host type must be partial.
- `PKAUD002`: exactly one `[AuditLogKeySelector]` method is required.
- `PKAUD003`: key selector must be static and return `TKey` from one `TEntry` parameter.
