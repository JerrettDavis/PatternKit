using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ReliabilityPipeline")]
public class ReliabilityPipelineBenchmarks
{
    private static readonly Message<AcceptOrder> Command = Message<AcceptOrder>
        .Create(new AcceptOrder("order-42"))
        .WithIdempotencyKey("accept-order-42");

    [Benchmark(Baseline = true, Description = "Fluent: create receiver and outbox")]
    [BenchmarkCategory("Fluent", "Construction")]
    public object Fluent_CreateReceiverAndOutbox()
    {
        var store = new InMemoryIdempotencyStore();
        var outbox = new InMemoryOutbox<ReliabilityOrderAccepted>();
        var receiver = IdempotentReceiver<AcceptOrder, string>.Create(
                store,
                async (message, _, cancellationToken) =>
                {
                    await outbox.EnqueueAsync(
                        Message<ReliabilityOrderAccepted>.Create(new ReliabilityOrderAccepted(message.Payload.OrderId)),
                        id: $"accepted-{message.Payload.OrderId}",
                        cancellationToken: cancellationToken);
                    return message.Payload.OrderId;
                })
            .OnDuplicate(DuplicateMessagePolicy.ReplayCompleted)
            .Build();

        return new FluentReliabilityFlow(receiver, outbox);
    }

    [Benchmark(Description = "Generated: create inbox and outbox")]
    [BenchmarkCategory("Generated", "Construction")]
    public object Generated_CreateInboxAndOutbox()
    {
        var store = new InMemoryIdempotencyStore();
        return new GeneratedReliabilityFlow(
            GeneratedReliabilityOrderPipeline.CreateInbox(store),
            GeneratedReliabilityOrderPipeline.CreateOutbox());
    }

    [Benchmark(Description = "Fluent: duplicate inbox then dispatch")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ValueTask<IReadOnlyList<string>> Fluent_ProcessDuplicateThenDispatch()
        => ReliabilityExample.RunFluentAsync();

    [Benchmark(Description = "Generated: duplicate inbox then dispatch")]
    [BenchmarkCategory("Generated", "Execution")]
    public ValueTask<IReadOnlyList<string>> Generated_ProcessDuplicateThenDispatch()
        => ReliabilityExample.RunGeneratedAsync();

    private sealed record FluentReliabilityFlow(
        IdempotentReceiver<AcceptOrder, string> Receiver,
        InMemoryOutbox<ReliabilityOrderAccepted> Outbox);

    private sealed record GeneratedReliabilityFlow(
        InboxProcessor<AcceptOrder, string> Inbox,
        InMemoryOutbox<ReliabilityOrderAccepted> Outbox);
}
