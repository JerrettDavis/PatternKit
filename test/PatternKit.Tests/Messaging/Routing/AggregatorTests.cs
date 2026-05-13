using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class AggregatorTests
{
    [Fact]
    public void Add_ReturnsPendingUntilCompletionPolicyMatches()
    {
        var aggregator = CreateCountAggregator(2);

        var first = aggregator.Add(Item("order-1", "msg-1", 10m));
        var second = aggregator.Add(Item("order-1", "msg-2", 15m));

        Assert.False(first.Completed);
        Assert.True(first.Accepted);
        Assert.Equal("order-1", first.Key);
        Assert.Equal(1, first.Count);
        Assert.Equal(default, first.Result);
        Assert.True(second.Completed);
        Assert.True(second.Accepted);
        Assert.Equal("order-1", second.Key);
        Assert.Equal(2, second.Count);
        Assert.Equal(25m, second.Result);
        Assert.Equal(0, aggregator.OpenGroupCount);
    }

    [Fact]
    public void Add_GroupsMessagesBySelectedKey()
    {
        var aggregator = CreateCountAggregator(2);

        var first = aggregator.Add(Item("order-1", "msg-1", 10m));
        var second = aggregator.Add(Item("order-2", "msg-2", 15m));

        Assert.False(first.Completed);
        Assert.False(second.Completed);
        Assert.Equal(2, aggregator.OpenGroupCount);
    }

    [Fact]
    public void Add_IgnoresDuplicateMessageIdsByDefault()
    {
        var aggregator = CreateCountAggregator(2);

        var first = aggregator.Add(Item("order-1", "msg-1", 10m));
        var duplicate = aggregator.Add(Item("order-1", "msg-1", 15m));

        Assert.True(first.Accepted);
        Assert.False(duplicate.Accepted);
        Assert.False(duplicate.Completed);
        Assert.Equal(1, duplicate.Count);
    }

    [Fact]
    public void Add_CanReplaceDuplicateMessageIds()
    {
        var aggregator = CreateCountAggregator(2, DuplicateMessagePolicy.Replace);

        aggregator.Add(Item("order-1", "msg-1", 10m));
        aggregator.Add(Item("order-1", "msg-1", 15m));
        var result = aggregator.Add(Item("order-1", "msg-2", 5m));

        Assert.True(result.Completed);
        Assert.Equal(20m, result.Result);
    }

    [Fact]
    public void Add_CanIncludeDuplicateMessageIds()
    {
        var aggregator = CreateCountAggregator(2, DuplicateMessagePolicy.Include);

        aggregator.Add(Item("order-1", "msg-1", 10m));
        var result = aggregator.Add(Item("order-1", "msg-1", 15m));

        Assert.True(result.Completed);
        Assert.Equal(25m, result.Result);
    }

    [Fact]
    public void Add_AllowsMessagesWithoutMessageIds()
    {
        var aggregator = Aggregator<string, InvoiceLine, decimal>.Create()
            .KeyBy((m, _) => m.Payload.OrderId)
            .CompleteWhen((_, messages, _) => messages.Count == 2)
            .Project((_, messages, _) => messages.Sum(m => m.Payload.Amount))
            .Build();

        aggregator.Add(Message<InvoiceLine>.Create(new InvoiceLine("order-1", 10m)));
        var result = aggregator.Add(Message<InvoiceLine>.Create(new InvoiceLine("order-1", 15m)));

        Assert.True(result.Completed);
        Assert.Equal(25m, result.Result);
    }

    [Fact]
    public void Add_RejectsNullMessage()
    {
        var aggregator = CreateCountAggregator(1);

        Assert.Throws<ArgumentNullException>(() => aggregator.Add(null!));
    }

    [Fact]
    public void Builder_RequiresAllDelegates()
    {
        Assert.Throws<InvalidOperationException>(() => Aggregator<string, InvoiceLine, decimal>.Create().Build());
        Assert.Throws<InvalidOperationException>(() => Aggregator<string, InvoiceLine, decimal>.Create()
            .KeyBy((m, _) => m.Payload.OrderId)
            .Build());
        Assert.Throws<InvalidOperationException>(() => Aggregator<string, InvoiceLine, decimal>.Create()
            .KeyBy((m, _) => m.Payload.OrderId)
            .CompleteWhen((_, _, _) => true)
            .Build());
    }

    [Fact]
    public void Builder_RejectsNullDelegates()
    {
        var builder = Aggregator<string, InvoiceLine, decimal>.Create();

        Assert.Throws<ArgumentNullException>(() => builder.KeyBy(null!));
        Assert.Throws<ArgumentNullException>(() => builder.CompleteWhen(null!));
        Assert.Throws<ArgumentNullException>(() => builder.Project(null!));
    }

    private static Aggregator<string, InvoiceLine, decimal> CreateCountAggregator(
        int count,
        DuplicateMessagePolicy duplicatePolicy = DuplicateMessagePolicy.Ignore)
        => Aggregator<string, InvoiceLine, decimal>.Create()
            .KeyBy((m, _) => m.Payload.OrderId)
            .CompleteWhen((_, messages, _) => messages.Count == count)
            .Project((_, messages, _) => messages.Sum(m => m.Payload.Amount))
            .Duplicates(duplicatePolicy)
            .Build();

    private static Message<InvoiceLine> Item(string orderId, string messageId, decimal amount)
        => Message<InvoiceLine>
            .Create(new InvoiceLine(orderId, amount))
            .WithMessageId(messageId);

    private sealed record InvoiceLine(string OrderId, decimal Amount);
}
