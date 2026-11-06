using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class SplitterTests
{
    [Scenario("Split ReturnsItemMessages")]
    [Fact]
    public void Split_ReturnsItemMessages()
    {
        var splitter = Splitter<Order, LineItem>.Create()
            .Use((m, _) => m.Payload.Items)
            .Build();

        var message = new Message<Order>(
            new Order("o-1", [new LineItem("a"), new LineItem("b")]),
            MessageHeaders.Empty.WithMessageId("msg-1").WithCorrelationId("corr-1"));

        var parts = splitter.Split(message);

        ScenarioExpect.Equal(2, parts.Count);
        ScenarioExpect.Equal("a", parts[0].Payload.Sku);
        ScenarioExpect.Equal("corr-1", parts[0].Headers.CorrelationId);
        ScenarioExpect.Equal("msg-1", parts[0].Headers.CausationId);
    }

    [Scenario("Split PreservesExistingCausationId")]
    [Fact]
    public void Split_PreservesExistingCausationId()
    {
        var splitter = Splitter<Order, LineItem>.Create()
            .Use((m, _) => m.Payload.Items)
            .Build();
        var message = new Message<Order>(
            new Order("o-1", [new LineItem("a")]),
            MessageHeaders.Empty.WithMessageId("msg-1").WithCausationId("cause-1"));

        var parts = splitter.Split(message);

        ScenarioExpect.Equal("cause-1", parts[0].Headers.CausationId);
    }

    [Scenario("Split AllowsEmptyResults")]
    [Fact]
    public void Split_AllowsEmptyResults()
    {
        var splitter = Splitter<Order, LineItem>.Create()
            .Use((_, _) => [])
            .Build();

        var parts = splitter.Split(Message<Order>.Create(new Order("o-1", [])));

        ScenarioExpect.Empty(parts);
    }

    [Scenario("Split RejectsNullMessage")]
    [Fact]
    public void Split_RejectsNullMessage()
    {
        var splitter = Splitter<Order, LineItem>.Create()
            .Use((m, _) => m.Payload.Items)
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => splitter.Split(null!));
    }

    [Scenario("Split RejectsNullHandlerResult")]
    [Fact]
    public void Split_RejectsNullHandlerResult()
    {
        var splitter = Splitter<Order, LineItem>.Create()
            .Use((_, _) => null!)
            .Build();

        ScenarioExpect.Throws<InvalidOperationException>(() => splitter.Split(Message<Order>.Create(new Order("o-1", []))));
    }

    [Scenario("Builder RequiresHandler")]
    [Fact]
    public void Builder_RequiresHandler()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() => Splitter<Order, LineItem>.Create().Build());
    }

    [Scenario("Builder RejectsNullHandler")]
    [Fact]
    public void Builder_RejectsNullHandler()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => Splitter<Order, LineItem>.Create().Use(null!));
    }

    private sealed record Order(string Id, IReadOnlyList<LineItem> Items);

    private sealed record LineItem(string Sku);
}
