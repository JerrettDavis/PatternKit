using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Storage;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageStore")]
public class MessageStoreBenchmarks
{
    private static readonly OrderMessageStoreEvent Event = new("O-100", "accepted", 149.95m, false);

    [Benchmark(Baseline = true, Description = "Fluent: create message store")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessageStore<OrderMessageStoreEvent> Fluent_CreateMessageStore()
        => OrderMessageStores.CreateAuditStore();

    [Benchmark(Description = "Generated: create message store")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessageStore<OrderMessageStoreEvent> Generated_CreateMessageStore()
        => GeneratedOrderMessageStore.Create();

    [Benchmark(Description = "Fluent: record and replay order event")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderMessageStoreSummary Fluent_RecordAndReplayOrderEvent()
        => OrderMessageStoreExampleRunner.RunFluent(Event, "msg-100", "corr-100");

    [Benchmark(Description = "Generated: record and replay order event")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderMessageStoreSummary Generated_RecordAndReplayOrderEvent()
        => new OrderMessageStoreService(GeneratedOrderMessageStore.Create()).Record(Event, "msg-100", "corr-100");
}
