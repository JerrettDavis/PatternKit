using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Consumers;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "PollingConsumer")]
public class PollingConsumerBenchmarks
{
    private static readonly ReplenishmentRequest Request = new("SKU-100", 12);

    [Benchmark(Baseline = true, Description = "Fluent: create polling consumer")]
    [BenchmarkCategory("Fluent", "Construction")]
    public PollingConsumer<ReplenishmentRequest> Fluent_CreatePollingConsumer()
        => WarehousePollingConsumers.Create(CreateLoadedChannel());

    [Benchmark(Description = "Generated: create polling consumer")]
    [BenchmarkCategory("Generated", "Construction")]
    public PollingConsumer<ReplenishmentRequest> Generated_CreatePollingConsumer()
        => GeneratedWarehousePollingConsumer.Create();

    [Benchmark(Description = "Fluent: poll replenishment request")]
    [BenchmarkCategory("Fluent", "Execution")]
    public WarehousePollingSummary Fluent_PollReplenishmentRequest()
        => WarehousePollingConsumerExampleRunner.RunFluent(Request);

    [Benchmark(Description = "Generated: poll replenishment request")]
    [BenchmarkCategory("Generated", "Execution")]
    public WarehousePollingSummary Generated_PollReplenishmentRequest()
    {
        GeneratedWarehousePollingConsumer.Enqueue(Request);
        return new WarehousePollingConsumerService(GeneratedWarehousePollingConsumer.Create()).Poll();
    }

    private static MessageChannel<ReplenishmentRequest> CreateLoadedChannel()
    {
        var channel = MessageChannel<ReplenishmentRequest>.Create("warehouse-replenishment").Build();
        channel.Send(Message<ReplenishmentRequest>.Create(Request));
        return channel;
    }
}
