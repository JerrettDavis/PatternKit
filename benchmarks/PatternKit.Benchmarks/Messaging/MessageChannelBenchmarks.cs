using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Channels;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageChannel")]
public class MessageChannelBenchmarks
{
    private static readonly InventoryAdjustment Adjustment = new("SKU-100", 12, "cycle-count");

    [Benchmark(Baseline = true, Description = "Fluent: create message channel")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessageChannel<InventoryAdjustment> Fluent_CreateMessageChannel()
        => InventoryMessageChannels.Create();

    [Benchmark(Description = "Generated: create message channel")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessageChannel<InventoryAdjustment> Generated_CreateMessageChannel()
        => GeneratedInventoryMessageChannel.Create();

    [Benchmark(Description = "Fluent: enqueue and process inventory adjustment")]
    [BenchmarkCategory("Fluent", "Execution")]
    public InventoryChannelSummary Fluent_EnqueueAndProcessInventoryAdjustment()
        => RunWith(InventoryMessageChannels.Create());

    [Benchmark(Description = "Generated: enqueue and process inventory adjustment")]
    [BenchmarkCategory("Generated", "Execution")]
    public InventoryChannelSummary Generated_EnqueueAndProcessInventoryAdjustment()
        => RunWith(GeneratedInventoryMessageChannel.Create());

    private static InventoryChannelSummary RunWith(MessageChannel<InventoryAdjustment> channel)
    {
        var service = new InventoryMessageChannelService(channel);
        service.Enqueue(Adjustment);
        return service.TryProcessNext();
    }
}
