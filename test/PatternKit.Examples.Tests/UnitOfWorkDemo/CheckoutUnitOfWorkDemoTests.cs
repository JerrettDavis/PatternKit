using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.UnitOfWorkDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.UnitOfWorkDemo;

[Feature("Checkout unit of work example")]
public sealed class CheckoutUnitOfWorkDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated unit of work commit ordered steps")]
    [Fact]
    public Task Fluent_And_Generated_Unit_Of_Work_Commit_Ordered_Steps()
        => Given("fluent and generated checkout units of work", async () => new
            {
                Fluent = await CheckoutUnitOfWorkDemo.RunFluentAsync(),
                Generated = await CheckoutUnitOfWorkDemo.RunGeneratedAsync()
            })
            .Then("both paths commit the same ordered steps", result =>
            {
                ScenarioExpect.True(result.Fluent.Committed);
                ScenarioExpect.True(result.Generated.Committed);
                ScenarioExpect.Equal(["reserve", "capture"], result.Generated.Log);
            })
            .AssertPassed();

    [Scenario("Unit of work rollback compensates committed steps")]
    [Fact]
    public Task Unit_Of_Work_Rollback_Compensates_Committed_Steps()
        => Given("a checkout unit of work with a failing persist step", CheckoutUnitOfWorkDemo.RunRollbackAsync)
            .Then("committed steps are compensated", summary =>
            {
                ScenarioExpect.False(summary.Committed);
                ScenarioExpect.Equal(["reserve", "undo-reserve"], summary.Log);
            })
            .AssertPassed();

    [Scenario("Unit of work example is importable through IServiceCollection")]
    [Fact]
    public Task Unit_Of_Work_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection importing the checkout unit of work example", () =>
            {
                var services = new ServiceCollection();
                services.AddCheckoutUnitOfWorkDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("an importing application runs the workflow", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<CheckoutUnitOfWorkWorkflow>().RunAsync().AsTask().GetAwaiter().GetResult();
            })
            .Then("the workflow commits", summary => ScenarioExpect.True(summary.Committed))
            .AssertPassed();
}
