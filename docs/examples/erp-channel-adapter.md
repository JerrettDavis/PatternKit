# ERP Channel Adapter

The ERP channel adapter example translates partner ERP order documents into internal PatternKit messages and back out to ERP documents.

```csharp
services.AddErpChannelAdapterDemo();

var service = provider.GetRequiredService<ErpChannelAdapterService>();
var summary = service.RoundTrip(new ErpOrderDocument("ERP-100", "42.50"));
```

The example includes fluent and source-generated construction, inbound and outbound message channels, and `IServiceCollection` registration for existing .NET applications.
