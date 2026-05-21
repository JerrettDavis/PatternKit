using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.EventSourcingDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.EventSourcingDemo;

[Feature("Order Event Sourcing demo")]
public sealed partial class OrderEventSourcingDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Order Event Sourcing demo replays an order stream")]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Order_Event_Sourcing_Demo_Replays_An_Order_Stream(bool sourceGenerated)
        => Given("the order event sourcing demo", () => sourceGenerated)
        .When("the selected path runs", (Func<bool, ValueTask<OrderEventSourcingSummary>>)(async generated =>
            generated
                ? await OrderEventSourcingDemo.RunGeneratedAsync()
                : await OrderEventSourcingDemo.RunFluentAsync()))
        .Then("the replayed order is paid", summary =>
        {
            ScenarioExpect.Equal("order-events", summary.StoreName);
            ScenarioExpect.Equal(125m, summary.Total);
            ScenarioExpect.True(summary.Paid);
            ScenarioExpect.Equal(2, summary.Version);
            ScenarioExpect.False(string.IsNullOrWhiteSpace(summary.OrderId));
        })
        .AssertPassed();

    [Scenario("Order Event Sourcing demo is importable through IServiceCollection")]
    [Fact]
    public Task Order_Event_Sourcing_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service provider with the order event sourcing demo", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderEventSourcingDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("a scoped workflow places and pays an order", (Func<ServiceProvider, ValueTask<OrderEventSourcingSummary>>)(async provider =>
        {
            using (provider)
            using (var scope = provider.CreateScope())
            {
                var workflow = scope.ServiceProvider.GetRequiredService<OrderEventSourcingWorkflow>();
                return await workflow.PlaceAndPayAsync("order-300", "customer-3", 50m, "payment-3");
            }
        }))
        .Then("the imported event store replays the order state", summary =>
        {
            ScenarioExpect.Equal("order-events", summary.StoreName);
            ScenarioExpect.Equal("order-300", summary.OrderId);
            ScenarioExpect.Equal("customer-3", summary.CustomerId);
            ScenarioExpect.Equal(50m, summary.Total);
            ScenarioExpect.True(summary.Paid);
            ScenarioExpect.Equal(2, summary.Version);
        })
        .AssertPassed();
}
