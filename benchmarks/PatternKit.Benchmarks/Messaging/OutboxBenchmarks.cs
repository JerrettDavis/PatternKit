using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("MessagingReliability", "Messaging", "Outbox")]
public class OutboxBenchmarks
{
    private static readonly Message<ReliabilityOrderAccepted> Accepted = Message<ReliabilityOrderAccepted>
        .Create(new ReliabilityOrderAccepted("order-42"));

    private static readonly RecordingOutboxDispatcher Dispatcher = new();

    [Benchmark(Baseline = true, Description = "Fluent: create outbox")]
    [BenchmarkCategory("Fluent", "Construction")]
    public InMemoryOutbox<ReliabilityOrderAccepted> Fluent_CreateOutbox()
        => new();

    [Benchmark(Description = "Generated: create outbox")]
    [BenchmarkCategory("Generated", "Construction")]
    public InMemoryOutbox<ReliabilityOrderAccepted> Generated_CreateOutbox()
        => GeneratedReliabilityOrderPipeline.CreateOutbox();

    [Benchmark(Description = "Fluent: enqueue and dispatch outbox")]
    [BenchmarkCategory("Fluent", "Execution")]
    public async ValueTask<int> Fluent_EnqueueAndDispatch()
    {
        var outbox = new InMemoryOutbox<ReliabilityOrderAccepted>();
        await outbox.EnqueueAsync(Accepted, id: "accepted-order-42");
        return await outbox.DispatchPendingAsync(Dispatcher);
    }

    [Benchmark(Description = "Generated: enqueue and dispatch outbox")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<int> Generated_EnqueueAndDispatch()
    {
        var outbox = GeneratedReliabilityOrderPipeline.CreateOutbox();
        await outbox.EnqueueAsync(Accepted, id: "accepted-order-42");
        return await outbox.DispatchPendingAsync(Dispatcher);
    }

    private sealed class RecordingOutboxDispatcher : IOutboxDispatcher<ReliabilityOrderAccepted>
    {
        public ValueTask DispatchAsync(OutboxMessage<ReliabilityOrderAccepted> message, CancellationToken cancellationToken = default)
            => default;
    }
}
