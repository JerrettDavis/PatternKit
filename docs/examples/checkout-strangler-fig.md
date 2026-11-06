# Checkout Strangler Fig Migration

The checkout Strangler Fig example routes migrated tenants and pilot orders to a modern checkout service while all other traffic falls back to the legacy system.

```csharp
services.AddCheckoutStranglerFigDemo();

var runner = provider.GetRequiredService<CheckoutStranglerFigDemoRunner>();
var result = runner.RunGenerated(new CheckoutMigrationRequest("enterprise-west", "O-100", 100m));
```

The example includes fluent and source-generated construction, an `IServiceCollection` extension, and an ASP.NET Core minimal API mapping through `MapCheckoutStranglerFig()`.
