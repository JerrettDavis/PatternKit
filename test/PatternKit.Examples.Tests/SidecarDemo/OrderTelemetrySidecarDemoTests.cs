using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ProductionReadiness;
using PatternKit.Examples.SidecarDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.SidecarDemo;

[Feature("Order Telemetry Sidecar example")]
public sealed class OrderTelemetrySidecarDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent order telemetry sidecar wraps order submission")]
    [Fact]
    public Task Fluent_Order_Telemetry_Sidecar_Wraps_Order_Submission()
        => Given("the fluent order telemetry sidecar", () => OrderTelemetrySidecars.CreateFluent(new DemoOrderTelemetrySink()))
        .When("an order is submitted", sidecar => sidecar.Invoke(new OrderTelemetryRequest("O-100", 42m)))
        .Then("trace and telemetry sidecar steps surround the primary handler", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("ACCEPTED-O-100", result.Response!.Confirmation);
            ScenarioExpect.Equal(["trace-context", "telemetry"], result.Events);
        })
        .AssertPassed();

    [Scenario("Generated order telemetry sidecar is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Order_Telemetry_Sidecar_Is_Importable_Through_IServiceCollection()
        => Given("a service collection with the sidecar example", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderTelemetrySidecarExample();
            return services.BuildServiceProvider();
        })
        .When("the demo runner submits an order", provider => provider.GetRequiredService<OrderTelemetrySidecarExample>().Runner.RunGenerated(new OrderTelemetryRequest("O-200", 50m)))
        .Then("the generated sidecar returns a traced response", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("trace-O-200", result.Response!.TraceId);
        })
        .AssertPassed();

    [Scenario("Order Telemetry Sidecar example is cataloged as production ready")]
    [Fact]
    public Task Order_Telemetry_Sidecar_Example_Is_Cataloged_As_Production_Ready()
        => Given("the production readiness catalogs", () => new { Examples = new PatternKitExampleCatalog(), Patterns = new PatternKitPatternCatalog() })
        .Then("the example catalog includes order telemetry sidecar", catalogs =>
            ScenarioExpect.Contains(catalogs.Examples.Entries, entry => entry.Name == "Order Telemetry Sidecar" && entry.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)))
        .And("the pattern catalog includes Sidecar", catalogs =>
            ScenarioExpect.Contains(catalogs.Patterns.Patterns, pattern => pattern.Name == "Sidecar" && pattern.Implementation.HasSourceGeneratedPath))
        .AssertPassed();
}
