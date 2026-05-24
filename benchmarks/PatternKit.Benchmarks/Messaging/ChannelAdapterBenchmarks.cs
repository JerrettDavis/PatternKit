using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging.Adapters;
using PatternKit.Messaging.Channels;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ChannelAdapter")]
public class ChannelAdapterBenchmarks
{
    private static readonly ErpOrderDocument Document = new("ERP-100", "149.95");

    [Benchmark(Baseline = true, Description = "Fluent: create channel adapter")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ChannelAdapter<ErpOrderDocument, OrderIntegrationMessage> Fluent_CreateChannelAdapter()
    {
        var channels = CreateChannels();
        return ErpChannelAdapters.Create(channels.Inbound, channels.Outbound);
    }

    [Benchmark(Description = "Generated: create channel adapter")]
    [BenchmarkCategory("Generated", "Construction")]
    public ChannelAdapter<ErpOrderDocument, OrderIntegrationMessage> Generated_CreateChannelAdapter()
    {
        var channels = CreateChannels();
        return GeneratedErpChannelAdapter.Create(channels.Inbound, channels.Outbound);
    }

    [Benchmark(Description = "Fluent: round-trip ERP order document")]
    [BenchmarkCategory("Fluent", "Execution")]
    public ErpChannelAdapterSummary Fluent_RoundTripErpOrderDocument()
    {
        var channels = CreateChannels();
        return new ErpChannelAdapterService(
            ErpChannelAdapters.Create(channels.Inbound, channels.Outbound),
            channels).RoundTrip(Document);
    }

    [Benchmark(Description = "Generated: round-trip ERP order document")]
    [BenchmarkCategory("Generated", "Execution")]
    public ErpChannelAdapterSummary Generated_RoundTripErpOrderDocument()
    {
        var channels = CreateChannels();
        return new ErpChannelAdapterService(
            GeneratedErpChannelAdapter.Create(channels.Inbound, channels.Outbound),
            channels).RoundTrip(Document);
    }

    private static ErpChannelAdapterChannels CreateChannels()
        => new(
            MessageChannel<OrderIntegrationMessage>.Create("erp-inbound").Build(),
            MessageChannel<OrderIntegrationMessage>.Create("erp-outbound").Build());
}
