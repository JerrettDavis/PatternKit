# Lazy Load Generator

`LazyLoadGenerator` emits a typed `LazyLoad<TValue>` factory from a loader method.

```csharp
[GenerateLazyLoad(
    typeof(CustomerProfile),
    FactoryMethodName = "CreateProfile",
    LoaderMethodName = "LoadProfileAsync",
    LazyLoadName = "customer-profile",
    TimeToLiveMilliseconds = 300000)]
public static partial class CustomerProfileLazyLoad
{
    public static ValueTask<CustomerProfile> LoadProfileAsync(CancellationToken cancellationToken)
        => store.LoadAsync(customerId, cancellationToken);
}
```

## Diagnostics

| ID | Severity | Message |
| --- | --- | --- |
| `PKLL001` | Error | The host type must be partial. |
| `PKLL002` | Error | Time-to-live values must be non-negative. |
| `PKLL003` | Error | Factory and loader method names must be valid C# identifiers. |

Register generated lazy loaders with normal .NET hosts by calling the generated factory from the service registration:

```csharp
services.AddSingleton(_ => CustomerProfileLazyLoad.CreateProfile());
```
