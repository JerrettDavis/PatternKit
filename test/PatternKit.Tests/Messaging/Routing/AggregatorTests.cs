using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class AggregatorTests
{
    [Scenario("Add ReturnsPendingUntilCompletionPolicyMatches")]
    [Fact]
    public void Add_ReturnsPendingUntilCompletionPolicyMatches()
    {
        var aggregator = CreateCountAggregator(2);

        var first = aggregator.Add(Item("order-1", "msg-1", 10m));
        var second = aggregator.Add(Item("order-1", "msg-2", 15m));

        ScenarioExpect.False(first.Completed);
        ScenarioExpect.True(first.Accepted);
        ScenarioExpect.Equal("order-1", first.Key);
        ScenarioExpect.Equal(1, first.Count);
        ScenarioExpect.Equal(default, first.Result);
        ScenarioExpect.True(second.Completed);
        ScenarioExpect.True(second.Accepted);
        ScenarioExpect.Equal("order-1", second.Key);
        ScenarioExpect.Equal(2, second.Count);
        ScenarioExpect.Equal(25m, second.Result);
        ScenarioExpect.Equal(0, aggregator.OpenGroupCount);
    }

    [Scenario("Add GroupsMessagesBySelectedKey")]
    [Fact]
    public void Add_GroupsMessagesBySelectedKey()
    {
        var aggregator = CreateCountAggregator(2);

        var first = aggregator.Add(Item("order-1", "msg-1", 10m));
        var second = aggregator.Add(Item("order-2", "msg-2", 15m));

        ScenarioExpect.False(first.Completed);
        ScenarioExpect.False(second.Completed);
        ScenarioExpect.Equal(2, aggregator.OpenGroupCount);
    }

    [Scenario("Add IgnoresDuplicateMessageIdsByDefault")]
    [Fact]
    public void Add_IgnoresDuplicateMessageIdsByDefault()
    {
        var aggregator = CreateCountAggregator(2);

        var first = aggregator.Add(Item("order-1", "msg-1", 10m));
        var duplicate = aggregator.Add(Item("order-1", "msg-1", 15m));

        ScenarioExpect.True(first.Accepted);
        ScenarioExpect.False(duplicate.Accepted);
        ScenarioExpect.False(duplicate.Completed);
        ScenarioExpect.Equal(1, duplicate.Count);
    }

    [Scenario("Add CanReplaceDuplicateMessageIds")]
    [Fact]
    public void Add_CanReplaceDuplicateMessageIds()
    {
        var aggregator = CreateCountAggregator(2, DuplicateMessagePolicy.Replace);

        aggregator.Add(Item("order-1", "msg-1", 10m));
        aggregator.Add(Item("order-1", "msg-1", 15m));
        var result = aggregator.Add(Item("order-1", "msg-2", 5m));

        ScenarioExpect.True(result.Completed);
        ScenarioExpect.Equal(20m, result.Result);
    }

    [Scenario("Add CanIncludeDuplicateMessageIds")]
    [Fact]
    public void Add_CanIncludeDuplicateMessageIds()
    {
        var aggregator = CreateCountAggregator(2, DuplicateMessagePolicy.Include);

        aggregator.Add(Item("order-1", "msg-1", 10m));
        var result = aggregator.Add(Item("order-1", "msg-1", 15m));

        ScenarioExpect.True(result.Completed);
        ScenarioExpect.Equal(25m, result.Result);
    }

    [Scenario("Add AllowsMessagesWithoutMessageIds")]
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

        ScenarioExpect.True(result.Completed);
        ScenarioExpect.Equal(25m, result.Result);
    }

    [Scenario("Add RejectsNullMessage")]
    [Fact]
    public void Add_RejectsNullMessage()
    {
        var aggregator = CreateCountAggregator(1);

        ScenarioExpect.Throws<ArgumentNullException>(() => aggregator.Add(null!));
    }

    [Scenario("Builder RequiresAllDelegates")]
    [Fact]
    public void Builder_RequiresAllDelegates()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() => Aggregator<string, InvoiceLine, decimal>.Create().Build());
        ScenarioExpect.Throws<InvalidOperationException>(() => Aggregator<string, InvoiceLine, decimal>.Create()
            .KeyBy((m, _) => m.Payload.OrderId)
            .Build());
        ScenarioExpect.Throws<InvalidOperationException>(() => Aggregator<string, InvoiceLine, decimal>.Create()
            .KeyBy((m, _) => m.Payload.OrderId)
            .CompleteWhen((_, _, _) => true)
            .Build());
    }

    [Scenario("Builder RejectsNullDelegates")]
    [Fact]
    public void Builder_RejectsNullDelegates()
    {
        var builder = Aggregator<string, InvoiceLine, decimal>.Create();

        ScenarioExpect.Throws<ArgumentNullException>(() => builder.KeyBy(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.CompleteWhen(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => builder.Project(null!));
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
