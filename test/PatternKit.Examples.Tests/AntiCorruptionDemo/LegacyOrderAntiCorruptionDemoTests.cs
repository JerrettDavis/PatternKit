using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.AntiCorruptionDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.AntiCorruptionDemo;

[Feature("Legacy order anti-corruption demo")]
public sealed class LegacyOrderAntiCorruptionDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent anti-corruption layer imports valid legacy orders")]
    [Fact]
    public Task Fluent_Anti_Corruption_Layer_Imports_Valid_Legacy_Orders()
        => Given("a legacy order import service using the fluent layer", () =>
            new LegacyOrderImportService(new ScriptedLegacyOrderFeed(), LegacyOrderAntiCorruptionPolicies.CreateFluentLayer()))
            .When("importing a valid legacy order", service =>
                service.Import(new LegacyOrderDto(" ORD-100 ", 125m, "USD", " cust-42 ")))
            .Then("the domain order is accepted with normalized identifiers", result =>
                result.Accepted
                && result.Order is not null
                && result.Order.OrderId == "ORD-100"
                && result.Order.CustomerId == "CUST-42")
            .AssertPassed();

    [Scenario("Generated anti-corruption layer rejects legacy model drift")]
    [Fact]
    public Task Generated_Anti_Corruption_Layer_Rejects_Legacy_Model_Drift()
        => Given("a legacy order import service using the generated layer", () =>
            new LegacyOrderImportService(new ScriptedLegacyOrderFeed(), GeneratedLegacyOrderAntiCorruptionLayer.CreateGeneratedLayer()))
            .When("importing an order with an unsupported currency", service =>
                service.Import(new LegacyOrderDto("ORD-100", 125m, "EUR", "CUST-42")))
            .Then("the import is rejected before the domain model is exposed", result =>
                result.Rejected
                && result.Order is null
                && result.RejectionReason == "Only USD orders are imported.")
            .AssertPassed();

    [Scenario("Legacy order anti-corruption demo is importable through IServiceCollection")]
    [Fact]
    public Task Legacy_Order_Anti_Corruption_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service collection importing the anti-corruption demo", () =>
            {
                var services = new ServiceCollection();
                services.AddLegacyOrderAntiCorruptionDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and using the registered import service", RunImportedDemoAsync)
            .Then("the DI-owned generated layer imports the order", result =>
                result.Accepted && result.Order?.CustomerId == "CUST-42")
            .AssertPassed();

    private static async Task<OrderImportResult> RunImportedDemoAsync(ServiceProvider provider)
    {
        using (provider)
        {
            var service = provider.GetRequiredService<LegacyOrderImportService>();
            return await service.ImportAsync("ORD-100");
        }
    }
}
