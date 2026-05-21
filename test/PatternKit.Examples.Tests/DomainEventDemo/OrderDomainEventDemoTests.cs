using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DomainEventDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.DomainEventDemo;

[Feature("Order Domain Event demo")]
public sealed partial class OrderDomainEventDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Order Domain Event demo dispatches events")]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Order_Domain_Event_Demo_Dispatches_Events(bool sourceGenerated)
        => Given("the order domain event demo", () => sourceGenerated)
        .When("the selected path runs", (Func<bool, ValueTask<OrderDomainEventSummary>>)(async generated =>
            generated
                ? await OrderDomainEventDemo.RunGeneratedAsync()
                : await OrderDomainEventDemo.RunFluentAsync()))
        .Then("the event updates projection and audit state", summary =>
        {
            ScenarioExpect.True(summary.Dispatched);
            ScenarioExpect.Single(summary.ProjectedOrderIds);
            ScenarioExpect.Single(summary.AuditEntries);
        })
        .AssertPassed();

    [Scenario("Order Domain Event demo is importable through IServiceCollection")]
    [Fact]
    public Task Order_Domain_Event_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service provider with the order domain event demo", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderDomainEventDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("a scoped workflow places an order", (Func<ServiceProvider, ValueTask<OrderDomainEventSummary>>)(async provider =>
        {
            using (provider)
            using (var scope = provider.CreateScope())
            {
                var workflow = scope.ServiceProvider.GetRequiredService<OrderDomainEventWorkflow>();
                return await workflow.PlaceAsync("order-300", "customer-3", 50m);
            }
        }))
        .Then("the imported dispatcher handles the event", summary =>
        {
            ScenarioExpect.True(summary.Dispatched);
            ScenarioExpect.Equal("order-300", ScenarioExpect.Single(summary.ProjectedOrderIds));
            ScenarioExpect.Equal("placed:order-300:50", ScenarioExpect.Single(summary.AuditEntries));
        })
        .AssertPassed();
}
