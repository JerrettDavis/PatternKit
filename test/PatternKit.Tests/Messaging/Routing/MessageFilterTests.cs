using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class MessageFilterTests
{
    [Scenario("Filter AcceptsFirstMatchingAllowRule")]
    [Fact]
    public void Filter_AcceptsFirstMatchingAllowRule()
    {
        var filter = MessageFilter<Order>.Create("fraud-screen")
            .AllowWhen("trusted-customer", static (m, _) => m.Payload.CustomerTier == "trusted")
            .AllowWhen("low-value", static (m, _) => m.Payload.Total < 100m)
            .Build();

        var result = filter.Filter(Message<Order>.Create(new Order("o-1", "trusted", 250m)));

        ScenarioExpect.True(result.Accepted);
        ScenarioExpect.Equal("fraud-screen", result.FilterName);
        ScenarioExpect.Equal("trusted-customer", result.RuleName);
        ScenarioExpect.Null(result.RejectionReason);
        ScenarioExpect.Equal("o-1", result.Message.Payload.Id);
    }

    [Scenario("Filter RejectsUnmatchedMessagesWithConfiguredReason")]
    [Fact]
    public void Filter_RejectsUnmatchedMessagesWithConfiguredReason()
    {
        var filter = MessageFilter<Order>.Create()
            .AllowWhen("low-value", static (m, _) => m.Payload.Total < 100m)
            .RejectUnmatched("Manual fraud review required.")
            .Build();

        var result = filter.Filter(Message<Order>.Create(new Order("o-1", "guest", 250m)));

        ScenarioExpect.False(result.Accepted);
        ScenarioExpect.Null(result.RuleName);
        ScenarioExpect.Equal("Manual fraud review required.", result.RejectionReason);
    }

    [Scenario("Filter PassesContextToAllowRules")]
    [Fact]
    public void Filter_PassesContextToAllowRules()
    {
        var filter = MessageFilter<Order>.Create()
            .AllowWhen("tenant-allow-list", static (_, ctx) => ctx.Headers.CorrelationId == "tenant-a")
            .Build();
        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("tenant-a"));

        var result = filter.Filter(Message<Order>.Create(new Order("o-1", "guest", 250m)), context);

        ScenarioExpect.True(result.Accepted);
        ScenarioExpect.Equal("tenant-allow-list", result.RuleName);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageFilter<Order>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() => MessageFilter<Order>.Create().AllowWhen("", static (_, _) => true));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageFilter<Order>.Create().AllowWhen("valid", null!));
        ScenarioExpect.Throws<ArgumentException>(() => MessageFilter<Order>.Create().RejectUnmatched(""));
        ScenarioExpect.Throws<InvalidOperationException>(() => MessageFilter<Order>.Create().Build());
    }

    [Scenario("Filter RejectsNullMessage")]
    [Fact]
    public void Filter_RejectsNullMessage()
    {
        var filter = MessageFilter<Order>.Create()
            .AllowWhen("all", static (_, _) => true)
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => filter.Filter(null!));
    }

    private sealed record Order(string Id, string CustomerTier, decimal Total);
}
