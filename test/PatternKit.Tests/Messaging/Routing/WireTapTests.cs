using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class WireTapTests
{
    [Scenario("Publish InvokesAllTapsAndReturnsOriginalMessage")]
    [Fact]
    public void Publish_InvokesAllTapsAndReturnsOriginalMessage()
    {
        var observed = new List<string>();
        var tap = WireTap<Order>.Create("order-observer")
            .AddTap("audit", (m, _) => observed.Add($"audit:{m.Payload.Id}"))
            .AddTap("metrics", (m, _) => observed.Add($"metrics:{m.Payload.Total}"))
            .Build();
        var message = Message<Order>.Create(new Order("o-1", 125m));

        var result = tap.Publish(message);

        ScenarioExpect.Equal(message, result.Message);
        ScenarioExpect.Equal("order-observer", result.TapName);
        ScenarioExpect.Equal(["audit", "metrics"], result.InvokedTaps);
        ScenarioExpect.Equal(["audit:o-1", "metrics:125"], observed);
    }

    [Scenario("Publish PassesContextToTapHandlers")]
    [Fact]
    public void Publish_PassesContextToTapHandlers()
    {
        string? seenCorrelationId = null;
        var tap = WireTap<Order>.Create()
            .AddTap("audit", (_, ctx) => seenCorrelationId = ctx.Headers.CorrelationId)
            .Build();
        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("corr-1"));

        _ = tap.Publish(Message<Order>.Create(new Order("o-1", 125m)), context);

        ScenarioExpect.Equal("corr-1", seenCorrelationId);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => WireTap<Order>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() => WireTap<Order>.Create().AddTap("", static (_, _) => { }));
        ScenarioExpect.Throws<ArgumentNullException>(() => WireTap<Order>.Create().AddTap("audit", null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => WireTap<Order>.Create().Build());
    }

    [Scenario("Publish RejectsNullMessage")]
    [Fact]
    public void Publish_RejectsNullMessage()
    {
        var tap = WireTap<Order>.Create()
            .AddTap("audit", static (_, _) => { })
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => tap.Publish(null!));
    }

    private sealed record Order(string Id, decimal Total);
}
