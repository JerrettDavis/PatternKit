using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates a generated saga/process manager over order messages.
/// </summary>
public static class SagaExample
{
    /// <summary>Runs a small order saga.</summary>
    public static SagaSummary Run()
    {
        var saga = OrderSaga.Create();
        var started = saga.Handle(OrderSagaState.Empty, Message<OrderSubmitted>.Create(new OrderSubmitted("order-42")));
        var paid = saga.Handle(started.State, Message<OrderPaid>.Create(new OrderPaid("order-42")));

        return new SagaSummary(paid.State.OrderId!, paid.State.Submitted, paid.State.Paid, paid.Completed);
    }
}

/// <summary>Example order saga state.</summary>
public sealed record OrderSagaState(string? OrderId, bool Submitted, bool Paid)
{
    /// <summary>Initial example state.</summary>
    public static OrderSagaState Empty { get; } = new(null, Submitted: false, Paid: false);
}

/// <summary>Example order-submitted message.</summary>
public sealed record OrderSubmitted(string OrderId);

/// <summary>Example order-paid message.</summary>
public sealed record OrderPaid(string OrderId);

/// <summary>Example saga output.</summary>
public sealed record SagaSummary(string OrderId, bool Submitted, bool Paid, bool Completed);

/// <summary>Generated example order saga.</summary>
[GenerateSaga(typeof(OrderSagaState))]
public static partial class OrderSaga
{
    [SagaStep(typeof(OrderSubmitted), 10)]
    private static OrderSagaState Submit(OrderSagaState state, Message<OrderSubmitted> message, MessageContext context)
        => state with { OrderId = message.Payload.OrderId, Submitted = true };

    [SagaStep(typeof(OrderPaid), 20)]
    private static OrderSagaState Pay(OrderSagaState state, Message<OrderPaid> message, MessageContext context)
        => state.OrderId == message.Payload.OrderId ? state with { Paid = true } : state;

    [SagaCompleteWhen]
    private static bool IsComplete(OrderSagaState state) => state.Submitted && state.Paid;
}
