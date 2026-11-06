using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Gateways;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Gateways;

public sealed class MessagingGatewayTests
{
    [Scenario("Invoke SendsRequestAndReturnsResponse")]
    [Fact]
    public void Invoke_SendsRequestAndReturnsResponse()
    {
        var channel = MessageChannel<Request>.Create("requests").Build();
        var gateway = MessagingGateway<Request, Response>.Create("payments")
            .SendTo(channel)
            .Handle((request, _) => Message<Response>.Create(new(request.Payload.OrderId, true)))
            .Build();

        var result = gateway.Invoke(new("order-1", 42m));

        ScenarioExpect.True(result.Completed);
        ScenarioExpect.Equal("payments", result.GatewayName);
        ScenarioExpect.Equal("order-1", result.Response!.Payload.OrderId);
        ScenarioExpect.Equal(1, channel.Count);
    }

    [Scenario("InvokeReturnsRejectedResultWhenRequestChannelRejects")]
    [Fact]
    public void InvokeReturnsRejectedResultWhenRequestChannelRejects()
    {
        var channel = MessageChannel<Request>.Create("requests").WithCapacity(1).Build();
        channel.Send(Message<Request>.Create(new("existing", 1m)));
        var gateway = MessagingGateway<Request, Response>.Create()
            .SendTo(channel)
            .Handle((request, _) => Message<Response>.Create(new(request.Payload.OrderId, true)))
            .Build();

        var result = gateway.Invoke(new("order-1", 42m));

        ScenarioExpect.False(result.Completed);
        ScenarioExpect.Null(result.Response);
        ScenarioExpect.False(result.ChannelResult.Accepted);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        var channel = MessageChannel<Request>.Create().Build();

        ScenarioExpect.Throws<ArgumentException>(() => MessagingGateway<Request, Response>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessagingGateway<Request, Response>.Create().SendTo(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessagingGateway<Request, Response>.Create().Handle(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => MessagingGateway<Request, Response>.Create().SendTo(channel).Build());
    }

    public sealed record Request(string OrderId, decimal Amount);

    public sealed record Response(string OrderId, bool Approved);
}
