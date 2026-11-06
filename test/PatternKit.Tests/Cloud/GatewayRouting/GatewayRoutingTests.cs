using PatternKit.Cloud.GatewayRouting;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.GatewayRouting;

[Feature("Gateway Routing")]
public sealed class GatewayRoutingTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Gateway routing dispatches requests to the first matching downstream")]
    [Fact]
    public Task Gateway_Routing_Dispatches_Requests_To_First_Matching_Downstream()
        => Given("a product gateway router", CreateRouter)
        .When("an inventory request is routed", router => router.Route(new GatewayRequest("/inventory/SKU-100")))
        .Then("the inventory route handles the request", result =>
        {
            ScenarioExpect.True(result.MatchedRoute);
            ScenarioExpect.Equal("inventory", result.RouteName);
            ScenarioExpect.Equal("inventory:/inventory/SKU-100", result.Response.Body);
        })
        .AssertPassed();

    [Scenario("Gateway routing preserves ordered route precedence")]
    [Fact]
    public Task Gateway_Routing_Preserves_Ordered_Route_Precedence()
        => Given("a router with overlapping predicates", () => GatewayRouting<GatewayRequest, GatewayResponse>.Create("ordered")
            .Route("first", static request => request.Path.StartsWith("/inventory/", StringComparison.OrdinalIgnoreCase), static request => new($"first:{request.Path}"))
            .Route("second", static request => request.Path.Contains("SKU", StringComparison.OrdinalIgnoreCase), static request => new($"second:{request.Path}"))
            .Fallback("not-found", Fallback)
            .Build())
        .When("a request matches both routes", router => router.Route(new GatewayRequest("/inventory/SKU-100")))
        .Then("the first route wins", result =>
        {
            ScenarioExpect.Equal("ordered", result.GatewayName);
            ScenarioExpect.Equal("first", result.RouteName);
            ScenarioExpect.False(result.Fallback);
        })
        .AssertPassed();

    [Scenario("Gateway routing uses fallback when no route matches")]
    [Fact]
    public Task Gateway_Routing_Uses_Fallback_When_No_Route_Matches()
        => Given("a product gateway router", CreateRouter)
        .When("an unknown request is routed", router => router.Route(new GatewayRequest("/unknown")))
        .Then("the fallback route handles the request", result =>
        {
            ScenarioExpect.True(result.Fallback);
            ScenarioExpect.Equal("not-found", result.RouteName);
            ScenarioExpect.Equal("fallback:/unknown", result.Response.Body);
        })
        .AssertPassed();

    [Scenario("Gateway routing validates configuration")]
    [Fact]
    public Task Gateway_Routing_Validates_Configuration()
        => Given("invalid Gateway Routing inputs", () => true)
        .Then("invalid names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => GatewayRouting<GatewayRequest, GatewayResponse>.Create("")
                .Route("inventory", IsInventory, Inventory)
                .Fallback("not-found", Fallback)
                .Build()))
        .And("missing routes are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => GatewayRouting<GatewayRequest, GatewayResponse>.Create().Fallback("not-found", Fallback).Build()))
        .And("missing fallbacks are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => GatewayRouting<GatewayRequest, GatewayResponse>.Create().Route("inventory", IsInventory, Inventory).Build()))
        .And("duplicate route names are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => GatewayRouting<GatewayRequest, GatewayResponse>.Create()
                .Route("inventory", IsInventory, Inventory)
                .Route("INVENTORY", IsInventory, Inventory)))
        .And("null route delegates are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentNullException>(() => GatewayRouting<GatewayRequest, GatewayResponse>.Create().Route("inventory", null!, Inventory));
            ScenarioExpect.Throws<ArgumentNullException>(() => GatewayRouting<GatewayRequest, GatewayResponse>.Create().Route("inventory", IsInventory, null!));
        })
        .And("invalid fallback inputs are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => GatewayRouting<GatewayRequest, GatewayResponse>.Create().Fallback("", Fallback));
            ScenarioExpect.Throws<ArgumentNullException>(() => GatewayRouting<GatewayRequest, GatewayResponse>.Create().Fallback("not-found", null!));
        })
        .And("null requests are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateRouter().Route(null!)))
        .AssertPassed();

    private static GatewayRouting<GatewayRequest, GatewayResponse> CreateRouter()
        => GatewayRouting<GatewayRequest, GatewayResponse>.Create("product-gateway")
            .Route("inventory", IsInventory, Inventory)
            .Route("pricing", static request => request.Path.StartsWith("/pricing/", StringComparison.OrdinalIgnoreCase), static request => new($"pricing:{request.Path}"))
            .Fallback("not-found", Fallback)
            .Build();

    private static bool IsInventory(GatewayRequest request) => request.Path.StartsWith("/inventory/", StringComparison.OrdinalIgnoreCase);

    private static GatewayResponse Inventory(GatewayRequest request) => new($"inventory:{request.Path}");

    private static GatewayResponse Fallback(GatewayRequest request) => new($"fallback:{request.Path}");

    private sealed record GatewayRequest(string Path);

    private sealed record GatewayResponse(string Body);
}
