using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.TransactionScriptDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.TransactionScriptDemo;

[Feature("Order Transaction Script demo")]
public sealed partial class OrderTransactionScriptDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated transaction scripts submit orders")]
    [Fact]
    public Task Fluent_And_Generated_Transaction_Scripts_Submit_Orders()
        => Given("the order transaction script demo", () => true)
        .When("fluent and generated scripts run", (Func<bool, ValueTask<OrderTransactionScriptResults>>)(async _ => new OrderTransactionScriptResults(
            await OrderTransactionScriptDemo.RunFluentAsync(),
            await OrderTransactionScriptDemo.RunGeneratedAsync())))
        .Then("orders are submitted and persisted", result =>
        {
            ScenarioExpect.True(result.Fluent.Submitted);
            ScenarioExpect.True(result.Generated.Submitted);
            ScenarioExpect.Equal(1, result.Fluent.RepositoryCount);
            ScenarioExpect.Equal(1, result.Generated.RepositoryCount);
        })
        .AssertPassed();

    [Scenario("Transaction Script demo registers with IServiceCollection")]
    [Fact]
    public Task Transaction_Script_Demo_Registers_With_IServiceCollection()
        => Given("a service collection with the transaction script demo", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderTransactionScriptDemo();
            return services.BuildServiceProvider();
        })
        .When("the scoped workflow submits an order", (Func<ServiceProvider, ValueTask<OrderTransactionScriptSummary>>)(async provider =>
        {
            using var scope = provider.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<OrderTransactionScriptWorkflow>();
            return await workflow.SubmitAsync(new SubmitOrderRequest("order-300", "customer-30", 45m));
        }))
        .Then("the workflow uses the registered script", summary =>
        {
            ScenarioExpect.True(summary.Submitted);
            ScenarioExpect.Equal("order-300", summary.OrderId);
            ScenarioExpect.Equal(1, summary.RepositoryCount);
        })
        .AssertPassed();

    private sealed record OrderTransactionScriptResults(
        OrderTransactionScriptSummary Fluent,
        OrderTransactionScriptSummary Generated);
}
