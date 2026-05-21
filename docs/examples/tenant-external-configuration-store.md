# Tenant External Configuration Store

The tenant external-configuration-store example loads feature settings from a central provider before application workflows use them. It demonstrates:

- a fluent `ExternalConfigurationStore<TenantFeatureSettings>`
- a `[GenerateExternalConfigurationStore]` source-generated factory
- typed validation and cache duration
- `IServiceCollection` registration through `AddTenantExternalConfigurationStoreDemo()`

The example is implemented in `src/PatternKit.Examples/ExternalConfigurationStoreDemo/TenantExternalConfigurationStoreDemo.cs` and covered by TinyBDD tests in `test/PatternKit.Examples.Tests/ExternalConfigurationStoreDemo/TenantExternalConfigurationStoreDemoTests.cs`.
