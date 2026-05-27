# Order Message Expiration

The order message-expiration example shows how to stamp fulfillment commands with a deadline and reject stale commands before processing.

```csharp
var services = new ServiceCollection()
    .AddOrderMessageExpirationDemo()
    .BuildServiceProvider();

var service = services.GetRequiredService<OrderMessageExpirationService>();
var message = service.Accept(new ExpiringOrderCommand("o-1", "c-1"));
var summary = service.Evaluate(message);
```

The example exposes both paths:

- `OrderMessageExpirations.Create(...)` for fluent construction and deterministic tests.
- `GeneratedOrderMessageExpiration.Create()` for source-generated, contract-style configuration.

The DI extension registers the generated policy, the service, and an example runner so the same shape can be imported into `IServiceCollection` in a worker, ASP.NET Core app, or test host.
