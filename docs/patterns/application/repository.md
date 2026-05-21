# Repository

Repository gives application code a collection-like persistence boundary around domain entities. PatternKit's runtime repository is async-first, supports specification filtering, and has deterministic mutation results for duplicate adds and missing updates.

```csharp
var repository = InMemoryRepository<OrderRecord, string>
    .Create(order => order.OrderId)
    .UseComparer(StringComparer.OrdinalIgnoreCase)
    .Build();

await repository.AddAsync(order, ct);
var pending = await repository.FindAsync(PendingOrderSpecification, ct);
```

Use `IRepository<TEntity,TKey>` at application boundaries and keep durable persistence application-owned. `InMemoryRepository<TEntity,TKey>` is useful for tests, examples, and embedded adapters.

## Source Generator

`[GenerateRepository]` emits an in-memory repository factory from a static key selector:

```csharp
[GenerateRepository(typeof(OrderRecord), typeof(string), FactoryName = "CreateRepository")]
public static partial class GeneratedOrderRepository
{
    [RepositoryKeySelector]
    private static string SelectKey(OrderRecord order) => order.OrderId;
}
```

See [Repository Generator](../../generators/repository.md).

## Example

- `src/PatternKit.Examples/RepositoryDemo/OrderRepositoryDemo.cs`
- `test/PatternKit.Examples.Tests/RepositoryDemo/OrderRepositoryDemoTests.cs`
