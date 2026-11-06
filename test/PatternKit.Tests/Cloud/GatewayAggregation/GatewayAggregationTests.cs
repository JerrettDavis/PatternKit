using PatternKit.Cloud.GatewayAggregation;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.GatewayAggregation;

[Feature("Gateway Aggregation")]
public sealed class GatewayAggregationTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Gateway aggregation composes downstream results")]
    [Fact]
    public Task Gateway_Aggregation_Composes_Downstream_Results()
        => Given("a customer dashboard gateway aggregation", CreateAggregation)
        .When("a dashboard request is aggregated", gateway => gateway.Aggregate(new DashboardRequest("C-100")))
        .Then("the response combines downstream parts", result =>
        {
            ScenarioExpect.True(result.Aggregated);
            ScenarioExpect.Equal("customer-dashboard", result.GatewayName);
            ScenarioExpect.Equal("C-100", result.Response!.CustomerId);
            ScenarioExpect.Equal(2, result.Response.OpenOrders);
            ScenarioExpect.True(result.Parts["profile"].Succeeded);
        })
        .AssertPassed();

    [Scenario("Gateway aggregation reports downstream and composer failures")]
    [Fact]
    public Task Gateway_Aggregation_Reports_Downstream_And_Composer_Failures()
        => Given("a gateway with a failing downstream fetch", () => GatewayAggregation<DashboardRequest, DashboardResponse>.Create("customer-dashboard")
            .Fetch<CustomerProfile>("profile", static request => new(request.CustomerId, "Ada"))
            .Fetch<OrderSummary>("orders", static _ => throw new InvalidOperationException("orders unavailable"))
            .Compose(static ctx => new(ctx.Require<CustomerProfile>("profile").CustomerId, ctx.Require<OrderSummary>("orders").OpenOrders))
            .Build())
        .When("the request is aggregated", gateway => gateway.Aggregate(new DashboardRequest("C-100")))
        .Then("the failed part and failed aggregate are explicit", result =>
        {
            ScenarioExpect.True(result.Failed);
            ScenarioExpect.True(result.Parts["orders"].Failed);
            ScenarioExpect.Contains("orders", result.Exception!.Message);
        })
        .AssertPassed();

    [Scenario("Gateway aggregation validates configuration")]
    [Fact]
    public Task Gateway_Aggregation_Validates_Configuration()
        => Given("invalid gateway aggregation inputs", () => true)
        .Then("invalid names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => GatewayAggregation<DashboardRequest, DashboardResponse>.Create("")
                .Fetch<CustomerProfile>("profile", FetchProfile)
                .Compose(Compose)
                .Build()))
        .And("missing fetches are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => GatewayAggregation<DashboardRequest, DashboardResponse>.Create().Compose(Compose).Build()))
        .And("missing composers are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => GatewayAggregation<DashboardRequest, DashboardResponse>.Create().Fetch<CustomerProfile>("profile", FetchProfile).Build()))
        .And("duplicate fetch names are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => GatewayAggregation<DashboardRequest, DashboardResponse>.Create()
                .Fetch<CustomerProfile>("profile", FetchProfile)
                .Fetch<CustomerProfile>("PROFILE", FetchProfile)))
        .And("null requests are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateAggregation().Aggregate(null!)))
        .AssertPassed();

    private static GatewayAggregation<DashboardRequest, DashboardResponse> CreateAggregation()
        => GatewayAggregation<DashboardRequest, DashboardResponse>.Create("customer-dashboard")
            .Fetch<CustomerProfile>("profile", FetchProfile)
            .Fetch<OrderSummary>("orders", static request => new(request.CustomerId, 2))
            .Compose(Compose)
            .Build();

    private static CustomerProfile FetchProfile(DashboardRequest request) => new(request.CustomerId, "Ada");

    private static DashboardResponse Compose(GatewayAggregationContext<DashboardRequest> ctx)
        => new(ctx.Require<CustomerProfile>("profile").CustomerId, ctx.Require<OrderSummary>("orders").OpenOrders);

    private sealed record DashboardRequest(string CustomerId);

    private sealed record CustomerProfile(string CustomerId, string Name);

    private sealed record OrderSummary(string CustomerId, int OpenOrders);

    private sealed record DashboardResponse(string CustomerId, int OpenOrders);
}
