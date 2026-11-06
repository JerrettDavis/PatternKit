using PatternKit.Application.Aggregates;
using TinyBDD;

namespace PatternKit.Tests.Application.Aggregates;

public sealed class AggregateRootTests
{
    private interface IOrderEvent;

    private sealed record OrderOpened(string OrderId, decimal Total) : IOrderEvent;

    private sealed record OrderPaid(string OrderId) : IOrderEvent;

    private sealed record PayOrder;

    private sealed class OrderAggregate(string id) : AggregateRoot<string, IOrderEvent>(id)
    {
        public bool IsOpen { get; private set; }

        public bool IsPaid { get; private set; }

        public decimal Total { get; private set; }

        public static OrderAggregate Open(string id, decimal total)
        {
            var aggregate = new OrderAggregate(id);
            aggregate.Raise(new OrderOpened(id, total), aggregate.Apply);
            return aggregate;
        }

        public void Apply(IOrderEvent domainEvent)
        {
            switch (domainEvent)
            {
                case OrderOpened opened:
                    IsOpen = true;
                    Total = opened.Total;
                    break;
                case OrderPaid:
                    IsPaid = true;
                    break;
            }
        }

        public void Record(IOrderEvent domainEvent) => Raise(domainEvent, Apply);

        public void ReplayCommitted(IOrderEvent domainEvent) => Replay(domainEvent, Apply);

        public void RecordWithNullApply(IOrderEvent domainEvent) => Raise(domainEvent, null!);

        public void ReplayWithNullApply(IOrderEvent domainEvent) => Replay(domainEvent, null!);
    }

    [Scenario("Aggregate root tracks uncommitted events and version")]
    [Fact]
    public void Aggregate_Root_Tracks_Uncommitted_Events_And_Version()
    {
        var order = OrderAggregate.Open("ORD-100", 25m);

        ScenarioExpect.True(order.IsOpen);
        ScenarioExpect.Equal(1L, order.Version);
        ScenarioExpect.Single(order.UncommittedEvents);

        var events = order.DequeueUncommittedEvents();

        ScenarioExpect.Single(events);
        ScenarioExpect.Empty(order.UncommittedEvents);
    }

    [Scenario("Aggregate root replays committed events without uncommitted changes")]
    [Fact]
    public void Aggregate_Root_Replays_Committed_Events_Without_Uncommitted_Changes()
    {
        var order = new OrderAggregate("ORD-100");

        order.ReplayCommitted(new OrderOpened("ORD-100", 25m));

        ScenarioExpect.True(order.IsOpen);
        ScenarioExpect.Equal(1L, order.Version);
        ScenarioExpect.Empty(order.UncommittedEvents);
    }

    [Scenario("Aggregate command handler decides and applies events")]
    [Fact]
    public void Aggregate_Command_Handler_Decides_And_Applies_Events()
    {
        var order = OrderAggregate.Open("ORD-100", 25m);
        order.MarkCommitted();
        var handler = AggregateCommandHandler<OrderAggregate, PayOrder, IOrderEvent>.Create(
            "pay-order",
            static (aggregate, _) => aggregate.IsPaid ? [] : [new OrderPaid(aggregate.Id)],
            static (aggregate, domainEvent) => aggregate.Record(domainEvent));

        var result = handler.Execute(order, new PayOrder());

        ScenarioExpect.True(result.HasChanges);
        ScenarioExpect.Equal("pay-order", result.Handler);
        ScenarioExpect.True(order.IsPaid);
        ScenarioExpect.Single(result.Events);
        ScenarioExpect.Single(order.UncommittedEvents);
    }

    [Scenario("Aggregate command handler reports no changes")]
    [Fact]
    public void Aggregate_Command_Handler_Reports_No_Changes()
    {
        var order = OrderAggregate.Open("ORD-100", 25m);
        order.Record(new OrderPaid("ORD-100"));
        order.MarkCommitted();
        var handler = AggregateCommandHandler<OrderAggregate, PayOrder, IOrderEvent>.Create(
            "pay-order",
            static (aggregate, _) => aggregate.IsPaid ? [] : [new OrderPaid(aggregate.Id)],
            static (aggregate, domainEvent) => aggregate.Record(domainEvent));

        var result = handler.Execute(order, new PayOrder());

        ScenarioExpect.False(result.HasChanges);
        ScenarioExpect.Empty(result.Events);
        ScenarioExpect.Empty(order.UncommittedEvents);
    }

    [Scenario("Aggregate command handler treats null decisions as no changes")]
    [Fact]
    public void Aggregate_Command_Handler_Treats_Null_Decisions_As_No_Changes()
    {
        var order = OrderAggregate.Open("ORD-100", 25m);
        order.MarkCommitted();
        var handler = AggregateCommandHandler<OrderAggregate, PayOrder, IOrderEvent>.Create(
            "pay-order",
            static (_, _) => null!,
            static (aggregate, domainEvent) => aggregate.Record(domainEvent));

        var result = handler.Execute(order, new PayOrder());

        ScenarioExpect.False(result.HasChanges);
        ScenarioExpect.Empty(result.Events);
    }

    [Scenario("Aggregate command handler rejects invalid construction and execution")]
    [Fact]
    public void Aggregate_Command_Handler_Rejects_Invalid_Construction_And_Execution()
    {
        var handler = AggregateCommandHandler<OrderAggregate, PayOrder, IOrderEvent>.Create(
            "pay-order",
            static (_, _) => [],
            static (_, _) => { });

        ScenarioExpect.Throws<ArgumentException>(() => AggregateCommandHandler<OrderAggregate, PayOrder, IOrderEvent>.Create("", static (_, _) => [], static (_, _) => { }));
        ScenarioExpect.Throws<ArgumentNullException>(() => AggregateCommandHandler<OrderAggregate, PayOrder, IOrderEvent>.Create("pay", null!, static (_, _) => { }));
        ScenarioExpect.Throws<ArgumentNullException>(() => AggregateCommandHandler<OrderAggregate, PayOrder, IOrderEvent>.Create("pay", static (_, _) => [], null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => handler.Execute(null!, new PayOrder()));
        ScenarioExpect.Throws<ArgumentNullException>(() => handler.Execute(OrderAggregate.Open("ORD-100", 25m), null!));
    }

    [Scenario("Aggregate root rejects null apply delegates")]
    [Fact]
    public void Aggregate_Root_Rejects_Null_Apply_Delegates()
    {
        var order = new OrderAggregate("ORD-100");

        ScenarioExpect.Throws<ArgumentNullException>(() => order.RecordWithNullApply(new OrderOpened("ORD-100", 25m)));
        ScenarioExpect.Throws<ArgumentNullException>(() => order.ReplayWithNullApply(new OrderOpened("ORD-100", 25m)));
    }
}
