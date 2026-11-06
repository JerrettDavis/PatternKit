using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "InvalidMessageChannel")]
public class InvalidMessageChannelBenchmarks
{
    private static readonly OrderImportCommand[] Commands =
    [
        new("ORD-1", "SKU-100", 2),
        new("ORD-2", "", 1),
        new("ORD-3", "SKU-300", 0)
    ];

    [Benchmark(Baseline = true, Description = "Fluent: create invalid message channel")]
    [BenchmarkCategory("Fluent", "Construction")]
    public InvalidMessageChannel<OrderImportCommand> Fluent_CreateInvalidMessageChannel()
        => OrderInvalidMessageChannels.Create(OrderInvalidMessageChannelExampleRunner.CreateInvalidChannel());

    [Benchmark(Description = "Generated: create invalid message channel")]
    [BenchmarkCategory("Generated", "Construction")]
    public InvalidMessageChannel<OrderImportCommand> Generated_CreateInvalidMessageChannel()
        => GeneratedOrderInvalidMessageChannel.Create(OrderInvalidMessageChannelExampleRunner.CreateInvalidChannel())
            .When(static message => string.IsNullOrWhiteSpace(message.Payload.Sku) || message.Payload.Quantity <= 0)
            .Because(static message => string.IsNullOrWhiteSpace(message.Payload.Sku) ? "SKU is required." : "Quantity must be positive.")
            .Build();

    [Benchmark(Description = "Fluent: route invalid order imports")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderInvalidMessageSummary Fluent_RouteInvalidOrderImports()
        => OrderInvalidMessageChannelExampleRunner.RunFluent(Commands);

    [Benchmark(Description = "Generated: route invalid order imports")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderInvalidMessageSummary Generated_RouteInvalidOrderImports()
    {
        var invalids = OrderInvalidMessageChannelExampleRunner.CreateInvalidChannel();
        var channel = GeneratedOrderInvalidMessageChannel.Create(invalids)
            .When(static message => string.IsNullOrWhiteSpace(message.Payload.Sku) || message.Payload.Quantity <= 0)
            .Because(static message => string.IsNullOrWhiteSpace(message.Payload.Sku) ? "SKU is required." : "Quantity must be positive.")
            .Build();
        var service = new OrderInvalidMessageChannelService(channel);
        return service.Import(Commands);
    }
}
