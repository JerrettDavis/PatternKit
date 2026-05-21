# Control Bus

Control Bus routes operational messages to named command handlers so an application can pause, resume, drain, or inspect message processors without mixing those controls into business message handlers.

`ControlBus<TCommand>` provides a fluent runtime path:

```csharp
var state = new FulfillmentProcessorControlState();

var bus = ControlBus<FulfillmentControlCommand>.Create("fulfillment-control")
    .Handle("pause", "pause-processor", (message, context) =>
    {
        state.Pause();
        return ControlBusResult<FulfillmentControlCommand>.Success();
    })
    .Handle("drain", "drain-processor", (message, context) =>
    {
        state.Drain();
        return ControlBusResult<FulfillmentControlCommand>.Success();
    })
    .Build();

var result = bus.Dispatch(
    Message<FulfillmentControlCommand>.Create(new("pause", "processor-1"))
        .WithHeader(ControlBusHeaders.CommandName, "pause"));
```

Use it when a worker, hosted service, queue processor, or integration endpoint needs a typed operational command surface. The default selector reads the `control-command` message header, and `SelectCommand` can be used when commands are embedded in the payload.

The source-generated path uses `[GenerateControlBus]` and `[ControlBusCommand]` to create the same fluent factory from annotated static handlers. Import the production example through `AddFulfillmentControlBusDemo()` or the aggregate `AddPatternKitExamples()` registration.
