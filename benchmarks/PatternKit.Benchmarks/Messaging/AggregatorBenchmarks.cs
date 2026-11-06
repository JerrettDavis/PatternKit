using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "Aggregator")]
public class AggregatorBenchmarks
{
    private static readonly Message<RoutedLine> FirstLine = Message<RoutedLine>
        .Create(new RoutedLine("sku-1", 30m))
        .WithMessageId("line:sku-1")
        .WithCorrelationId("order-42");

    private static readonly Message<RoutedLine> SecondLine = Message<RoutedLine>
        .Create(new RoutedLine("sku-2", 70m))
        .WithMessageId("line:sku-2")
        .WithCorrelationId("order-42");

    [Benchmark(Baseline = true, Description = "Fluent: create aggregator")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Aggregator<string, RoutedLine, decimal> Fluent_CreateAggregator()
        => Aggregator<string, RoutedLine, decimal>.Create()
            .KeyBy(static (message, _) => message.Headers.CorrelationId ?? message.Payload.Sku)
            .CompleteWhen(static (_, messages, _) => messages.Count == 2)
            .Project(static (_, messages, _) => messages.Sum(message => message.Payload.Amount))
            .Build();

    [Benchmark(Description = "Generated: create aggregator")]
    [BenchmarkCategory("Generated", "Construction")]
    public Aggregator<string, RoutedLine, decimal> Generated_CreateAggregator()
        => GeneratedOrderLineAggregator.CreateLineTotalAggregator();

    [Benchmark(Description = "Fluent: aggregate order lines")]
    [BenchmarkCategory("Fluent", "Execution")]
    public AggregationResult<string, decimal> Fluent_AggregateOrderLines()
    {
        var aggregator = Fluent_CreateAggregator();
        aggregator.Add(FirstLine);
        return aggregator.Add(SecondLine);
    }

    [Benchmark(Description = "Generated: aggregate order lines")]
    [BenchmarkCategory("Generated", "Execution")]
    public AggregationResult<string, decimal> Generated_AggregateOrderLines()
    {
        var aggregator = GeneratedOrderLineAggregator.CreateLineTotalAggregator();
        aggregator.Add(FirstLine);
        return aggregator.Add(SecondLine);
    }
}
