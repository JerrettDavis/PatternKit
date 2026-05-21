using PatternKit.Application.MaterializedViews;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.MaterializedViews;

[Feature("Materialized View")]
public sealed class MaterializedViewTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Materialized view projects ordered events into a read model")]
    [Fact]
    public Task Materialized_View_Projects_Ordered_Events_Into_A_Read_Model()
        => Given("a materialized view with order handlers", () => MaterializedView<OrderReadModel, OrderEvent>.Create("orders")
            .WithHandler<OrderPlaced>((state, @event) => state with { OrderId = @event.OrderId, Status = "Placed" }, order: 10)
            .WithHandler<OrderPaid>((state, _) => state with { Status = "Paid" }, order: 20)
            .Build())
        .When("projecting an event stream", view => view.ProjectAsync(
            new OrderReadModel("", "Pending"),
            new OrderEvent[] { new OrderPlaced("order-1"), new OrderPaid("order-1") }).AsTask())
        .Then("the read model contains the latest state", projected =>
        {
            ScenarioExpect.Equal("order-1", projected.OrderId);
            ScenarioExpect.Equal("Paid", projected.Status);
        })
        .AssertPassed();

    [Scenario("Materialized view applies handlers deterministically")]
    [Fact]
    public Task Materialized_View_Applies_Handlers_Deterministically()
        => Given("a materialized view with multiple handlers for the same event", () => MaterializedView<ProjectionState, ProjectionEvent>.Create("projection")
            .WithHandler<ProjectionEvent>((state, _) => state.Append("second"), order: 20)
            .WithHandler<ProjectionEvent>((state, _) => state.Append("first"), order: 10)
            .Build())
        .When("projecting one event", view => view.ProjectAsync(new ProjectionState(Array.Empty<string>()), [new ProjectionEvent()]).AsTask())
        .Then("handlers run by order", state =>
            ScenarioExpect.Equal(["first", "second"], state.Steps))
        .AssertPassed();

    [Scenario("Materialized view validates configuration")]
    [Fact]
    public Task Materialized_View_Validates_Configuration()
        => Given("invalid materialized view inputs", () => true)
        .Then("blank names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => MaterializedView<OrderReadModel, OrderEvent>.Create("")))
        .And("null handlers are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => MaterializedView<OrderReadModel, OrderEvent>.Create("orders")
                .WithHandler<OrderPlaced>(null!)))
        .And("empty handler sets are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => MaterializedView<OrderReadModel, OrderEvent>.Create("orders").Build()))
        .AssertPassed();

    private abstract record OrderEvent(string OrderId);

    private sealed record OrderPlaced(string OrderId) : OrderEvent(OrderId);

    private sealed record OrderPaid(string OrderId) : OrderEvent(OrderId);

    private sealed record OrderReadModel(string OrderId, string Status);

    private sealed record ProjectionEvent;

    private sealed record ProjectionState(IReadOnlyList<string> Steps)
    {
        public ProjectionState Append(string step)
        {
            var next = Steps.ToList();
            next.Add(step);
            return this with { Steps = next };
        }
    }
}
