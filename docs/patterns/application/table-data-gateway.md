# Table Data Gateway

Table Data Gateway models row-level access to a single table or table-like persistence boundary. Use it when an application needs simple row CRUD and query operations without mapping rows into richer domain objects.

PatternKit provides `ITableDataGateway<TRow,TKey>` and `InMemoryTableDataGateway<TRow,TKey>` in `PatternKit.Application.TableDataGateway`.

```csharp
var gateway = InMemoryTableDataGateway<OrderTableRow, string>
    .Create("orders", row => row.OrderId)
    .Build();

await gateway.InsertAsync(new OrderTableRow("order-100", "customer-1", "Pending", 125m));
await gateway.UpdateAsync(new OrderTableRow("order-100", "customer-1", "Closed", 125m));
var closed = await gateway.QueryAsync(row => row.Status == "Closed");
```

The gateway returns `TableGatewayResult<TRow>` for mutations so callers can distinguish inserted, updated, deleted, conflict, and missing rows.

Use the source-generated path when the table row type and key selector are stable. Register `ITableDataGateway<TRow,TKey>` as scoped when the gateway is composed with request-scoped storage, transaction, or tenant services.

See also:

- [Table Data Gateway generator](../../generators/table-data-gateway.md)
- [Order Table Data Gateway example](../../examples/order-table-data-gateway-pattern.md)
