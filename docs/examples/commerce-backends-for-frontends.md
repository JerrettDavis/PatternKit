# Commerce Backends for Frontends

The commerce Backends for Frontends example shapes one customer summary workflow for web, mobile, and default clients.

```csharp
services.AddCommerceBackendsForFrontendsDemo();

var runner = provider.GetRequiredService<CommerceBackendsForFrontendsDemoRunner>();
var summary = runner.RunGenerated("mobile", "C-100");
```

The example includes fluent and source-generated construction, an `IServiceCollection` extension, and an ASP.NET Core minimal API mapping through `MapCommerceBackendsForFrontends()`.
