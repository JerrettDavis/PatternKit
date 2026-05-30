using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.EventualConsistency;
using PatternKit.Examples.EventualConsistencyDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.EventualConsistencyDemo;

[Feature("Order Projection Consistency demo")]
public sealed partial class OrderProjectionConsistencyDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Order Projection Consistency demo tracks convergence through fluent and generated paths")]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Order_Projection_Consistency_Demo_Tracks_Convergence_Through_Fluent_And_Generated_Paths(bool sourceGenerated)
        => Given("the order projection consistency demo", () => sourceGenerated)
        .When("the selected path runs", generated =>
            generated
                ? OrderProjectionConsistencyDemo.RunGenerated()
                : OrderProjectionConsistencyDemo.RunFluent())
        .Then("the projection moves from lagging to converged", summary =>
        {
            ScenarioExpect.Equal("order-projection-consistency", summary.MonitorName);
            ScenarioExpect.Equal(EventualConsistencyStatus.Lagging, summary.InitialStatus);
            ScenarioExpect.Equal(EventualConsistencyStatus.Converged, summary.FinalStatus);
            ScenarioExpect.Equal(1, summary.FinalLag);
            ScenarioExpect.True(summary.Converged);
            ScenarioExpect.False(string.IsNullOrWhiteSpace(summary.OrderId));
        })
        .AssertPassed();

    [Scenario("Order Projection Consistency demo is importable through IServiceCollection")]
    [Fact]
    public Task Order_Projection_Consistency_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service provider with the order projection consistency demo", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderProjectionConsistencyDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("a service records projection progress", provider =>
        {
            using (provider)
            using (var scope = provider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<OrderProjectionConsistencyService>();
                return service.RecordProjectionProgress("order-300", 5, 4);
            }
        })
        .Then("the imported monitor reports the projection as converged within lag threshold", summary =>
        {
            ScenarioExpect.Equal("order-projection-consistency", summary.MonitorName);
            ScenarioExpect.Equal("order-300", summary.OrderId);
            ScenarioExpect.Equal(EventualConsistencyStatus.Converged, summary.FinalStatus);
            ScenarioExpect.Equal(1, summary.FinalLag);
            ScenarioExpect.True(summary.Converged);
        })
        .AssertPassed();

    [Scenario("Order Projection Consistency generated factory creates the configured monitor")]
    [Fact]
    public void Order_Projection_Consistency_Generated_Factory_Creates_The_Configured_Monitor()
    {
        var monitor = GeneratedOrderProjectionConsistencyMonitor.CreateMonitor();

        ScenarioExpect.Equal("order-projection-consistency", monitor.Name);
        ScenarioExpect.Equal(1, monitor.MaxAllowedLag);
        ScenarioExpect.IsType<EventualConsistencyMonitor<string>>(monitor);
    }
}
