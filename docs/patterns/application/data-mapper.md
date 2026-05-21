# Data Mapper

Data Mapper keeps domain objects independent from persistence or transport records. Use it when a database row, API DTO, or integration shape should not leak into your domain model.

## Fluent Path

```csharp
var mapper = DataMapper<OrderAggregate, OrderRow>.Create()
    .MapToData(order => new OrderRow(order.OrderId, order.CustomerId, order.Total, "PAID"))
    .MapToDomain(row => new OrderAggregate(row.OrderId, row.BuyerId, row.TotalAmount, OrderState.Paid))
    .ValidateDomain(order => string.IsNullOrWhiteSpace(order.OrderId)
        ? new DataMapperError("order-id-required", "Order id is required.")
        : null)
    .Build();
```

The mapper returns `DataMapperResult<T>` so callers can distinguish successful mappings from validation failures without throwing for business validation.

## Integration Notes

- Register `IDataMapper<TDomain,TData>` in `IServiceCollection` at the scope that matches the persistence boundary.
- Keep mapping rules deterministic and side-effect free; use repositories or gateways after mapping completes.
- Add validation hooks for invariants that must be true before crossing the domain/data boundary.

See [Order Data Mapper Pattern](../../examples/order-data-mapper-pattern.md) for a repository-backed example.
