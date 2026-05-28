using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.ContextMaps;
using PatternKit.Examples.ContextMapDemo;
using PatternKit.Examples.DependencyInjection;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ContextMapDemo;

[Feature("Commerce context map example")]
public sealed class CommerceContextMapDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated context maps match")]
    [Fact]
    public Task Fluent_And_Generated_Context_Maps_Match()
        => Given("fluent and generated context maps", () => new
        {
            Fluent = CommerceContextMapDemo.CreateFluentMap(),
            Generated = CommerceContextMapDemo.CreateGeneratedMap()
        })
            .Then("map names match", ctx =>
                ScenarioExpect.Equal(ctx.Fluent.Name, ctx.Generated.Name))
            .And("relationship contracts match", ctx =>
                ScenarioExpect.Equal(
                    ctx.Fluent.Relationships.Select(static item => item.ContractName).ToArray(),
                    ctx.Generated.Relationships.Select(static item => item.ContractName).ToArray()))
            .AssertPassed();

    [Scenario("Commerce context map imports through IServiceCollection")]
    [Fact]
    public Task Commerce_Context_Map_Imports_Through_IServiceCollection()
        => Given("services with the commerce context map example", () =>
            {
                var services = new ServiceCollection();
                services.AddCommerceContextMapDemo();
                return services.BuildServiceProvider();
            })
            .When("summarizing the context map", sp => sp.GetRequiredService<CommerceContextMapDemo.CommerceContextMapReporter>().Summarize())
            .Then("the summary reflects the production relationships", summary =>
            {
                ScenarioExpect.Equal(2, summary.RelationshipCount);
                ScenarioExpect.True(summary.HasPublishedLanguage);
                ScenarioExpect.True(summary.HasCustomerSupplier);
            })
            .AssertPassed();

    [Scenario("Commerce context map is included in aggregate examples")]
    [Fact]
    public Task Commerce_Context_Map_Is_Included_In_Aggregate_Examples()
        => Given("all PatternKit examples registered in DI", () =>
            {
                var services = new ServiceCollection();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider();
            })
            .Then("the context map wrapper resolves", sp =>
                ScenarioExpect.NotNull(sp.GetRequiredService<CommerceContextMapPatternExample>()))
            .And("the generated descriptor resolves", sp =>
                ScenarioExpect.Equal("Commerce", sp.GetServices<ContextMapDescriptor>().Single().Name))
            .AssertPassed();
}
