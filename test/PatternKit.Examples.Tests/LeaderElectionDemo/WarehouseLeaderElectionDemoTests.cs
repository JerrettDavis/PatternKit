using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.LeaderElectionDemo;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.LeaderElectionDemo;

[Feature("Warehouse Leader Election demo")]
public sealed class WarehouseLeaderElectionDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent leader election acquires warehouse leadership")]
    [Fact]
    public Task Fluent_Leader_Election_Acquires_Warehouse_Leadership()
        => Given("a fluent warehouse leader election", WarehouseLeaderElectionDemoRunner.RunFluent)
        .Then("the candidate becomes leader", result =>
        {
            ScenarioExpect.True(result.Acquired);
            ScenarioExpect.Equal("warehouse-node-a", result.CandidateId);
        })
        .AssertPassed();

    [Scenario("Generated leader election is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Leader_Election_Is_Importable_Through_IServiceCollection()
        => Given("a service provider configured with the warehouse leader election demo", () =>
        {
            var services = new ServiceCollection();
            services.AddWarehouseLeaderElectionDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("the demo runner resolves and runs", provider =>
        {
            using (provider)
                return provider.GetRequiredService<WarehouseLeaderElectionDemoRunner>().RunGenerated();
        })
        .Then("leadership callbacks are executed", log =>
            ScenarioExpect.Equal(["acquired:1", "renewed:1", "released"], log))
        .AssertPassed();

    [Scenario("Hosted service participates in leadership lifecycle")]
    [Fact]
    public Task Hosted_Service_Participates_In_Leadership_Lifecycle()
        => Given("a hosted warehouse leader service", () =>
        {
            var services = new ServiceCollection();
            services.AddWarehouseLeaderElectionDemo();
            var provider = services.BuildServiceProvider(validateScopes: true);
            return new { Provider = provider, Hosted = provider.GetServices<IHostedService>().OfType<WarehouseLeadershipHostedService>().Single() };
        })
        .When("the host starts and stops the service", async ctx =>
        {
            using (ctx.Provider)
            {
                await ctx.Hosted.StartAsync(CancellationToken.None);
                await ctx.Hosted.StopAsync(CancellationToken.None);
                return ctx.Hosted.Context.Log.ToArray();
            }
        })
        .Then("leadership is acquired and released", log =>
            ScenarioExpect.Equal(["acquired:1", "released"], log))
        .AssertPassed();

    [Scenario("Warehouse leader election appears in production catalogs")]
    [Fact]
    public Task Warehouse_Leader_Election_Appears_In_Production_Catalogs()
        => Given("the production catalogs", () => new
        {
            Examples = new PatternKitExampleCatalog(),
            Patterns = new PatternKitPatternCatalog()
        })
        .Then("the example catalog includes the leader election demo", ctx =>
            ScenarioExpect.Contains(ctx.Examples.Entries, entry => entry.Name == "Warehouse Leader Election"))
        .And("the pattern catalog includes Leader Election", ctx =>
            ScenarioExpect.Contains(ctx.Patterns.Patterns, pattern => pattern.Name == "Leader Election"))
        .AssertPassed();

    [Scenario("Aggregate example registration includes warehouse leader election")]
    [Fact]
    public Task Aggregate_Example_Registration_Includes_Warehouse_Leader_Election()
        => Given("all PatternKit examples registered in a service collection", () =>
        {
            var services = new ServiceCollection();
            services.AddPatternKitExamples();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("the leader election example is resolved", provider =>
        {
            using (provider)
                return provider.GetRequiredService<WarehouseLeaderElectionExample>().Runner.RunGenerated();
        })
        .Then("the registered example executes leadership callbacks", log =>
            ScenarioExpect.Equal(["acquired:1", "renewed:1", "released"], log))
        .AssertPassed();
}
