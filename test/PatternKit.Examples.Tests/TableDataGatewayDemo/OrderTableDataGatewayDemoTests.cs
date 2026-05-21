using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.TableDataGatewayDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.TableDataGatewayDemo;

[Feature("Order Table Data Gateway demo")]
public sealed partial class OrderTableDataGatewayDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Order Table Data Gateway demo manages order rows")]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Order_Table_Data_Gateway_Demo_Manages_Order_Rows(bool sourceGenerated)
        => Given("the order table data gateway demo", () => sourceGenerated)
        .When("the selected path runs", (Func<bool, ValueTask<OrderTableGatewaySummary>>)(async generated =>
            generated
                ? await OrderTableDataGatewayDemo.RunGeneratedAsync()
                : await OrderTableDataGatewayDemo.RunFluentAsync()))
        .Then("the gateway queries closed orders", summary =>
        {
            ScenarioExpect.Equal("orders", summary.TableName);
            ScenarioExpect.Equal(1, summary.ClosedOrderCount);
            ScenarioExpect.False(string.IsNullOrWhiteSpace(summary.FirstClosedOrderId));
        })
        .AssertPassed();

    [Scenario("Order Table Data Gateway demo is importable through IServiceCollection")]
    [Fact]
    public Task Order_Table_Data_Gateway_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service provider with the order table data gateway demo", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderTableDataGatewayDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("a scoped workflow closes an order row", (Func<ServiceProvider, ValueTask<OrderTableGatewaySummary>>)(async provider =>
        {
            using (provider)
            using (var scope = provider.CreateScope())
            {
                var workflow = scope.ServiceProvider.GetRequiredService<OrderTableGatewayWorkflow>();
                return await workflow.CloseAsync("order-300", "customer-3", 50m);
            }
        }))
        .Then("the imported gateway handles the row workflow", summary =>
        {
            ScenarioExpect.Equal("orders", summary.TableName);
            ScenarioExpect.Equal("order-300", summary.FirstClosedOrderId);
            ScenarioExpect.Equal(1, summary.ClosedOrderCount);
        })
        .AssertPassed();
}
