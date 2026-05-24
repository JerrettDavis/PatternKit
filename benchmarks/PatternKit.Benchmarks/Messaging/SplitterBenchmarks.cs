using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "Splitter")]
public class SplitterBenchmarks
{
    private static readonly Message<RoutedOrder> Order = Message<RoutedOrder>
        .Create(new RoutedOrder("order-42", "enterprise", [
            new RoutedLine("sku-1", 30m),
            new RoutedLine("sku-2", 70m)
        ]))
        .WithMessageId("msg-order-42")
        .WithCorrelationId("order-42");

    [Benchmark(Baseline = true, Description = "Fluent: create splitter")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Splitter<RoutedOrder, RoutedLine> Fluent_CreateSplitter()
        => Splitter<RoutedOrder, RoutedLine>.Create()
            .Use(static (message, _) => message.Payload.Lines)
            .Build();

    [Benchmark(Description = "Generated: create splitter")]
    [BenchmarkCategory("Generated", "Construction")]
    public Splitter<RoutedOrder, RoutedLine> Generated_CreateSplitter()
        => GeneratedOrderLineSplitter.CreateLineSplitter();

    [Benchmark(Description = "Fluent: split order lines")]
    [BenchmarkCategory("Fluent", "Execution")]
    public IReadOnlyList<Message<RoutedLine>> Fluent_SplitOrderLines()
        => Fluent_CreateSplitter().Split(Order);

    [Benchmark(Description = "Generated: split order lines")]
    [BenchmarkCategory("Generated", "Execution")]
    public IReadOnlyList<Message<RoutedLine>> Generated_SplitOrderLines()
        => GeneratedOrderLineSplitter.CreateLineSplitter().Split(Order);
}
