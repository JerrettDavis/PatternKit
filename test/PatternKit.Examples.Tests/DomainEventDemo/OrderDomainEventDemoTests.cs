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

    [Scenario("Order Domain Event demo dispatches billed events")]
    [Fact]
    public Task Order_Domain_Event_Demo_Dispatches_Billed_Events()
        => Given("fluent and generated order domain event dispatchers", () =>
        {
            var fluentProjection = new OrderEventProjection();
            var fluentAudit = new List<string>();
            GeneratedOrderDomainEvents.Projection = new OrderEventProjection();
            GeneratedOrderDomainEvents.Audit = [];

            return new BilledEventDispatchers(
                OrderDomainEventPolicies.CreateFluentDispatcher(fluentProjection, fluentAudit),
                fluentAudit,
                GeneratedOrderDomainEvents.CreateDispatcher());
        })
        .When("order billed events are dispatched", (Func<BilledEventDispatchers, ValueTask<(IReadOnlyList<string> FluentAudit, IReadOnlyList<string> GeneratedAudit)>>)(async dispatchers =>
        {
            var fluentEvent = new OrderBilled(Guid.NewGuid(), DateTimeOffset.UtcNow, "order-400", 25m);
            var generatedEvent = new OrderBilled(Guid.NewGuid(), DateTimeOffset.UtcNow, "order-500", 30m);

            await dispatchers.Fluent.DispatchAsync(fluentEvent);
            await dispatchers.Generated.DispatchAsync(generatedEvent);

            return (dispatchers.FluentAudit.ToArray(), GeneratedOrderDomainEvents.Audit.ToArray());
        }))
        .Then("both paths audit billed events", result =>
        {
            ScenarioExpect.Equal("billed:order-400:25", ScenarioExpect.Single(result.FluentAudit));
            ScenarioExpect.Equal("billed:order-500:30", ScenarioExpect.Single(result.GeneratedAudit));
        })
        .AssertPassed();

    private sealed record BilledEventDispatchers(
        PatternKit.Application.DomainEvents.DomainEventDispatcher<OrderDomainEvent> Fluent,
        List<string> FluentAudit,
        PatternKit.Application.DomainEvents.IDomainEventDispatcher<OrderDomainEvent> Generated);
}
