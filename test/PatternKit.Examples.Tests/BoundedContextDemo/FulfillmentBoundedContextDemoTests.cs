using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.BoundedContextDemo;
using PatternKit.Examples.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.BoundedContextDemo;

[Feature("Fulfillment bounded context example")]
public sealed class FulfillmentBoundedContextDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated bounded context descriptors match")]
    [Fact]
    public Task Fluent_And_Generated_Bounded_Context_Descriptors_Match()
        => Given("fluent and generated fulfillment context descriptors", () => new
        {
            Fluent = FulfillmentBoundedContextDemo.CreateFluentDescriptor(),
            Generated = FulfillmentBoundedContextDemo.CreateGeneratedDescriptor()
        })
            .Then("context names match", ctx =>
                ScenarioExpect.Equal(ctx.Fluent.Name, ctx.Generated.Name))
            .And("capability names match", ctx =>
                ScenarioExpect.Equal(
                    ctx.Fluent.Capabilities.Select(static capability => capability.Name).ToArray(),
                    ctx.Generated.Capabilities.Select(static capability => capability.Name).ToArray()))
            .And("adapter boundaries match", ctx =>
                ScenarioExpect.Equal(
                    ctx.Fluent.Adapters.Select(static adapter => $"{adapter.UpstreamContext}->{adapter.DownstreamContext}").ToArray(),
                    ctx.Generated.Adapters.Select(static adapter => $"{adapter.UpstreamContext}->{adapter.DownstreamContext}").ToArray()))
            .AssertPassed();

    [Scenario("Fulfillment bounded context imports through IServiceCollection")]
    [Fact]
    public Task Fulfillment_Bounded_Context_Imports_Through_IServiceCollection()
        => Given("services with the fulfillment bounded context example", () =>
            {
                var services = new ServiceCollection();
                services.AddFulfillmentBoundedContextDemo();
                return services.BuildServiceProvider();
            })
            .When("planning fulfillment for a catalog product", sp => sp.GetRequiredService<FulfillmentBoundedContextDemo.FulfillmentPlanner>()
                .Plan(new FulfillmentBoundedContextDemo.CatalogProduct("SKU-1", 42m)))
            .Then("the plan uses the bounded context services", plan =>
            {
                ScenarioExpect.Equal("SKU-1", plan.Sku);
                ScenarioExpect.Equal("freight", plan.Carrier);
                ScenarioExpect.True(plan.InventoryReserved);
            })
            .AssertPassed();

    [Scenario("Fulfillment bounded context is included in aggregate examples")]
    [Fact]
    public Task Fulfillment_Bounded_Context_Is_Included_In_Aggregate_Examples()
        => Given("all PatternKit examples registered in DI", () =>
            {
                var services = new ServiceCollection();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider();
            })
            .Then("the bounded context wrapper resolves", sp =>
                ScenarioExpect.NotNull(sp.GetRequiredService<FulfillmentBoundedContextPatternExample>()))
            .And("the generated descriptor resolves", sp =>
                ScenarioExpect.Equal("Fulfillment", sp.GetRequiredService<PatternKit.Application.BoundedContexts.BoundedContextDescriptor>().Name))
            .AssertPassed();
}
