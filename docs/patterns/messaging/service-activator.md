# Service Activator

Service Activator invokes an application service operation from a typed message and returns a typed response message.

```csharp
var activator = ServiceActivator<InventoryReservationRequest, InventoryReservationResult>
    .Create("inventory-reservation-activator")
    .Handle((message, context) => Message<InventoryReservationResult>.Create(result))
    .Build();

var response = activator.Activate(Message<InventoryReservationRequest>.Create(request));
```

Use it when a message endpoint should hand work to a domain or application service while preserving message context and typed request/response contracts. The activator keeps handler validation explicit, making it suitable for container-owned services in Generic Host or ASP.NET Core applications.

The source-generated path uses `[GenerateServiceActivator]` and `[ServiceActivatorHandler]`. Import the inventory example through `AddInventoryServiceActivatorDemo()` or `AddPatternKitExamples()`.
