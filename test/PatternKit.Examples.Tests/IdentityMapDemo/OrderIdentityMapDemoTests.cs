using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.IdentityMapDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.IdentityMapDemo;

[Feature("Order Identity Map example")]
public sealed partial class OrderIdentityMapDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated Identity Maps reuse loaded orders")]
    [Fact]
    public Task Fluent_And_Generated_Identity_Maps_Reuse_Loaded_Orders()
        => Given("fluent and generated identity maps", () => new
            {
                Fluent = OrderIdentityMapDemo.RunFluent(),
                Generated = OrderIdentityMapDemo.RunGenerated()
            })
            .Then("both paths reuse object identity and reject duplicates", runs =>
            {
                ScenarioExpect.True(runs.Fluent.ReusedInstance);
                ScenarioExpect.True(runs.Generated.ReusedInstance);
                ScenarioExpect.True(runs.Generated.DuplicateRejected);
            })
            .AssertPassed();

    [Scenario("Identity Map example is scoped through IServiceCollection")]
    [Fact]
    public Task Identity_Map_Example_Is_Scoped_Through_IServiceCollection()
        => Given("a provider importing the identity map example", () =>
            {
                var services = new ServiceCollection();
                services.AddOrderIdentityMapDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("a request scope uses the workflow", provider =>
            {
                using (provider)
                using (var scope = provider.CreateScope())
                    return scope.ServiceProvider.GetRequiredService<OrderIdentityMapWorkflow>().Run();
            })
            .Then("the scoped map reuses the loaded order", summary =>
            {
                ScenarioExpect.True(summary.ReusedInstance);
                ScenarioExpect.Equal(1, summary.TrackedCount);
            })
            .AssertPassed();
}
