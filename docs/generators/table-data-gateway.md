# Table Data Gateway Generator

`GenerateTableDataGatewayAttribute` creates a typed `InMemoryTableDataGateway<TRow,TKey>` factory from a key selector method.

```csharp
[GenerateTableDataGateway(typeof(OrderTableRow), typeof(string), FactoryName = "CreateGateway", TableName = "orders")]
public static partial class GeneratedOrderTableGateway
{
    [TableGatewayKeySelector]
    private static string SelectKey(OrderTableRow row) => row.OrderId;
}
```

The generated factory is equivalent to:

```csharp
InMemoryTableDataGateway<OrderTableRow, string>
    .Create("orders", SelectKey)
    .Build();
```

Diagnostics:

- `PKTDG001`: host type must be partial.
- `PKTDG002`: exactly one `[TableGatewayKeySelector]` method is required.
- `PKTDG003`: key selector must be static and return `TKey` from one `TRow` parameter.
