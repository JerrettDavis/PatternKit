using PatternKit.Messaging;
using PatternKit.Messaging.Adapters;
using PatternKit.Messaging.Channels;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Adapters;

public sealed class ChannelAdapterTests
{
    [Scenario("AcceptExternal TranslatesAndEnqueuesMessage")]
    [Fact]
    public void AcceptExternal_TranslatesAndEnqueuesMessage()
    {
        var inbound = MessageChannel<Command>.Create("inbound").Build();
        var outbound = MessageChannel<Command>.Create("outbound").Build();
        var adapter = ChannelAdapter<ExternalCommand, Command>.Create("erp")
            .ReceiveInto(inbound)
            .SendFrom(outbound)
            .MapInbound((external, _) => Message<Command>.Create(new(external.Sku, external.Quantity)))
            .MapOutbound((message, _) => new(message.Payload.Sku, message.Payload.Quantity))
            .Build();

        var result = adapter.AcceptExternal(new("sku-1", 3));
        var received = inbound.TryReceive();

        ScenarioExpect.True(result.Accepted);
        ScenarioExpect.Equal("erp", result.AdapterName);
        ScenarioExpect.True(received.Received);
        ScenarioExpect.Equal("sku-1", received.Message!.Payload.Sku);
    }

    [Scenario("TryTakeExternal TranslatesOutboundMessage")]
    [Fact]
    public void TryTakeExternal_TranslatesOutboundMessage()
    {
        var inbound = MessageChannel<Command>.Create("inbound").Build();
        var outbound = MessageChannel<Command>.Create("outbound").Build();
        outbound.Send(Message<Command>.Create(new("sku-1", 3)));
        var adapter = ChannelAdapter<ExternalCommand, Command>.Create()
            .ReceiveInto(inbound)
            .SendFrom(outbound)
            .MapInbound((external, _) => Message<Command>.Create(new(external.Sku, external.Quantity)))
            .MapOutbound((message, _) => new(message.Payload.Sku, message.Payload.Quantity))
            .Build();

        var first = adapter.TryTakeExternal();
        var second = adapter.TryTakeExternal();

        ScenarioExpect.True(first.Produced);
        ScenarioExpect.Equal("sku-1", first.External!.Sku);
        ScenarioExpect.False(second.Produced);
        ScenarioExpect.Null(second.External);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        var channel = MessageChannel<Command>.Create().Build();

        ScenarioExpect.Throws<ArgumentException>(() => ChannelAdapter<ExternalCommand, Command>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChannelAdapter<ExternalCommand, Command>.Create().ReceiveInto(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChannelAdapter<ExternalCommand, Command>.Create().SendFrom(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChannelAdapter<ExternalCommand, Command>.Create().MapInbound(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ChannelAdapter<ExternalCommand, Command>.Create().MapOutbound(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => ChannelAdapter<ExternalCommand, Command>.Create()
            .ReceiveInto(channel)
            .SendFrom(channel)
            .MapInbound((external, _) => Message<Command>.Create(new(external.Sku, external.Quantity)))
            .Build());
    }

    public sealed record ExternalCommand(string Sku, int Quantity);

    public sealed record Command(string Sku, int Quantity);
}
