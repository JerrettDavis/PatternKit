# Routing Slip

The Routing Slip pattern carries an ordered itinerary with a message and executes each named step in sequence. PatternKit provides a runtime fluent API and a source generator for static, compile-time validated slip factories.

Use routing slips when the workflow is dynamic enough to be modeled as an itinerary, but still runs inside one process behind a queue consumer, API handler, background worker, or dispatcher handler.

These APIs do not persist workflow progress across process restarts and do not provide broker delivery guarantees. Use durable workflow infrastructure when a slip must survive process failure.

## Runtime API

```csharp
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

var slip = RoutingSlip<Order>.Create()
    .Step("validate", (message, context) => message)
    .Step("ship", (message, context) => message)
    .Build();

var result = slip.Execute(Message<Order>.Create(order));
```

`RoutingSlip<TPayload>` executes each step synchronously and returns a `RoutingSlipResult<TPayload>` with the final message and completed step names.

`AsyncRoutingSlip<TPayload>` provides the same itinerary model for asynchronous handlers:

```csharp
var slip = AsyncRoutingSlip<Order>.Create()
    .Step("validate", (message, context, cancellationToken) => new ValueTask<Message<Order>>(message))
    .Build();
```

## Headers

Routing slips write progress into message headers:

- `MessageHeaderNames.RoutingSlip` contains the itinerary names.
- `MessageHeaderNames.RoutingSlipIndex` contains the current step index during execution and the completed count after execution.
- `MessageHeaderNames.RoutingSlipCompleted` contains the completed step names after execution.

Handlers receive a `MessageContext` whose headers match the current message state before that step runs.

## Source Generator

Use `[GenerateRoutingSlip]` on a partial class or struct, then mark static step methods with `[RoutingSlipStep]`.

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateRoutingSlip(typeof(Order))]
public static partial class OrderSlip
{
    [RoutingSlipStep("validate", 10)]
    private static Message<Order> Validate(Message<Order> message, MessageContext context) => message;

    [RoutingSlipStep("ship", 20)]
    private static Message<Order> Ship(Message<Order> message, MessageContext context) => message;
}

var result = OrderSlip.Create().Execute(Message<Order>.Create(order));
```

Generated sync steps must be static and have this shape:

```csharp
Message<TPayload> Step(Message<TPayload> message, MessageContext context)
```

Generated async steps must be static and have this shape:

```csharp
ValueTask<Message<TPayload>> StepAsync(
    Message<TPayload> message,
    MessageContext context,
    CancellationToken cancellationToken)
```

The generator orders steps by `RoutingSlipStepAttribute.Order` and emits diagnostics for non-partial slip types, missing steps, and invalid step signatures.

## API

- <xref:PatternKit.Messaging.Routing.RoutingSlip`1>
- <xref:PatternKit.Messaging.Routing.AsyncRoutingSlip`1>
- <xref:PatternKit.Messaging.Routing.RoutingSlipResult`1>
- <xref:PatternKit.Messaging.MessageHeaderNames>
- <xref:PatternKit.Generators.Messaging.GenerateRoutingSlipAttribute>
- <xref:PatternKit.Generators.Messaging.RoutingSlipStepAttribute>

## Example Source

- `src/PatternKit.Examples/Messaging/RoutingSlipExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/RoutingSlipExampleTests.cs`
