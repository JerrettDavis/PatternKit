using PatternKit.Messaging.PipesAndFilters;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Messaging.PipesAndFilters;

[Feature("Pipes and Filters")]
public sealed class PipesAndFiltersPipelineTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Pipes and Filters execute ordered transformations")]
    [Fact]
    public Task Pipes_And_Filters_Execute_Ordered_Transformations()
        => Given("a fulfillment processing pipeline", () => PipesAndFiltersPipeline<string>.Create("fulfillment")
            .AddFilter("trim", (value, _) => new ValueTask<string>(value.Trim()))
            .AddFilter("normalize", (value, _) => new ValueTask<string>(value.ToUpperInvariant()))
            .AddFilter("tag", (value, _) => new ValueTask<string>($"ready:{value}"))
            .Build())
        .When("the pipeline processes input", pipeline => pipeline.ExecuteAsync(" order-100 ").AsTask())
        .Then("each filter transforms the output in order", result =>
        {
            ScenarioExpect.True(result.Succeeded);
            ScenarioExpect.Equal("ready:ORDER-100", result.Value);
            ScenarioExpect.Equal(["trim", "normalize", "tag"], result.Filters.Select(static filter => filter.Name));
        })
        .AssertPassed();

    [Scenario("Pipes and Filters validate configuration")]
    [Fact]
    public Task Pipes_And_Filters_Validate_Configuration()
        => Given("invalid pipes and filters inputs", () => true)
        .Then("invalid pipeline names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => PipesAndFiltersPipeline<string>.Create("").AddFilter("trim", (value, _) => new ValueTask<string>(value)).Build()))
        .And("pipelines require at least one filter", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => PipesAndFiltersPipeline<string>.Create().Build()))
        .And("invalid filter names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => PipesAndFiltersPipeline<string>.Create().AddFilter("", (value, _) => new ValueTask<string>(value)).Build()))
        .And("null filters are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => PipesAndFiltersPipeline<string>.Create().AddFilter("trim", null!)))
        .AssertPassed();
}
