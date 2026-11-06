using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Diagnostics;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageHistory")]
public class MessageHistoryBenchmarks
{
    private static readonly HistoryOrder Order = new("O-100", 125m, "web");

    [Benchmark(Baseline = true, Description = "Fluent: create message history")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessageHistory<HistoryOrder> Fluent_CreateMessageHistory()
        => OrderMessageHistories.CreateReceived();

    [Benchmark(Description = "Generated: create message history")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessageHistory<HistoryOrder> Generated_CreateMessageHistory()
        => GeneratedOrderReceivedHistory.Create()
            .Details(static message => message.Payload.Channel)
            .Build();

    [Benchmark(Description = "Fluent: record order message history")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderMessageHistorySummary Fluent_RecordOrderHistory()
        => OrderMessageHistoryExampleRunner.RunFluent(Order);

    [Benchmark(Description = "Generated: record order message history")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderMessageHistorySummary Generated_RecordOrderHistory()
        => OrderMessageHistoryExampleRunner.RunGeneratedStatic(Order);
}
