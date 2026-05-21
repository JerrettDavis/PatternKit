using PatternKit.Examples.MaterializedViewDemo;
using Microsoft.Extensions.DependencyInjection;
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

    [Scenario("Materialized view workflow is importable through IServiceCollection")]
    [Fact]
    public Task Materialized_View_Workflow_Is_Importable_Through_IServiceCollection()
        => Given("an importing app service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderMaterializedViewDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving and running the workflow", provider =>
        {
            using (provider)
            {
                var workflow = provider.GetRequiredService<OrderMaterializedViewWorkflow>();
                return workflow.BuildReadModelAsync([
                    new OrderPlacedForReadModel("order-di", "customer-di", 42m, DateTimeOffset.UtcNow),
                    new OrderPaymentCapturedForReadModel("order-di", "payment-di", DateTimeOffset.UtcNow)
                ]).AsTask();
            }
        })
        .Then("the workflow projects the supplied events", summary =>
        {
            ScenarioExpect.Equal("order-di", summary.OrderId);
            ScenarioExpect.Equal("Paid", summary.Status);
            ScenarioExpect.Equal(2, summary.ChangeCount);
        })
        .AssertPassed();
}
