using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageRouting")]
public class MessageRoutingBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create routing flow")]
    [BenchmarkCategory("Fluent", "Construction")]
    public object Fluent_CreateRoutingFlow()
    {
        var splitter = Splitter<RoutedOrder, RoutedLine>.Create()
            .Use(static (message, _) => message.Payload.Lines)
            .Build();

        var aggregator = Aggregator<string, RoutedLine, decimal>.Create()
            .KeyBy(static (message, _) => message.Headers.CorrelationId ?? message.Payload.Sku)
            .CompleteWhen(static (_, messages, _) => messages.Count == 2)
            .Project(static (_, messages, _) => messages.Sum(message => message.Payload.Amount))
            .Build();

        return new RoutingFlow(splitter, aggregator);
    }

    [Benchmark(Description = "Generated: create routing flow")]
    [BenchmarkCategory("Generated", "Construction")]
    public object Generated_CreateRoutingFlow()
        => new RoutingFlow(
            GeneratedOrderLineSplitter.CreateLineSplitter(),
            GeneratedOrderLineAggregator.CreateLineTotalAggregator());

    [Benchmark(Description = "Fluent: route split aggregate")]
    [BenchmarkCategory("Fluent", "Execution")]
    public RoutingSummary Fluent_RunRoutingFlow()
        => MessageRoutingExample.RunFluent();

    [Benchmark(Description = "Generated: route split aggregate")]
    [BenchmarkCategory("Generated", "Execution")]
    public RoutingSummary Generated_RunRoutingFlow()
        => MessageRoutingExample.RunGenerated();

    private sealed record RoutingFlow(
        Splitter<RoutedOrder, RoutedLine> Splitter,
        Aggregator<string, RoutedLine, decimal> Aggregator);
}
