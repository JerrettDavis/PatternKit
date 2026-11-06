using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Sagas;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "SagaProcessManager")]
public class SagaProcessManagerBenchmarks
{
    private static readonly Message<OrderSubmitted> Submitted = Message<OrderSubmitted>.Create(new("order-42"));
    private static readonly Message<OrderPaid> Paid = Message<OrderPaid>.Create(new("order-42"));

    [Benchmark(Baseline = true, Description = "Fluent: create saga")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Saga<OrderSagaState> Fluent_CreateSaga()
        => Saga<OrderSagaState>.Create()
            .On<OrderSubmitted>()
            .Then(static (state, message, _) => state with { OrderId = message.Payload.OrderId, Submitted = true })
            .On<OrderPaid>()
            .Then(static (state, message, _) => state.OrderId == message.Payload.OrderId ? state with { Paid = true } : state)
            .CompleteWhen(static state => state.Submitted && state.Paid)
            .Build();

    [Benchmark(Description = "Generated: create saga")]
    [BenchmarkCategory("Generated", "Construction")]
    public Saga<OrderSagaState> Generated_CreateSaga()
        => OrderSaga.Create();

    [Benchmark(Description = "Fluent: complete order saga")]
    [BenchmarkCategory("Fluent", "Execution")]
    public SagaSummary Fluent_CompleteOrderSaga()
    {
        var saga = Fluent_CreateSaga();
        var started = saga.Handle(OrderSagaState.Empty, Submitted);
        var paid = saga.Handle(started.State, Paid);
        return new(paid.State.OrderId!, paid.State.Submitted, paid.State.Paid, paid.Completed);
    }

    [Benchmark(Description = "Generated: complete order saga")]
    [BenchmarkCategory("Generated", "Execution")]
    public SagaSummary Generated_CompleteOrderSaga()
        => SagaExample.Run();
}
