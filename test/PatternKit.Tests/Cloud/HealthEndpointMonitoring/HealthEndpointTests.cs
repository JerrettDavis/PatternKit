using PatternKit.Cloud.HealthEndpointMonitoring;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.HealthEndpointMonitoring;

[Feature("Health Endpoint Monitoring")]
public sealed class HealthEndpointTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Health endpoint reports healthy when every check passes")]
    [Fact]
    public Task Health_Endpoint_Reports_Healthy_When_Every_Check_Passes()
        => Given("a health endpoint", () => HealthEndpoint<SystemHealth>.Create("fulfillment-health")
            .WithCheck("database", static health => health.DatabaseOnline
                ? HealthEndpointCheckResult.HealthyCheck("database", "reachable")
                : HealthEndpointCheckResult.UnhealthyCheck("database", "offline"))
            .WithCheck("broker", static health => health.BrokerOnline
                ? HealthEndpointCheckResult.HealthyCheck("broker", "connected")
                : HealthEndpointCheckResult.UnhealthyCheck("broker", "disconnected"))
            .Build())
        .When("all dependencies are available", endpoint => endpoint.Evaluate(new(true, true, 4)))
        .Then("the report is healthy", report =>
        {
            ScenarioExpect.Equal("fulfillment-health", report.EndpointName);
            ScenarioExpect.True(report.Healthy);
            ScenarioExpect.Equal(2, report.PassedCount);
            ScenarioExpect.Equal(0, report.FailedCount);
        })
        .AssertPassed();

    [Scenario("Health endpoint reports failed checks")]
    [Fact]
    public Task Health_Endpoint_Reports_Failed_Checks()
        => Given("a health endpoint", () => HealthEndpoint<SystemHealth>.Create()
            .WithCheck("queue-depth", static health => health.QueueDepth <= 10
                ? HealthEndpointCheckResult.HealthyCheck("queue-depth", "within target")
                : HealthEndpointCheckResult.UnhealthyCheck("queue-depth", "backlog too deep"))
            .Build())
        .When("the queue is overloaded", endpoint => endpoint.Evaluate(new(true, true, 25)))
        .Then("the report captures the failed dependency", report =>
        {
            ScenarioExpect.False(report.Healthy);
            ScenarioExpect.Equal(0, report.PassedCount);
            ScenarioExpect.Equal(1, report.FailedCount);
            ScenarioExpect.Equal("backlog too deep", ScenarioExpect.Single(report.Checks).Message);
        })
        .AssertPassed();

    [Scenario("Health endpoint validates configuration and context")]
    [Fact]
    public Task Health_Endpoint_Validates_Configuration_And_Context()
        => Given("invalid health endpoint inputs", () => true)
        .Then("invalid endpoint names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => HealthEndpoint<SystemHealth>.Create("").WithCheck("database", static _ => HealthEndpointCheckResult.HealthyCheck("database")).Build()))
        .And("missing checks are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => HealthEndpoint<SystemHealth>.Create().Build()))
        .And("invalid check names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => HealthEndpoint<SystemHealth>.Create().WithCheck("", static _ => HealthEndpointCheckResult.HealthyCheck("database"))))
        .And("null checks are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => HealthEndpoint<SystemHealth>.Create().WithCheck("database", null!)))
        .And("null contexts are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => HealthEndpoint<string>.Create().WithCheck("value", static value => HealthEndpointCheckResult.HealthyCheck(value)).Build().Evaluate(null!)))
        .AssertPassed();

    [Scenario("Health endpoint rejects null check results")]
    [Fact]
    public Task Health_Endpoint_Rejects_Null_Check_Results()
        => Given("a health endpoint with an invalid check implementation", () => HealthEndpoint<SystemHealth>.Create()
            .WithCheck("broken", static _ => null!)
            .Build())
        .Then("the null check result is rejected", endpoint =>
            ScenarioExpect.Throws<InvalidOperationException>(() => endpoint.Evaluate(new(true, true, 0))))
        .AssertPassed();

    private sealed record SystemHealth(bool DatabaseOnline, bool BrokerOnline, int QueueDepth);
}
