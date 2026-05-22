using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.HealthEndpointMonitoringDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.HealthEndpointMonitoringDemo;

[Feature("Fulfillment health endpoint monitoring example")]
public sealed class FulfillmentHealthEndpointDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated health endpoints report fulfillment health")]
    [Fact]
    public Task Fluent_And_Generated_Health_Endpoints_Report_Fulfillment_Health()
        => Given("fulfillment health endpoint examples", () => new
        {
            Fluent = FulfillmentHealthEndpointDemoRunner.RunFluent(),
            Generated = FulfillmentHealthEndpointDemoRunner.RunGeneratedStatic()
        })
        .Then("both paths report a healthy fulfillment dependency set", result =>
        {
            ScenarioExpect.True(result.Fluent.Healthy);
            ScenarioExpect.True(result.Generated.Healthy);
            ScenarioExpect.Equal("fulfillment-health", result.Generated.EndpointName);
            ScenarioExpect.Equal(3, result.Generated.PassedCount);
        })
        .AssertPassed();

    [Scenario("Health endpoint demo reports degraded dependencies")]
    [Fact]
    public Task Health_Endpoint_Demo_Reports_Degraded_Dependencies()
        => Given("a service provider with degraded fulfillment dependencies", () =>
        {
            var services = new ServiceCollection();
            services.AddFulfillmentHealthEndpointDemo(_ => FulfillmentHealthEndpointDemoRunner.DegradedSnapshot());
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("evaluating the generated health endpoint", provider =>
        {
            using (provider)
                return provider.GetRequiredService<FulfillmentHealthEndpointService>().Evaluate();
        })
        .Then("the report identifies failed checks", report =>
        {
            ScenarioExpect.False(report.Healthy);
            ScenarioExpect.Equal(1, report.PassedCount);
            ScenarioExpect.Equal(2, report.FailedCount);
            ScenarioExpect.Contains(report.Checks, static check => check.Name == "message-broker" && !check.Healthy);
        })
        .AssertPassed();

    [Scenario("Health endpoint demo is importable through IServiceCollection")]
    [Fact]
    public Task Health_Endpoint_Demo_Is_Importable_Through_IServiceCollection()
        => Given("an importing app service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddFulfillmentHealthEndpointDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving and running the service", provider =>
        {
            using (provider)
                return provider.GetRequiredService<FulfillmentHealthEndpointService>().Summarize();
        })
        .Then("the service reports healthy fulfillment dependencies", result =>
        {
            ScenarioExpect.True(result.Healthy);
            ScenarioExpect.Equal(0, result.FailedCount);
        })
        .AssertPassed();

    [Scenario("Aggregate examples import health endpoint demo")]
    [Fact]
    public Task Aggregate_Examples_Import_Health_Endpoint_Demo()
        => Given("a PatternKit examples service provider", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving the aggregate health endpoint example", provider =>
        {
            using (provider)
                return provider.GetRequiredService<FulfillmentHealthEndpointExample>();
        })
        .Then("the aggregate example exposes the endpoint and service", example =>
        {
            ScenarioExpect.Equal("fulfillment-health", example.Endpoint.Name);
            ScenarioExpect.NotNull(example.Service);
        })
        .AssertPassed();

    [Scenario("Health endpoint demo maps to ASP.NET Core endpoints")]
    [Fact]
    public Task Health_Endpoint_Demo_Maps_To_AspNetCore_Endpoints()
        => Given("an ASP.NET Core application", () =>
        {
            var builder = WebApplication.CreateBuilder();
            builder.Services.AddFulfillmentHealthEndpointDemo();
            return builder.Build();
        })
        .When("mapping the fulfillment health endpoint", app =>
        {
            using (app)
            {
                app.MapFulfillmentHealthEndpoint();
                return app.Services.GetRequiredService<FulfillmentHealthEndpointService>().Summarize();
            }
        })
        .Then("the mapped app can resolve the endpoint service", result =>
            ScenarioExpect.True(result.Healthy))
        .AssertPassed();
}
