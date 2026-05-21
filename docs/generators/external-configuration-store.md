# External Configuration Store Generator

`[GenerateExternalConfigurationStore]` creates a typed `ExternalConfigurationStore<TSettings>` factory from a static loader and ordered validators.

```csharp
[GenerateExternalConfigurationStore(typeof(TenantFeatureSettings), FactoryName = "Create")]
public static partial class GeneratedTenantConfigStore
{
    [ExternalConfigurationLoader]
    private static ValueTask<ExternalConfigurationSnapshot<TenantFeatureSettings>> Load(CancellationToken ct) { }

    [ExternalConfigurationValidator("Tenant id is required.", 10)]
    private static bool HasTenant(TenantFeatureSettings settings) => !string.IsNullOrWhiteSpace(settings.TenantId);
}
```

The loader must return `ValueTask<ExternalConfigurationSnapshot<TSettings>>` and accept a `CancellationToken`. Validators must be static `bool` methods accepting `TSettings`.
