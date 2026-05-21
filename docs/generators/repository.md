# Repository Generator

`[GenerateRepository]` emits a factory for `InMemoryRepository<TEntity,TKey>` from a partial host and a key selector method.

```csharp
using PatternKit.Generators.Repository;

[GenerateRepository(typeof(OrderRecord), typeof(string), FactoryName = "CreateRepository")]
public static partial class GeneratedOrderRepository
{
    [RepositoryKeySelector]
    private static string SelectKey(OrderRecord order) => order.OrderId;
}
```

## Diagnostics

| ID | Meaning |
| --- | --- |
| `PKREP001` | The host type marked with `[GenerateRepository]` must be partial. |
| `PKREP002` | The host must declare exactly one `[RepositoryKeySelector]` method. |
| `PKREP003` | The key selector must be static and return `TKey` from one `TEntity` parameter. |

## Example

See [Order Repository Pattern](../examples/order-repository-pattern.md).
