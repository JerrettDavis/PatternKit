using PatternKit.Application.EventSourcing;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.EventSourcing;

[Feature("Event Sourcing")]
public sealed partial class EventStoreTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Event store appends and reads stream events")]
    [Fact]
    public Task Event_Store_Appends_And_Reads_Stream_Events()
        => Given("an order event store", () => InMemoryEventStore<OrderEvent, string>.Create("order-events").Build())
        .When("events are appended to one stream", (Func<IEventStore<OrderEvent, string>, ValueTask<StreamScenario>>)(async store =>
        {
            var first = await store.AppendAsync("order-100", 0, [new OrderPlaced("order-100", 125m)]);
            var second = await store.AppendAsync("order-100", 1, [new OrderPaid("order-100", "payment-1")]);
            var stream = await store.ReadStreamAsync("order-100");
            return new(first, second, stream);
        }))
        .Then("the stream keeps committed order and versions", result =>
        {
            ScenarioExpect.True(result.First.Committed);
            ScenarioExpect.True(result.Second.Committed);
            ScenarioExpect.Equal(1, result.First.AppendedCount);
            ScenarioExpect.Equal(2, result.Second.Version);
            ScenarioExpect.Equal([1L, 2L], result.Stream.Select(static stored => stored.Version));
            ScenarioExpect.IsType<OrderPlaced>(result.Stream[0].Event);
            ScenarioExpect.IsType<OrderPaid>(result.Stream[1].Event);
        })
        .AssertPassed();

    [Scenario("Event store rejects stale expected versions")]
    [Fact]
    public Task Event_Store_Rejects_Stale_Expected_Versions()
        => Given("an order event stream with one committed event", (Func<ValueTask<IEventStore<OrderEvent, string>>>)(async () =>
        {
            var store = InMemoryEventStore<OrderEvent, string>.Create("order-events").Build();
            _ = await store.AppendAsync("order-100", 0, [new OrderPlaced("order-100", 125m)]);
            return store;
        }))
        .When("a stale append is attempted", (Func<IEventStore<OrderEvent, string>, ValueTask<ConflictScenario>>)(async store =>
        {
            var conflict = await store.AppendAsync("order-100", 0, [new OrderPaid("order-100", "payment-1")]);
            var stream = await store.ReadStreamAsync("order-100");
            return new(conflict, stream);
        }))
        .Then("the append reports a conflict without mutating the stream", result =>
        {
            ScenarioExpect.Equal(EventStoreAppendStatus.Conflict, result.Conflict.Status);
            ScenarioExpect.False(result.Conflict.Committed);
            ScenarioExpect.Equal(1, result.Conflict.Version);
            ScenarioExpect.Equal(0, result.Conflict.ExpectedVersion);
            ScenarioExpect.Single(result.Stream);
        })
        .AssertPassed();

    [Scenario("Event store validates required configuration")]
    [Fact]
    public Task Event_Store_Validates_Required_Configuration()
        => Given("event store builders", () => true)
        .Then("invalid arguments are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => InMemoryEventStore<OrderEvent, string>.Create(""));
            ScenarioExpect.Throws<ArgumentNullException>(() => InMemoryEventStore<OrderEvent, string>.Create("order-events").UseComparer(null!));
            var store = InMemoryEventStore<OrderEvent, string>.Create("order-events").Build();
            ScenarioExpect.Throws<ArgumentNullException>(() => store.AppendAsync(null!, 0, [new OrderPlaced("order-1", 10m)]).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => store.AppendAsync("order-1", -1, [new OrderPlaced("order-1", 10m)]).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentNullException>(() => store.AppendAsync("order-1", 0, null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentException>(() => store.AppendAsync("order-1", 0, []).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentException>(() => store.AppendAsync("order-1", 0, [null!]).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentNullException>(() => store.ReadStreamAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => EventStoreAppendResult.Commit(-1, 1));
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => EventStoreAppendResult.Commit(1, -1));
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => EventStoreAppendResult.Conflict(-1, 0));
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => EventStoreAppendResult.Conflict(0, -1));
        })
        .AssertPassed();

    private abstract record OrderEvent(string OrderId);

    private sealed record OrderPlaced(string OrderId, decimal Total) : OrderEvent(OrderId);

    private sealed record OrderPaid(string OrderId, string PaymentId) : OrderEvent(OrderId);

    private sealed record StreamScenario(
        EventStoreAppendResult First,
        EventStoreAppendResult Second,
        IReadOnlyList<StoredEvent<OrderEvent, string>> Stream);

    private sealed record ConflictScenario(
        EventStoreAppendResult Conflict,
        IReadOnlyList<StoredEvent<OrderEvent, string>> Stream);
}
