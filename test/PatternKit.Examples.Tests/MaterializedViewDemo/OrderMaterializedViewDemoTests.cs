using PatternKit.Examples.MaterializedViewDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.MaterializedViewDemo;

[Feature("Order materialized view example")]
public sealed class OrderMaterializedViewDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent materialized view builds a production read model")]
    [Fact]
    public Task Fluent_Materialized_View_Builds_A_Production_Read_Model()
        => Given("the fluent order materialized view example", () => OrderMaterializedViewDemo.RunFluentAsync().AsTask())
        .Then("the final read model is shipped", summary =>
        {
            ScenarioExpect.Equal("order-read-model", summary.ViewName);
            ScenarioExpect.Equal("Shipped", summary.Status);
            ScenarioExpect.Equal(3, summary.ChangeCount);
        })
        .AssertPassed();

    [Scenario("Generated materialized view builds a production read model")]
    [Fact]
    public Task Generated_Materialized_View_Builds_A_Production_Read_Model()
        => Given("the generated order materialized view example", () => OrderMaterializedViewDemo.RunGeneratedAsync().AsTask())
        .Then("the final read model is shipped", summary =>
        {
            ScenarioExpect.Equal("order-read-model", summary.ViewName);
            ScenarioExpect.Equal("Shipped", summary.Status);
            ScenarioExpect.Equal(3, summary.ChangeCount);
        })
        .AssertPassed();
}
