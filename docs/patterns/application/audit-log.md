# Audit Log

Audit Log records append-only facts about who did what and when. Use it for compliance trails, operational investigation, security review, and business event traceability.

PatternKit provides `IAuditLog<TEntry,TKey>` and `InMemoryAuditLog<TEntry,TKey>` in `PatternKit.Application.AuditLog`.

```csharp
var log = InMemoryAuditLog<OrderAuditEntry, string>
    .Create("order-audit", entry => entry.EntryId)
    .Build();

await log.AppendAsync(OrderAuditEntry.Create("order-100", "submitted", "api"));
await log.AppendAsync(OrderAuditEntry.Create("order-100", "approved", "risk"));

var entries = await log.QueryAsync(entry => entry.OrderId == "order-100");
```

Appends reject duplicate entry keys and preserve insertion order for query results. Applications can replace the in-memory implementation with durable storage while keeping the same `IAuditLog<TEntry,TKey>` dependency boundary.

Use the source-generated path when the audit entry type and key selector are stable. Register `IAuditLog<TEntry,TKey>` as scoped when the log composes with request-scoped transaction, tenant, or user services.

See also:

- [Audit Log generator](../../generators/audit-log.md)
- [Order Audit Log example](../../examples/order-audit-log-pattern.md)
