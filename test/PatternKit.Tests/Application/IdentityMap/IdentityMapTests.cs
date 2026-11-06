using PatternKit.Application.IdentityMap;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.IdentityMap;

[Feature("Identity Map")]
public sealed partial class IdentityMapTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Identity Map reuses object instances for the same key")]
    [Fact]
    public Task Identity_Map_Reuses_Object_Instances_For_The_Same_Key()
        => Given("an identity map", () => PatternKit.Application.IdentityMap.IdentityMap<Order, string>.Create(static order => order.Id).Build())
            .When("loading the same key twice", map =>
            {
                var first = map.GetOrAdd("order-100", static id => new Order(id, 125m));
                var second = map.GetOrAdd("order-100", static id => new Order(id, 999m));
                return new { Map = map, First = first, Second = second };
            })
            .Then("the same instance is returned", result =>
            {
                ScenarioExpect.True(ReferenceEquals(result.First, result.Second));
                ScenarioExpect.Equal(1, result.Map.Count);
                ScenarioExpect.Equal(125m, result.Second.Total);
            })
            .AssertPassed();

    [Scenario("Identity Map rejects duplicate keys with different instances")]
    [Fact]
    public Task Identity_Map_Rejects_Duplicate_Keys_With_Different_Instances()
        => Given("an identity map tracking one entity", () =>
            {
                var map = PatternKit.Application.IdentityMap.IdentityMap<Order, string>.Create(static order => order.Id).Build();
                var tracked = map.Track(new Order("order-100", 125m));
                return new { Map = map, Tracked = tracked };
            })
            .When("a different instance with the same key is tracked", ctx => ctx.Map.Track(new Order("order-100", 200m)))
            .Then("the duplicate is rejected", result =>
            {
                ScenarioExpect.Equal(IdentityMapStatus.Conflict, result.Status);
                ScenarioExpect.False(result.Succeeded);
            })
            .AssertPassed();

    [Scenario("Identity Map returns existing result when tracking the same instance again")]
    [Fact]
    public Task Identity_Map_Returns_Existing_Result_When_Tracking_The_Same_Instance_Again()
        => Given("an identity map and an order instance", () =>
            {
                var map = PatternKit.Application.IdentityMap.IdentityMap<Order, string>.Create(static order => order.Id).Build();
                var order = new Order("order-100", 125m);
                var tracked = map.Track(order);
                return new { Map = map, Order = order, Tracked = tracked };
            })
            .When("the same instance is tracked again", ctx => ctx.Map.Track(ctx.Order))
            .Then("the existing entity is returned as a successful result", result =>
            {
                ScenarioExpect.Equal(IdentityMapStatus.Existing, result.Status);
                ScenarioExpect.True(result.Succeeded);
                ScenarioExpect.Null(result.Reason);
            })
            .AssertPassed();

    [Scenario("Identity Map reads tracked entities by key")]
    [Fact]
    public Task Identity_Map_Reads_Tracked_Entities_By_Key()
        => Given("an identity map with a tracked entity", () =>
            {
                var map = PatternKit.Application.IdentityMap.IdentityMap<Order, string>.Create().Build();
                var order = new Order("order-100", 125m);
                var result = map.Track("order-100", order);
                return new { Map = map, Order = order, Result = result };
            })
            .Then("the entity can be retrieved and the result reports a new track", ctx =>
            {
                ScenarioExpect.Equal(IdentityMapStatus.Tracked, ctx.Result.Status);
                ScenarioExpect.True(ctx.Result.Succeeded);
                ScenarioExpect.Null(ctx.Result.Reason);
                ScenarioExpect.True(ReferenceEquals(ctx.Order, ctx.Map.Get("order-100")));
                ScenarioExpect.Null(ctx.Map.Get("missing"));
            })
            .AssertPassed();

    [Scenario("Identity Map can remove and clear tracked entities")]
    [Fact]
    public Task Identity_Map_Can_Remove_And_Clear_Tracked_Entities()
        => Given("an identity map with two entities", () =>
            {
                var map = PatternKit.Application.IdentityMap.IdentityMap<Order, string>.Create().Build();
                map.Track("order-100", new Order("order-100", 125m));
                map.Track("order-101", new Order("order-101", 25m));
                return map;
            })
            .Then("entities can be removed and cleared", map =>
            {
                ScenarioExpect.True(map.Remove("order-100"));
                ScenarioExpect.Equal(1, map.Count);
                map.Clear();
                ScenarioExpect.Equal(0, map.Count);
            })
            .AssertPassed();

    [Scenario("Identity Map requires a selector for implicit tracking")]
    [Fact]
    public Task Identity_Map_Requires_A_Selector_For_Implicit_Tracking()
        => Given("an identity map without a key selector", () => PatternKit.Application.IdentityMap.IdentityMap<Order, string>.Create().Build())
            .Then("implicit tracking is rejected", map =>
                ScenarioExpect.Throws<InvalidOperationException>(() => map.Track(new Order("order-100", 125m))))
            .AssertPassed();

    [Scenario("Identity Map validates required arguments")]
    [Fact]
    public Task Identity_Map_Validates_Required_Arguments()
        => Given("an identity map", () => PatternKit.Application.IdentityMap.IdentityMap<Order, string>.Create().Build())
            .Then("null arguments are rejected", map =>
            {
                ScenarioExpect.Throws<ArgumentNullException>(() => map.Get(null!));
                ScenarioExpect.Throws<ArgumentNullException>(() => map.Track(null!, new Order("order-100", 125m)));
                ScenarioExpect.Throws<ArgumentNullException>(() => map.Track("order-100", null!));
                ScenarioExpect.Throws<ArgumentNullException>(() => map.GetOrAdd(null!, static id => new Order(id, 125m)));
                ScenarioExpect.Throws<ArgumentNullException>(() => map.GetOrAdd("order-100", null!));
                ScenarioExpect.Throws<ArgumentNullException>(() => map.Remove(null!));
                ScenarioExpect.Throws<ArgumentNullException>(() => PatternKit.Application.IdentityMap.IdentityMap<Order, string>.Create().UseComparer(null!));
            })
            .AssertPassed();

    [Scenario("Identity Map conflict result requires a reason")]
    [Fact]
    public Task Identity_Map_Conflict_Result_Requires_A_Reason()
        => Given("an order", () => new Order("order-100", 125m))
            .Then("blank conflict reasons are rejected", order =>
            {
                ScenarioExpect.Throws<ArgumentException>(() => IdentityMapResult<Order>.Conflict(order, ""));
                var conflict = IdentityMapResult<Order>.Conflict(order, "already tracked");
                ScenarioExpect.Equal(IdentityMapStatus.Conflict, conflict.Status);
                ScenarioExpect.False(conflict.Succeeded);
                ScenarioExpect.Equal("already tracked", conflict.Reason);
            })
            .AssertPassed();

    private sealed record Order(string Id, decimal Total);
}
