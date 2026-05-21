# Control Bus Generator

`[GenerateControlBus]` creates a typed `ControlBus<TCommand>` factory for a partial class or struct.

```csharp
[GenerateControlBus(typeof(FulfillmentControlCommand), FactoryName = "Create", BusName = "fulfillment-control")]
public static partial class FulfillmentControlBus
{
    [ControlBusCommand("pause", "pause-processor", 10)]
    private static ControlBusResult<FulfillmentControlCommand> Pause(
        Message<FulfillmentControlCommand> message,
        MessageContext context)
        => ControlBusResult<FulfillmentControlCommand>.Success();
}
```

The generated factory composes the fluent runtime API:

- `ControlBus<TCommand>.Create(BusName)`
- one `.Handle(commandName, handlerName, handler)` call per `[ControlBusCommand]`
- `.Build()`

Handlers must be static and return `ControlBusResult<TCommand>` with parameters `Message<TCommand>` and `MessageContext`.

Diagnostics:

- `PKCTL001`: host type must be partial.
- `PKCTL002`: at least one command handler is required.
- `PKCTL003`: command handler signature is invalid.
- `PKCTL004`: command names and orders must be unique.
