using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "MessageFilter")]
public class MessageFilterBenchmarks
{
    private static readonly OrderMessageFilterCommand Command = new("order-100", "trusted", 149.95m, true);

    [Benchmark(Baseline = true, Description = "Fluent: create message filter")]
    [BenchmarkCategory("Fluent", "Construction")]
    public MessageFilter<OrderMessageFilterCommand> Fluent_CreateMessageFilter()
        => OrderMessageFilters.CreateFraudScreen();

    [Benchmark(Description = "Generated: create message filter")]
    [BenchmarkCategory("Generated", "Construction")]
    public MessageFilter<OrderMessageFilterCommand> Generated_CreateMessageFilter()
        => GeneratedOrderMessageFilter.Create();

    [Benchmark(Description = "Fluent: screen order command")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderMessageFilterSummary Fluent_ScreenOrderCommand()
        => OrderMessageFilterExampleRunner.RunFluent(Command);

    [Benchmark(Description = "Generated: screen order command")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderMessageFilterSummary Generated_ScreenOrderCommand()
        => new OrderMessageFilterService(GeneratedOrderMessageFilter.Create()).Screen(Command);
}
