using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class SplitterTests
{
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

        Assert.Equal(2, parts.Count);
        Assert.Equal("a", parts[0].Payload.Sku);
        Assert.Equal("corr-1", parts[0].Headers.CorrelationId);
        Assert.Equal("msg-1", parts[0].Headers.CausationId);
    }

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

        Assert.Equal("cause-1", parts[0].Headers.CausationId);
    }

    [Fact]
    public void Split_AllowsEmptyResults()
    {
        var splitter = Splitter<Order, LineItem>.Create()
            .Use((_, _) => [])
            .Build();

        var parts = splitter.Split(Message<Order>.Create(new Order("o-1", [])));

        Assert.Empty(parts);
    }

    [Fact]
    public void Split_RejectsNullMessage()
    {
        var splitter = Splitter<Order, LineItem>.Create()
            .Use((m, _) => m.Payload.Items)
            .Build();

        Assert.Throws<ArgumentNullException>(() => splitter.Split(null!));
    }

    [Fact]
    public void Split_RejectsNullHandlerResult()
    {
        var splitter = Splitter<Order, LineItem>.Create()
            .Use((_, _) => null!)
            .Build();

        Assert.Throws<InvalidOperationException>(() => splitter.Split(Message<Order>.Create(new Order("o-1", []))));
    }

    [Fact]
    public void Builder_RequiresHandler()
    {
        Assert.Throws<InvalidOperationException>(() => Splitter<Order, LineItem>.Create().Build());
    }

    [Fact]
    public void Builder_RejectsNullHandler()
    {
        Assert.Throws<ArgumentNullException>(() => Splitter<Order, LineItem>.Create().Use(null!));
    }

    private sealed record Order(string Id, IReadOnlyList<LineItem> Items);

    private sealed record LineItem(string Sku);
}
