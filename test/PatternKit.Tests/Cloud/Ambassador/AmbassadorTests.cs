using PatternKit.Cloud.Ambassador;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.Ambassador;

[Feature("Ambassador")]
public sealed class AmbassadorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Ambassador transforms requests and records telemetry around outbound calls")]
    [Fact]
    public Task Ambassador_Transforms_Requests_And_Records_Telemetry_Around_Outbound_Calls()
        => Given("an inventory ambassador", CreateAmbassador)
        .When("a request is invoked", ambassador => ambassador.Invoke(new InventoryRequest("sku-1", "tenant-a")))
        .Then("the transformed request reaches the outbound call", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("inventory-ambassador", result.AmbassadorName);
            ScenarioExpect.Equal("SKU-1", result.Response!.Sku);
            ScenarioExpect.Equal("available", result.Response.Status);
            ScenarioExpect.Contains("transform", result.Events);
            ScenarioExpect.Contains("trace", result.Events);
        })
        .AssertPassed();

    [Scenario("Ambassador applies connection policy and fallback")]
    [Fact]
    public Task Ambassador_Applies_Connection_Policy_And_Fallback()
        => Given("an ambassador with fallback behavior", CreateAmbassador)
        .When("a blocked tenant calls the ambassador", ambassador => ambassador.Invoke(new InventoryRequest("sku-1", "blocked")))
        .Then("fallback response is returned and flagged", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.True(result.UsedFallback);
            ScenarioExpect.Equal("cached", result.Response!.Status);
            ScenarioExpect.Contains("fallback", result.Events);
        })
        .AssertPassed();

    [Scenario("Ambassador validates configuration and reports failures")]
    [Fact]
    public Task Ambassador_Validates_Configuration_And_Reports_Failures()
        => Given("invalid ambassador inputs", () => true)
        .Then("invalid names and missing calls are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => Ambassador<InventoryRequest, InventoryResponse>.Create("")
                .Call(CallInventory)
                .Build());
            ScenarioExpect.Throws<InvalidOperationException>(() => Ambassador<InventoryRequest, InventoryResponse>.Create().Build());
        })
        .And("null delegates are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentNullException>(() => Ambassador<InventoryRequest, InventoryResponse>.Create().Transform(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => Ambassador<InventoryRequest, InventoryResponse>.Create().ConnectionPolicy(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => Ambassador<InventoryRequest, InventoryResponse>.Create().Telemetry("trace", null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => Ambassador<InventoryRequest, InventoryResponse>.Create().Call(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => Ambassador<InventoryRequest, InventoryResponse>.Create().Fallback(null!));
        })
        .And("duplicate telemetry names are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => Ambassador<InventoryRequest, InventoryResponse>.Create()
                .Telemetry("trace", static _ => { })
                .Telemetry("TRACE", static _ => { })))
        .And("unhandled failures are explicit", _ =>
        {
            var result = Ambassador<InventoryRequest, InventoryResponse>.Create("inventory-ambassador")
                .Call(static _ => throw new InvalidOperationException("downstream unavailable"))
                .Build()
                .Invoke(new InventoryRequest("sku-1", "tenant-a"));
            ScenarioExpect.True(result.Failed);
            ScenarioExpect.Contains("downstream unavailable", result.Exception!.Message);
        })
        .And("null requests and results are guarded", _ =>
        {
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateAmbassador().Invoke(null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => AmbassadorResult<InventoryResponse>.Success("inventory-ambassador", null!, []));
            ScenarioExpect.Throws<ArgumentNullException>(() => AmbassadorResult<InventoryResponse>.Success("inventory-ambassador", new("sku", "ok"), null!));
            ScenarioExpect.Throws<ArgumentNullException>(() => AmbassadorResult<InventoryResponse>.Failure("inventory-ambassador", null!, []));
            ScenarioExpect.Throws<ArgumentNullException>(() => AmbassadorResult<InventoryResponse>.Failure("inventory-ambassador", new InvalidOperationException(), null!));
        })
        .AssertPassed();

    private static Ambassador<InventoryRequest, InventoryResponse> CreateAmbassador()
        => Ambassador<InventoryRequest, InventoryResponse>.Create("inventory-ambassador")
            .Transform(static request => request with { Sku = request.Sku.ToUpperInvariant() })
            .ConnectionPolicy(static request => request.Tenant != "blocked")
            .Telemetry("trace", static ctx => ctx.Items["traceId"] = ctx.Request.Tenant)
            .Call(CallInventory)
            .Fallback(static ctx => new(ctx.Request.Sku, "cached"))
            .Build();

    private static InventoryResponse CallInventory(AmbassadorContext<InventoryRequest> ctx)
        => new(ctx.Request.Sku, "available");

    private sealed record InventoryRequest(string Sku, string Tenant);

    private sealed record InventoryResponse(string Sku, string Status);
}
