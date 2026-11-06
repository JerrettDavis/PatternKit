using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("MessagingReliability", "Messaging", "IdempotentReceiver")]
public class IdempotentReceiverBenchmarks
{
    private static readonly Message<AcceptOrder> Command = Message<AcceptOrder>
        .Create(new AcceptOrder("order-42"))
        .WithIdempotencyKey("accept-order-42");

    [Benchmark(Baseline = true, Description = "Fluent: create idempotent receiver")]
    [BenchmarkCategory("Fluent", "Construction")]
    public IdempotentReceiver<AcceptOrder, string> Fluent_CreateReceiver()
        => IdempotentReceiver<AcceptOrder, string>.Create(
                new InMemoryIdempotencyStore(),
                static (message, _, _) => new ValueTask<string>(message.Payload.OrderId))
            .OnDuplicate(DuplicateMessagePolicy.ReplayCompleted)
            .Build();

    [Benchmark(Description = "Generated: create idempotent receiver")]
    [BenchmarkCategory("Generated", "Construction")]
    public IdempotentReceiver<AcceptOrder, string> Generated_CreateReceiver()
        => GeneratedReliabilityOrderPipeline.CreateOrderReceiver(new InMemoryIdempotencyStore());

    [Benchmark(Description = "Fluent: handle idempotent command")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<IdempotentReceiverResult<string>> Fluent_HandleCommand()
        => Fluent_CreateReceiver().HandleAsync(Command);

    [Benchmark(Description = "Generated: handle idempotent command")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<IdempotentReceiverResult<string>> Generated_HandleCommand()
        => GeneratedReliabilityOrderPipeline.CreateOrderReceiver(new InMemoryIdempotencyStore()).HandleAsync(Command);
}
