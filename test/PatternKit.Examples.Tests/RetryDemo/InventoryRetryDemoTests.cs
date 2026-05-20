using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.RetryDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.RetryDemo;

[Feature("Inventory retry demo")]
public sealed class InventoryRetryDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated retry policies recover inventory lookups")]
    [Fact]
    public Task Fluent_And_Generated_Retry_Policies_Recover_Inventory_Lookups()
        => Given("a transient inventory outage", static () => new
            {
                FluentClient = new ScriptedInventoryClient(
                    new InventoryResponse("SKU-42", 0, 503),
                    new InventoryResponse("SKU-42", 8, 200)),
                GeneratedClient = new ScriptedInventoryClient(
                    new InventoryResponse("SKU-42", 0, 503),
                    new InventoryResponse("SKU-42", 8, 200))
            })
            .When("checking availability through both policy paths", ctx => new
            {
                Fluent = new InventoryLookupService(ctx.FluentClient, InventoryRetryPolicies.CreateFluentPolicy())
                    .CheckAsync("SKU-42").GetAwaiter().GetResult(),
                Generated = new InventoryLookupService(ctx.GeneratedClient, GeneratedInventoryRetryPolicy.CreateGeneratedPolicy())
                    .CheckAsync("SKU-42").GetAwaiter().GetResult(),
                ctx.FluentClient.Calls,
                GeneratedCalls = ctx.GeneratedClient.Calls
            })
            .Then("both paths retry once and return the available stock", result =>
            {
                ScenarioExpect.True(result.Fluent.Available);
                ScenarioExpect.True(result.Generated.Available);
                ScenarioExpect.Equal(2, result.Fluent.Attempts);
                ScenarioExpect.Equal(2, result.Generated.Attempts);
                ScenarioExpect.Equal(8, result.Fluent.AvailableQuantity);
                ScenarioExpect.Equal(result.Fluent.AvailableQuantity, result.Generated.AvailableQuantity);
            })
            .And("both clients were called by the retry policy", result =>
            {
                ScenarioExpect.Equal(2, result.Calls);
                ScenarioExpect.Equal(2, result.GeneratedCalls);
            })
            .AssertPassed();

    [Scenario("Inventory retry demo registers with IServiceCollection")]
    [Fact]
    public Task Inventory_Retry_Demo_Registers_With_IServiceCollection()
        => Given("a standard service collection", static () =>
            {
                var services = new ServiceCollection();
                services.AddInventoryRetryDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and using the inventory lookup service", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<InventoryLookupService>().CheckAsync("SKU-42").GetAwaiter().GetResult();
            })
            .Then("the registered service uses the generated retry policy", result =>
            {
                ScenarioExpect.True(result.Available);
                ScenarioExpect.Equal(2, result.Attempts);
                ScenarioExpect.Equal(200, result.StatusCode);
            })
            .AssertPassed();
}
