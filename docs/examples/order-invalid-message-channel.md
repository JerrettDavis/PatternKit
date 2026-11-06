# Order Invalid Message Channel

The order invalid message channel example demonstrates a production import boundary that accepts valid order imports and routes invalid payloads to an inspectable channel.

The example includes:

- A fluent `InvalidMessageChannel<OrderImportCommand>` with SKU and quantity validation.
- A source-generated builder factory via `[GenerateInvalidMessageChannel]`.
- `AddOrderInvalidMessageChannelDemo()` for standard `IServiceCollection` integration.
- TinyBDD coverage for fluent and generated/container-backed paths.

```csharp
services.AddOrderInvalidMessageChannelDemo();

var runner = provider.GetRequiredService<OrderInvalidMessageChannelExampleRunner>();
var summary = runner.RunGenerated(commands);
```
