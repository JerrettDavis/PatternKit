using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.ContextMaps;
using PatternKit.Examples.ContextMapDemo;
using PatternKit.Examples.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ContextMapDemo;

[Feature("Commerce context map example")]
public sealed class CommerceContextMapDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated context maps match")]
    [Fact]
    public Task Fluent_And_Generated_Context_Maps_Match()
        => Given("fluent and generated context maps", () => new
        {
            Fluent = CommerceContextMapDemo.CreateFluentMap(),
            Generated = CommerceContextMapDemo.CreateGeneratedMap()
        })
            .Then("map names match", ctx =>
                ScenarioExpect.Equal(ctx.Fluent.Name, ctx.Generated.Name))
            .And("relationship contracts match", ctx =>
                ScenarioExpect.Equal(
                    ctx.Fluent.Relationships.Select(static item => item.ContractName).ToArray(),
                    ctx.Generated.Relationships.Select(static item => item.ContractName).ToArray()))
            .AssertPassed();

    [Scenario("Commerce context map imports through IServiceCollection")]
    [Fact]
    public Task Commerce_Context_Map_Imports_Through_IServiceCollection()
        => Given("services with the commerce context map example", () =>
            {
                var services = new ServiceCollection();
                services.AddCommerceContextMapDemo();
                return services.BuildServiceProvider();
            })
            .When("summarizing the context map and translating catalog products", sp =>
            {
                var summary = sp.GetRequiredService<CommerceContextMapDemo.CommerceContextMapReporter>().Summarize();
                var translator = sp.GetRequiredService<CommerceContextMapDemo.CatalogToFulfillmentTranslator>();
                var product = translator.Translate(new CommerceContextMapDemo.CatalogProduct("SKU-42", "Trail Jacket"));
                var shipment = new CommerceContextMapDemo.BillingShipment("SHIP-42", 9.95m);
                return new { Summary = summary, Product = product, Shipment = shipment };
            })
            .Then("the summary reflects the production relationships", result =>
            {
                ScenarioExpect.Equal(2, result.Summary.RelationshipCount);
                ScenarioExpect.True(result.Summary.HasPublishedLanguage);
                ScenarioExpect.True(result.Summary.HasCustomerSupplier);
            })
            .And("the translator maps catalog language into fulfillment language", result =>
            {
                ScenarioExpect.Equal("SKU-42", result.Product.Sku);
                ScenarioExpect.Equal("Trail Jacket", result.Product.Description);
                ScenarioExpect.Equal("SHIP-42", result.Shipment.ShipmentId);
                ScenarioExpect.Equal(9.95m, result.Shipment.Charge);
            })
            .AssertPassed();

    [Scenario("Commerce context map is included in aggregate examples")]
    [Fact]
    public Task Commerce_Context_Map_Is_Included_In_Aggregate_Examples()
        => Given("all PatternKit examples registered in DI", () =>
            {
                var services = new ServiceCollection();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider();
            })
            .Then("the context map wrapper resolves", sp =>
                ScenarioExpect.NotNull(sp.GetRequiredService<CommerceContextMapPatternExample>()))
            .And("the generated descriptor resolves", sp =>
                ScenarioExpect.Equal("Commerce", sp.GetServices<ContextMapDescriptor>().Single().Name))
            .AssertPassed();
}
