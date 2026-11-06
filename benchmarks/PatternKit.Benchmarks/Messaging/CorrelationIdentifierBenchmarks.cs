using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Correlation;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "CorrelationIdentifier")]
public class CorrelationIdentifierBenchmarks
{
    private static readonly CorrelatedOrder Order = new("O-100", "C-42", 125m);

    [Benchmark(Baseline = true, Description = "Fluent: create correlation identifier")]
    [BenchmarkCategory("Fluent", "Construction")]
    public CorrelationIdentifier<CorrelatedOrder> Fluent_CreateCorrelationIdentifier()
        => OrderCorrelationIdentifiers.Create().Build();

    [Benchmark(Description = "Generated: create correlation identifier")]
    [BenchmarkCategory("Generated", "Construction")]
    public CorrelationIdentifier<CorrelatedOrder> Generated_CreateCorrelationIdentifier()
        => GeneratedOrderCorrelation.Create()
            .Select(static (message, _) => "customer:" + message.Payload.CustomerId)
            .Build();

    [Benchmark(Description = "Fluent: correlate order flow")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderCorrelationSummary Fluent_CorrelateOrderFlow()
        => OrderCorrelationIdentifierExampleRunner.RunFluentStatic(Order);

    [Benchmark(Description = "Generated: correlate order flow")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderCorrelationSummary Generated_CorrelateOrderFlow()
        => OrderCorrelationIdentifierExampleRunner.RunGeneratedStatic(Order);
}
