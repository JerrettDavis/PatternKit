# External Configuration Store

Use `ExternalConfigurationStore<TSettings>` when application configuration is centralized in a service such as Azure App Configuration, AWS AppConfig, Consul, Vault, or a tenant settings API. PatternKit keeps the loader, validation rules, and cache policy together so applications import one typed store through DI.

```csharp
var store = ExternalConfigurationStore<TenantFeatureSettings>.Create("tenant-feature-config")
    .LoadFrom(provider.LoadAsync)
    .ValidateWith("Tenant id is required.", settings => !string.IsNullOrWhiteSpace(settings.TenantId))
    .CacheFor(TimeSpan.FromMinutes(5))
    .Build();
```

The source-generated path uses `[GenerateExternalConfigurationStore]`, one `[ExternalConfigurationLoader]`, and optional `[ExternalConfigurationValidator]` methods. Import the example through `AddTenantExternalConfigurationStoreDemo()` or `AddPatternKitExamples()`.
