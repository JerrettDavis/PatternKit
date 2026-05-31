using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.ObjectPoolDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ObjectPoolDemo;

[Feature("Spreadsheet formula Object Pool demo")]
public sealed class SpreadsheetFormulaObjectPoolDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static readonly FormulaEvaluationRequest Request = new(
        "D4",
        "subtotal + tax + 5",
        new Dictionary<string, decimal>
        {
            ["subtotal"] = 100m,
            ["tax"] = 8.25m
        });

    [Scenario("Fluent and generated object pools evaluate formulas consistently")]
    [Fact]
    public Task Fluent_And_Generated_Object_Pools_Evaluate_Formulas_Consistently()
        => Given("a spreadsheet formula request", () => Request)
        .When("evaluating through both object pool routes", request => new
        {
            Fluent = SpreadsheetFormulaObjectPoolDemoRunner.RunFluent(request),
            Generated = SpreadsheetFormulaObjectPoolDemoRunner.RunGenerated(request)
        })
        .Then("both routes produce the same result", result =>
        {
            ScenarioExpect.Equal(result.Fluent, result.Generated);
            ScenarioExpect.Equal(113.25m, result.Generated.Value);
            ScenarioExpect.Equal(5, result.Generated.TemporaryAllocations);
        })
        .AssertPassed();

    [Scenario("Object Pool demo is importable through IServiceCollection")]
    [Fact]
    public Task Object_Pool_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service collection with the object pool demo", () =>
        {
            var services = new ServiceCollection();
            services.AddSpreadsheetFormulaObjectPoolDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("resolving and running the demo", provider =>
        {
            using (provider)
                return provider.GetRequiredService<SpreadsheetFormulaObjectPoolDemoRunner>().Run(Request);
        })
        .Then("the configured generated pool is used by the service", result =>
        {
            ScenarioExpect.Equal("D4", result.Cell);
            ScenarioExpect.Equal(113.25m, result.Value);
        })
        .AssertPassed();
}
