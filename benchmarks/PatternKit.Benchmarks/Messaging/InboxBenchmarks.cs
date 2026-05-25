using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("MessagingReliability", "Messaging", "Inbox")]
public class InboxBenchmarks
{
    private static readonly Message<AcceptOrder> Command = Message<AcceptOrder>
        .Create(new AcceptOrder("order-42"))
        .WithIdempotencyKey("accept-order-42");

    [Benchmark(Baseline = true, Description = "Fluent: create inbox")]
    [BenchmarkCategory("Fluent", "Construction")]
    public InboxProcessor<AcceptOrder, string> Fluent_CreateInbox()
        => InboxProcessor<AcceptOrder, string>.Create(
            IdempotentReceiver<AcceptOrder, string>.Create(
                    new InMemoryIdempotencyStore(),
                    static (message, _, _) => new ValueTask<string>(message.Payload.OrderId))
                .OnDuplicate(DuplicateMessagePolicy.ReplayCompleted)
                .Build());

    [Benchmark(Description = "Generated: create inbox")]
    [BenchmarkCategory("Generated", "Construction")]
    public InboxProcessor<AcceptOrder, string> Generated_CreateInbox()
        => GeneratedReliabilityOrderPipeline.CreateInbox(new InMemoryIdempotencyStore());

    [Benchmark(Description = "Fluent: process inbox command")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<IdempotentReceiverResult<string>> Fluent_ProcessCommand()
        => Fluent_CreateInbox().ProcessAsync(Command);

    [Benchmark(Description = "Generated: process inbox command")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<IdempotentReceiverResult<string>> Generated_ProcessCommand()
        => GeneratedReliabilityOrderPipeline.CreateInbox(new InMemoryIdempotencyStore()).ProcessAsync(Command);
}
