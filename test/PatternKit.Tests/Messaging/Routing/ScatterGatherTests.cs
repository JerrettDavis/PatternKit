using PatternKit.Messaging;
using TinyBDD;
using ScatterGatherCountUnderTest = global::PatternKit.Messaging.Routing.ScatterGather<PatternKit.Tests.Messaging.Routing.ScatterGatherTests.Request, PatternKit.Tests.Messaging.Routing.ScatterGatherTests.Quote, int>;
using ScatterGatherReply = global::PatternKit.Messaging.Routing.ScatterGatherReply<PatternKit.Tests.Messaging.Routing.ScatterGatherTests.Quote>;
using ScatterGatherUnderTest = global::PatternKit.Messaging.Routing.ScatterGather<PatternKit.Tests.Messaging.Routing.ScatterGatherTests.Request, PatternKit.Tests.Messaging.Routing.ScatterGatherTests.Quote, PatternKit.Tests.Messaging.Routing.ScatterGatherTests.QuoteSummary>;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class ScatterGatherTests
{
    [Scenario("Dispatch GathersRepliesAndAggregates")]
    [Fact]
    public void Dispatch_GathersRepliesAndAggregates()
    {
        var scatterGather = ScatterGatherUnderTest.Create("supplier-quotes")
            .AddRecipient("primary", static (_, _) => ScatterGatherReply.Success(new("primary", 12m)))
            .AddRecipient("secondary", static (_, _) => ScatterGatherReply.Success(new("secondary", 10m)))
            .AggregateWith(static (replies, _, _) => new QuoteSummary(
                replies.Count(static reply => reply.Accepted),
                replies.Where(static reply => reply.Accepted).Min(static reply => reply.Response!.Price)))
            .Build();

        var result = scatterGather.Dispatch(Message<Request>.Create(new("sku-1", true)));

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("supplier-quotes", result.Name);
        ScenarioExpect.Equal(2, result.Replies.Count);
        ScenarioExpect.Equal(2, result.Result!.AcceptedCount);
        ScenarioExpect.Equal(10m, result.Result.BestPrice);
    }

    [Scenario("Dispatch AppliesRecipientPredicates")]
    [Fact]
    public void Dispatch_AppliesRecipientPredicates()
    {
        var scatterGather = ScatterGatherCountUnderTest.Create()
            .AddRecipient("standard", static (_, _) => ScatterGatherReply.Success(new("standard", 14m)))
            .AddRecipient("hazmat", static (_, _) => ScatterGatherReply.Success(new("hazmat", 20m)), static (message, _) => message.Payload.Hazmat)
            .AggregateWith(static (replies, _, _) => replies.Count)
            .Build();

        var normal = scatterGather.Dispatch(Message<Request>.Create(new("sku-1", false)));
        var hazmat = scatterGather.Dispatch(Message<Request>.Create(new("sku-1", true)));

        ScenarioExpect.Equal(1, normal.Result);
        ScenarioExpect.Equal(2, hazmat.Result);
    }

    [Scenario("Dispatch RejectsWhenNoRecipientAccepts")]
    [Fact]
    public void Dispatch_RejectsWhenNoRecipientAccepts()
    {
        var scatterGather = ScatterGatherCountUnderTest.Create()
            .AddRecipient("hazmat", static (_, _) => ScatterGatherReply.Success(new("hazmat", 20m)), static (message, _) => message.Payload.Hazmat)
            .AggregateWith(static (replies, _, _) => replies.Count)
            .Build();

        var result = scatterGather.Dispatch(Message<Request>.Create(new("sku-1", false)));

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.Equal("No scatter-gather recipients accepted the request.", result.RejectionReason);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => ScatterGatherCountUnderTest.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() => ScatterGatherCountUnderTest.Create().AddRecipient("", static (_, _) => ScatterGatherReply.Success(new("a", 1m))));
        ScenarioExpect.Throws<ArgumentNullException>(() => ScatterGatherCountUnderTest.Create().AddRecipient("a", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ScatterGatherCountUnderTest.Create().AggregateWith(null!));
        ScenarioExpect.Throws<InvalidOperationException>(() => ScatterGatherCountUnderTest.Create().Build());
        ScenarioExpect.Throws<InvalidOperationException>(() => ScatterGatherCountUnderTest.Create()
            .AddRecipient("a", static (_, _) => ScatterGatherReply.Success(new("a", 1m)))
            .Build());
        ScenarioExpect.Throws<InvalidOperationException>(() => ScatterGatherCountUnderTest.Create()
            .AddRecipient("a", static (_, _) => ScatterGatherReply.Success(new("a", 1m)))
            .AddRecipient("a", static (_, _) => ScatterGatherReply.Success(new("a", 1m))));
        ScenarioExpect.Throws<ArgumentException>(() => ScatterGatherReply.Failure(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => ScatterGatherCountUnderTest.Create()
            .AddRecipient("a", static (_, _) => ScatterGatherReply.Success(new("a", 1m)))
            .AggregateWith(static (replies, _, _) => replies.Count)
            .Build()
            .Dispatch(null!));
    }

    public sealed record Request(string Sku, bool Hazmat);

    public sealed record Quote(string Supplier, decimal Price);

    public sealed record QuoteSummary(int AcceptedCount, decimal BestPrice);
}
