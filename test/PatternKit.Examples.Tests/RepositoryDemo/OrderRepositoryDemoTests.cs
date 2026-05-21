using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.RepositoryDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.RepositoryDemo;

[Feature("Order repository example")]
public sealed class OrderRepositoryDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent repository example stores queries and rejects duplicates")]
    [Fact]
    public Task Fluent_Repository_Example_Stores_Queries_And_Rejects_Duplicates()
        => Given("the fluent order repository example", OrderRepositoryDemo.RunFluentAsync)
            .Then("the repository behaves like a collection boundary", summary =>
            {
                ScenarioExpect.True(summary.Stored);
                ScenarioExpect.True(summary.DuplicateRejected);
                ScenarioExpect.Equal(1, summary.PendingCount);
                ScenarioExpect.Equal("customer-1", summary.LoadedCustomerId);
            })
            .AssertPassed();

    [Scenario("Generated repository example stores queries and rejects duplicates")]
    [Fact]
    public Task Generated_Repository_Example_Stores_Queries_And_Rejects_Duplicates()
        => Given("the generated order repository example", OrderRepositoryDemo.RunGeneratedAsync)
            .Then("the generated repository factory supports the same workflow", summary =>
            {
                ScenarioExpect.True(summary.Stored);
                ScenarioExpect.True(summary.DuplicateRejected);
                ScenarioExpect.Equal(1, summary.PendingCount);
            })
            .AssertPassed();

    [Scenario("Repository example is importable through IServiceCollection")]
    [Fact]
    public Task Repository_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection importing the order repository example", () =>
            {
                var services = new ServiceCollection();
                services.AddOrderRepositoryDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("an importing application runs the workflow", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<OrderRepositoryWorkflow>().RunAsync().AsTask().GetAwaiter().GetResult();
            })
            .Then("the workflow uses the registered repository", summary =>
            {
                ScenarioExpect.True(summary.Stored);
                ScenarioExpect.True(summary.DuplicateRejected);
            })
            .AssertPassed();
}
