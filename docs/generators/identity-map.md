# Identity Map Generator

`IdentityMapGenerator` emits a typed `IdentityMap<TEntity,TKey>` factory from a key selector.

```csharp
[GenerateIdentityMap(typeof(TrackedOrder), typeof(string), FactoryName = "CreateMap")]
public static partial class GeneratedOrderIdentityMap
{
    [IdentityMapKeySelector]
    private static string SelectKey(TrackedOrder order) => order.OrderId;
}
```

## Diagnostics

| ID | Severity | Message |
| --- | --- | --- |
| `PKIM001` | Error | The host type must be partial. |
| `PKIM002` | Error | Exactly one `[IdentityMapKeySelector]` method is required. |
| `PKIM003` | Error | The key selector must be static, non-generic, return `TKey`, and accept one `TEntity`. |

Register generated maps as scoped services when used with normal .NET hosts:

```csharp
services.AddScoped<IIdentityMap<TrackedOrder, string>>(_ =>
    GeneratedOrderIdentityMap.CreateMap());
```
