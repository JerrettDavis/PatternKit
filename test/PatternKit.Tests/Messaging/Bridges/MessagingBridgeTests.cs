using PatternKit.Messaging;
using PatternKit.Messaging.Bridges;
using PatternKit.Messaging.Channels;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Messaging.Bridges;

[Feature("Messaging Bridge")]
public sealed class MessagingBridgeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("BridgeNext translates one inbound message into the target bus")]
    [Fact]
    public Task BridgeNext_TranslatesOneInboundMessageIntoTheTargetBus()
        => Given("a bridge between partner order imports and internal order events", CreateContext)
            .When("one partner order is bridged", ctx =>
            {
                ctx.Source.Send(Message<PartnerOrder>.Create(new("P-100", "accepted", 125m))
                    .WithCorrelationId("checkout:P-100")
                    .WithHeader("source-system", "partner"));

                return new { ctx.Internal, Result = ctx.Bridge.BridgeNext() };
            })
            .Then("the bridge publishes to the selected topic", ctx =>
            {
                ScenarioExpect.True(ctx.Result.Bridged);
                ScenarioExpect.Equal("accepted", ctx.Result.Topic);
                ScenarioExpect.Equal(1, ctx.Result.AcceptedCount);
            })
            .And("the translated message preserves headers", ctx =>
            {
                var received = ctx.Internal.TryReceive();
                ScenarioExpect.True(received.Received);
                ScenarioExpect.Equal("P-100", received.Message!.Payload.OrderId);
                ScenarioExpect.Equal("checkout:P-100", received.Message.Headers.CorrelationId);
                ScenarioExpect.Equal("partner", received.Message.Headers.GetString("source-system"));
            })
            .AssertPassed();

    [Scenario("BridgeAll drains active work until the source channel is empty")]
    [Fact]
    public Task BridgeAll_DrainsActiveWorkUntilTheSourceChannelIsEmpty()
        => Given("a bridge with two inbound partner orders", CreateContext)
            .When("all available messages are bridged", ctx =>
            {
                ctx.Source.Send(Message<PartnerOrder>.Create(new("P-100", "accepted", 125m)));
                ctx.Source.Send(Message<PartnerOrder>.Create(new("P-101", "paid", 250m)));
                return new { ctx.Source, ctx.Internal, Results = ctx.Bridge.BridgeAll() };
            })
            .Then("both messages are published and the source is empty", ctx =>
            {
                ScenarioExpect.Equal(2, ctx.Results.Count);
                ScenarioExpect.Equal(0, ctx.Source.Count);
                ScenarioExpect.Equal(2, ctx.Internal.Count);
            })
            .And("topics are selected from the inbound payload", ctx =>
                ScenarioExpect.Equal(["accepted", "paid"], ctx.Results.Select(static result => result.Topic).ToArray()))
            .AssertPassed();

    [Scenario("BridgeNext returns an empty result when no source message exists")]
    [Fact]
    public Task BridgeNext_ReturnsEmptyResultWhenNoSourceMessageExists()
        => Given("an empty bridge source channel", CreateContext)
            .When("the bridge is asked for the next message", ctx => ctx.Bridge.BridgeNext())
            .Then("the result reports no bridged message", result =>
            {
                ScenarioExpect.False(result.Bridged);
                ScenarioExpect.Equal(0, result.AcceptedCount);
                ScenarioExpect.Null(result.Message);
                ScenarioExpect.Null(result.PublishResult);
            })
            .AssertPassed();

    [Scenario("Builder rejects invalid messaging bridge configuration")]
    [Fact]
    public Task Builder_RejectsInvalidMessagingBridgeConfiguration()
        => Given("messaging bridge dependencies", CreateContext)
            .When("building invalid bridge configurations", ctx => new Action[]
            {
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create(""),
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create().From(null!),
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create().To(null!),
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create().TranslateWith(null!),
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create().SelectTopic(null!),
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create().PreserveHeaders(null!),
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create().From(ctx.Source).To(ctx.Target).Build(),
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create().From(ctx.Source).To(ctx.Target).PreserveHeaders(Map).Build(),
                () => MessagingBridge<PartnerOrder, InternalOrderEvent>.Create().From(ctx.Source).To(ctx.Target).SelectTopic(SelectTopic).Build()
            })
            .Then("each invalid configuration fails explicitly", failures =>
            {
                ScenarioExpect.Throws<ArgumentException>(failures[0]);
                ScenarioExpect.Throws<ArgumentNullException>(failures[1]);
                ScenarioExpect.Throws<ArgumentNullException>(failures[2]);
                ScenarioExpect.Throws<ArgumentNullException>(failures[3]);
                ScenarioExpect.Throws<ArgumentNullException>(failures[4]);
                ScenarioExpect.Throws<ArgumentNullException>(failures[5]);
                ScenarioExpect.Throws<InvalidOperationException>(failures[6]);
                ScenarioExpect.Throws<InvalidOperationException>(failures[7]);
                ScenarioExpect.Throws<InvalidOperationException>(failures[8]);
            })
            .AssertPassed();

    private static BridgeContext CreateContext()
    {
        var source = MessageChannel<PartnerOrder>.Create("partner-orders").Build();
        var internalOrders = MessageChannel<InternalOrderEvent>.Create("internal-orders").Build();
        var target = MessageBus<InternalOrderEvent>.Create("commerce-bus")
            .Route("accepted", internalOrders)
            .Route("paid", internalOrders)
            .Build();
        var bridge = MessagingBridge<PartnerOrder, InternalOrderEvent>.Create("partner-commerce-bridge")
            .From(source)
            .To(target)
            .PreserveHeaders(Map)
            .SelectTopic(SelectTopic)
            .Build();

        return new(source, internalOrders, target, bridge);
    }

    private static InternalOrderEvent Map(PartnerOrder order)
        => new(order.PartnerOrderId, order.State, order.Amount);

    private static string SelectTopic(Message<PartnerOrder> message)
        => message.Payload.State;

    private sealed record BridgeContext(
        MessageChannel<PartnerOrder> Source,
        MessageChannel<InternalOrderEvent> Internal,
        MessageBus<InternalOrderEvent> Target,
        MessagingBridge<PartnerOrder, InternalOrderEvent> Bridge);

    private sealed record PartnerOrder(string PartnerOrderId, string State, decimal Amount);

    private sealed record InternalOrderEvent(string OrderId, string Status, decimal Total);
}
