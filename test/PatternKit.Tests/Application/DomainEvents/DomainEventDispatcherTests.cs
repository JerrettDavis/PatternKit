using PatternKit.Application.DomainEvents;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.DomainEvents;

[Feature("Domain Event")]
public sealed partial class DomainEventDispatcherTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Domain Event dispatcher invokes typed handlers")]
    [Fact]
    public Task Domain_Event_Dispatcher_Invokes_Typed_Handlers()
        => Given("a dispatcher with two handlers for the same event", () =>
        {
            var handled = new List<string>();
            var dispatcher = DomainEventDispatcher<OrderDomainEvent>.Create("orders")
                .Handle<OrderPlaced>((domainEvent, _) =>
                {
                    handled.Add($"projection:{domainEvent.OrderId}");
                    return ValueTask.CompletedTask;
                })
                .Handle<OrderPlaced>((domainEvent, _) =>
                {
                    handled.Add($"audit:{domainEvent.OrderId}");
                    return ValueTask.CompletedTask;
                })
                .Build();
            return new DispatcherContext(dispatcher, handled);
        })
        .When("an order placed event is dispatched", (Func<DispatcherContext, ValueTask<DispatchedEvent>>)(async ctx =>
            new DispatchedEvent(await ctx.Dispatcher.DispatchAsync(new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, "order-100")), ctx.Handled)))
        .Then("all matching handlers run", result =>
        {
            ScenarioExpect.Equal(DomainEventDispatchStatus.Handled, result.Result.Status);
            ScenarioExpect.Equal(2, result.Result.HandlerCount);
            ScenarioExpect.Equal(["projection:order-100", "audit:order-100"], result.Handled);
        })
        .AssertPassed();

    [Scenario("Domain Event dispatcher returns unhandled for unknown event types")]
    [Fact]
    public Task Domain_Event_Dispatcher_Returns_Unhandled_For_Unknown_Event_Types()
        => Given("a dispatcher for placed orders", () => DomainEventDispatcher<OrderDomainEvent>.Create("orders")
            .Handle<OrderPlaced>(static (_, _) => ValueTask.CompletedTask)
            .Build())
        .When("a different event type is dispatched", (Func<IDomainEventDispatcher<OrderDomainEvent>, ValueTask<DomainEventDispatchResult>>)(async dispatcher =>
            await dispatcher.DispatchAsync(new OrderBilled(Guid.NewGuid(), DateTimeOffset.UtcNow, "order-100"))))
        .Then("the result is unhandled", result =>
        {
            ScenarioExpect.Equal(DomainEventDispatchStatus.Unhandled, result.Status);
            ScenarioExpect.False(result.Succeeded);
            ScenarioExpect.Equal(0, result.HandlerCount);
            ScenarioExpect.Equal(typeof(OrderBilled), result.EventType);
        })
        .AssertPassed();

    [Scenario("Domain Event dispatcher reports handler failures")]
    [Fact]
    public Task Domain_Event_Dispatcher_Reports_Handler_Failures()
        => Given("a dispatcher with a failing handler", () => DomainEventDispatcher<OrderDomainEvent>.Create("orders")
            .Handle<OrderPlaced>(static (_, _) => throw new InvalidOperationException("projection failed"))
            .Build())
        .When("the event is dispatched", (Func<IDomainEventDispatcher<OrderDomainEvent>, ValueTask<DomainEventDispatchResult>>)(async dispatcher =>
            await dispatcher.DispatchAsync(new OrderPlaced(Guid.NewGuid(), DateTimeOffset.UtcNow, "order-100"))))
        .Then("the failure is returned", result =>
        {
            ScenarioExpect.Equal(DomainEventDispatchStatus.Failed, result.Status);
            ScenarioExpect.False(result.Succeeded);
            ScenarioExpect.IsType<InvalidOperationException>(result.Exception);
        })
        .AssertPassed();

    [Scenario("Domain Event dispatcher validates required configuration")]
    [Fact]
    public Task Domain_Event_Dispatcher_Validates_Required_Configuration()
        => Given("domain event dispatcher builders", () => true)
        .Then("invalid arguments are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => DomainEventDispatcher<OrderDomainEvent>.Create(""));
            ScenarioExpect.Throws<ArgumentNullException>(() => DomainEventDispatcher<OrderDomainEvent>.Create("orders").Handle<OrderPlaced>(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => DomainEventDispatcher<OrderDomainEvent>.Create("orders").Build().DispatchAsync(null!).AsTask().GetAwaiter().GetResult());
            ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => DomainEventDispatchResult.Handled(typeof(OrderPlaced), 0));
            ScenarioExpect.Throws<ArgumentNullException>(() => DomainEventDispatchResult.Unhandled(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => DomainEventDispatchResult.Failed(typeof(OrderPlaced), null!));
        })
        .AssertPassed();

    private abstract record OrderDomainEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;

    private sealed record OrderPlaced(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderDomainEvent(EventId, OccurredAt);

    private sealed record OrderBilled(Guid EventId, DateTimeOffset OccurredAt, string OrderId) : OrderDomainEvent(EventId, OccurredAt);

    private sealed record DispatcherContext(IDomainEventDispatcher<OrderDomainEvent> Dispatcher, List<string> Handled);

    private sealed record DispatchedEvent(DomainEventDispatchResult Result, List<string> Handled);
}
