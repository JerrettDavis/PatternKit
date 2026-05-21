# Data Mapper Generator

`DataMapperGenerator` emits a fluent `DataMapper<TDomain,TData>` factory from two attributed projection methods.

```csharp
[GenerateDataMapper(typeof(OrderAggregate), typeof(OrderRow), FactoryName = "CreateMapper")]
public static partial class GeneratedOrderDataMapper
{
    [DataMapperToData]
    private static OrderRow ToData(OrderAggregate order)
        => new(order.OrderId, order.CustomerId, order.Total, "PAID");

    [DataMapperToDomain]
    private static OrderAggregate ToDomain(OrderRow row)
        => new(row.OrderId, row.BuyerId, row.TotalAmount, OrderState.Paid);
}
```

The generated method returns a normal runtime mapper:

```csharp
DataMapper<OrderAggregate, OrderRow> mapper = GeneratedOrderDataMapper.CreateMapper();
```

## Diagnostics

| ID | Severity | Message |
| --- | --- | --- |
| `PKMAP001` | Error | The host type must be partial. |
| `PKMAP002` | Error | Exactly one `[DataMapperToData]` and one `[DataMapperToDomain]` method are required. |
| `PKMAP003` | Error | Projection methods must be static, non-generic, return the target type, and accept one source parameter. |

## DI Usage

```csharp
services.AddSingleton<IDataMapper<OrderAggregate, OrderRow>>(_ =>
    GeneratedOrderDataMapper.CreateMapper());
```

See [Order Data Mapper Pattern](../examples/order-data-mapper-pattern.md) for the full importable example.
