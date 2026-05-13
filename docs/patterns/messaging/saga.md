# Saga / Process Manager

The Saga pattern coordinates a long-running business process by reacting to messages and advancing explicit state. PatternKit provides a small in-process saga runtime and a source generator for static, typed process-manager factories.

Use these APIs when the saga state is already loaded by your application and message handling happens inside the current process. Persist state, concurrency tokens, retries, and transport delivery outside PatternKit when the workflow must survive process restarts.

## Runtime API

```csharp
using PatternKit.Messaging;
using PatternKit.Messaging.Sagas;

var saga = Saga<OrderState>.Create()
    .On<OrderSubmitted>().Then((state, message, context) =>
        state with { OrderId = message.Payload.OrderId, Submitted = true })
    .On<OrderPaid>().Then((state, message, context) =>
        state with { Paid = true })
    .CompleteWhen(state => state.Submitted && state.Paid)
    .Build();

var result = saga.Handle(state, Message<OrderSubmitted>.Create(submitted));
```

`Saga<TState>` only runs steps whose message type matches the handled message. Use `When<TMessage>` for guarded transitions.

`AsyncSaga<TState>` supports asynchronous guards and handlers:

```csharp
var saga = AsyncSaga<OrderState>.Create()
    .On<OrderSubmitted>().Then((state, message, context, cancellationToken) =>
        new ValueTask<OrderState>(state with { Submitted = true }))
    .Build();
```

## Source Generator

Use `[GenerateSaga]` on a partial class or struct, then mark static step methods with `[SagaStep]`. Add `[SagaCompleteWhen]` to one static completion predicate when needed.

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateSaga(typeof(OrderState))]
public static partial class OrderSaga
{
    [SagaStep(typeof(OrderSubmitted), 10)]
    private static OrderState Submit(
        OrderState state,
        Message<OrderSubmitted> message,
        MessageContext context) => state with { Submitted = true };

    [SagaCompleteWhen]
    private static bool IsComplete(OrderState state) => state.Submitted;
}

var result = OrderSaga.Create().Handle(state, Message<OrderSubmitted>.Create(submitted));
```

Generated sync steps must be static and have this shape:

```csharp
TState Step(TState state, Message<TMessage> message, MessageContext context)
```

Generated async steps must be static and have this shape:

```csharp
ValueTask<TState> StepAsync(
    TState state,
    Message<TMessage> message,
    MessageContext context,
    CancellationToken cancellationToken)
```

The generator emits diagnostics for non-partial saga types, missing steps, invalid step signatures, and invalid completion predicates.

## API

- <xref:PatternKit.Messaging.Sagas.Saga`1>
- <xref:PatternKit.Messaging.Sagas.AsyncSaga`1>
- <xref:PatternKit.Messaging.Sagas.SagaResult`1>
- <xref:PatternKit.Generators.Messaging.GenerateSagaAttribute>
- <xref:PatternKit.Generators.Messaging.SagaStepAttribute>
- <xref:PatternKit.Generators.Messaging.SagaCompleteWhenAttribute>

## Example Source

- `src/PatternKit.Examples/Messaging/SagaExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/SagaExampleTests.cs`
