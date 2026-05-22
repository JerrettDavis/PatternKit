using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ProductionReadiness;
using PatternKit.Examples.SchedulerAgentSupervisorDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.SchedulerAgentSupervisorDemo;

[Feature("Warehouse Scheduler Agent Supervisor demo")]
public sealed class WarehouseSchedulerAgentSupervisorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent scheduler agent supervisor dispatches replenishment work")]
    [Fact]
    public Task Fluent_Scheduler_Agent_Supervisor_Dispatches_Replenishment_Work()
        => Given("the fluent warehouse scheduler", WarehouseSchedulers.CreateFluent)
        .When("a replenishment job is due", scheduler =>
        {
            var now = new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero);
            var work = new WarehouseReplenishmentWork("B-200", FailFirstAttempt: false, []);
            scheduler.Schedule("replenish:B-200", work, now);
            return scheduler.RunDue(now);
        })
        .Then("work is dispatched successfully", results =>
        {
            var result = ScenarioExpect.Single(results);
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("release-replenishment", result.AgentName);
            ScenarioExpect.Equal("B-200", result.Response!.BatchId);
        })
        .AssertPassed();

    [Scenario("Generated scheduler is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Scheduler_Is_Importable_Through_IServiceCollection()
        => Given("a service provider configured with the scheduler demo", () =>
        {
            var services = new ServiceCollection();
            services.AddWarehouseSchedulerAgentSupervisorDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("the demo runner resolves and runs", provider =>
        {
            using (provider)
                return provider.GetRequiredService<WarehouseSchedulerDemoRunner>().RunGenerated();
        })
        .Then("retry supervision captures failure and retry success", results =>
        {
            ScenarioExpect.Equal(2, results.Count);
            ScenarioExpect.True(results[0].RetryScheduled);
            ScenarioExpect.True(results[1].Succeeded);
            ScenarioExpect.Equal(2, results[1].Attempt);
        })
        .AssertPassed();

    [Scenario("Hosted service participates in scheduler lifecycle")]
    [Fact]
    public Task Hosted_Service_Participates_In_Scheduler_Lifecycle()
        => Given("a hosted warehouse scheduler service", () =>
        {
            var services = new ServiceCollection();
            services.AddWarehouseSchedulerAgentSupervisorDemo();
            var provider = services.BuildServiceProvider(validateScopes: true);
            return new { Provider = provider, Hosted = provider.GetServices<IHostedService>().OfType<WarehouseSchedulerHostedService>().Single() };
        })
        .When("the host starts and stops the service", ctx => RunHostedLifecycle(ctx.Provider, ctx.Hosted))
        .Then("scheduled work is dispatched", results =>
        {
            var result = ScenarioExpect.Single(results);
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("B-100", result.Response!.BatchId);
        })
        .AssertPassed();

    [Scenario("Warehouse scheduler appears in production catalogs")]
    [Fact]
    public Task Warehouse_Scheduler_Appears_In_Production_Catalogs()
        => Given("the production catalogs", () => new
        {
            Examples = new PatternKitExampleCatalog(),
            Patterns = new PatternKitPatternCatalog()
        })
        .Then("the example catalog includes the scheduler demo", ctx =>
            ScenarioExpect.Contains(ctx.Examples.Entries, entry => entry.Name == "Warehouse Scheduler Agent Supervisor"))
        .And("the pattern catalog includes Scheduler Agent Supervisor", ctx =>
            ScenarioExpect.Contains(ctx.Patterns.Patterns, pattern => pattern.Name == "Scheduler Agent Supervisor"))
        .AssertPassed();

    [Scenario("Aggregate example registration includes warehouse scheduler")]
    [Fact]
    public Task Aggregate_Example_Registration_Includes_Warehouse_Scheduler()
        => Given("all PatternKit examples registered in a service collection", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("the scheduler example is resolved", provider =>
        {
            using (provider)
                return provider.GetRequiredService<WarehouseSchedulerAgentSupervisorExample>().Runner.RunGenerated();
        })
        .Then("the registered example executes retry supervision", results =>
        {
            ScenarioExpect.Equal(2, results.Count);
            ScenarioExpect.True(results[1].Succeeded);
        })
        .AssertPassed();

    private static async Task<IReadOnlyList<PatternKit.Cloud.SchedulerAgentSupervisor.SchedulerAgentResult<WarehouseReplenishmentSummary>>> RunHostedLifecycle(
        ServiceProvider provider,
        WarehouseSchedulerHostedService hosted)
    {
        using (provider)
        {
            await hosted.StartAsync(CancellationToken.None);
            await hosted.StopAsync(CancellationToken.None);
            return hosted.LastResults;
        }
    }
}
