using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.EventCarriedStateTransferDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.EventCarriedStateTransferDemo;

[Feature("Inventory Event-Carried State Transfer example")]
public sealed class InventoryEventCarriedStateTransferDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated paths project carried inventory state")]
    [Fact]
    public Task Fluent_And_Generated_Paths_Project_Carried_Inventory_State()
        => Given("an inventory event", () => new InventoryAdjustedEvent("SKU-100", 12, "CHI-01", 4))
        .When("fluent and generated transfers project the event", evt => new
        {
            Fluent = InventoryEventCarriedStateTransferDemoRunner.RunFluent(),
            Generated = BuildServiceProvider().GetRequiredService<InventoryEventCarriedStateTransferDemoRunner>().RunGenerated(evt)
        })
        .Then("both paths update the read model without a source service callback", result =>
        {
            ScenarioExpect.Equal("SKU-100", result.Fluent.Sku);
            ScenarioExpect.Equal(12, result.Fluent.QuantityOnHand);
            ScenarioExpect.Equal("CHI-01", result.Generated.Warehouse);
            ScenarioExpect.Equal(4L, result.Generated.Version);
        })
        .AssertPassed();

    [Scenario("Inventory state transfer is importable through AddPatternKitExamples")]
    [Fact]
    public Task Inventory_State_Transfer_Is_Importable_Through_AddPatternKitExamples()
        => Given("the aggregate PatternKit example registration", BuildAggregateProvider)
        .When("the inventory state transfer example is resolved", provider => provider.GetRequiredService<InventoryEventCarriedStateTransferExample>())
        .Then("the runner and service are available through standard IoC", example =>
        {
            var summary = example.Runner.RunGenerated(new InventoryAdjustedEvent("SKU-200", 25, "DFW-02", 9));
            ScenarioExpect.Equal("SKU-200", summary.Sku);
            ScenarioExpect.Equal(25, summary.QuantityOnHand);
            ScenarioExpect.NotNull(example.Service);
        })
        .AssertPassed();

    private static ServiceProvider BuildServiceProvider()
        => new ServiceCollection()
            .AddInventoryEventCarriedStateTransferDemo()
            .BuildServiceProvider();

    private static ServiceProvider BuildAggregateProvider()
        => new ServiceCollection()
            .AddPatternKitExamples()
            .BuildServiceProvider();
}
