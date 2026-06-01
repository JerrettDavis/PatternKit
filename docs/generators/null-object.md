# Null Object Generator

`[GenerateNullObject]` generates a sealed Null Object implementation for an interface contract.

```csharp
[GenerateNullObject(TypeName = "NullInventoryNotifier")]
public interface IInventoryNotifier
{
    [NullObjectDefault(false)]
    bool CanDeliver { get; }

    [NullObjectDefault("suppressed")]
    string Notify(string sku);
}
```

Generated output includes:

- a sealed implementation of the contract
- a static `Instance` property
- no-op `void` methods
- deterministic defaults for strings, booleans, numeric values, arrays, `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>`
- per-member defaults through `[NullObjectDefault]`

Use the generated implementation directly or wrap it with `NullObject<TContract>` for consistent dependency injection registration.
