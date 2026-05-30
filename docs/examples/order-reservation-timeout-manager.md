# Order Reservation Timeout Manager

This example models an order reservation hold that expires if the customer does not complete checkout before the deadline.

The fluent path builds the manager directly:

```csharp
var manager = OrderReservationTimeoutManagers.CreateFluent();
```

The generated path uses a source-generated factory:

```csharp
var manager = GeneratedOrderReservationTimeoutManager.CreateGenerated();
```

The example is importable through standard dependency injection:

```csharp
services.AddOrderReservationTimeoutDemo();
```

`OrderReservationTimeoutDemoRunner` schedules a reservation timeout and expires due holds. Production applications can pair the expired order identifiers with a message channel, outbox, saga, or service layer operation to release inventory and notify customers.
