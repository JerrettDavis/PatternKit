using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Generated splitter and aggregator example")]
public sealed class MessageRoutingExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated splitter aggregator paths produce the same routing summary")]
    [Fact]
    public Task Fluent_And_Generated_Splitter_Aggregator_Paths_Produce_The_Same_Routing_Summary()
        => Given("message routing example entry points", () =>
                new MessageRoutingExampleRunner(MessageRoutingExample.RunFluent, MessageRoutingExample.RunGenerated))
            .When("running both splitter aggregator paths", runner => new
            {
                Fluent = runner.RunFluent(),
                Generated = runner.RunGenerated()
            })
            .Then("both paths route and fan out the order consistently", result =>
            {
                ScenarioExpect.Equal("priority", result.Generated.Route);
                ScenarioExpect.Equal(result.Fluent.Route, result.Generated.Route);
                ScenarioExpect.Equal(result.Fluent.Recipients, result.Generated.Recipients);
                ScenarioExpect.Equal(["audit", "billing"], result.Generated.Recipients);
            })
            .And("both paths split and aggregate the same correlated line items", result =>
            {
                ScenarioExpect.Equal(result.Fluent.SplitCount, result.Generated.SplitCount);
                ScenarioExpect.Equal(2, result.Generated.SplitCount);
                ScenarioExpect.Equal(result.Fluent.AggregatedTotal, result.Generated.AggregatedTotal);
                ScenarioExpect.Equal(100m, result.Generated.AggregatedTotal);
                ScenarioExpect.Equal("msg-order-42", result.Generated.CausationId);
            })
            .And("the generated path advertises its source-generated factories", result =>
                ScenarioExpect.Equal("source-generated", result.Generated.Path))
            .AssertPassed();

    [Scenario("Generated splitter aggregator example is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Splitter_Aggregator_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection using the PatternKit splitter aggregator extension", () =>
            {
                var services = new ServiceCollection();
                services.AddGeneratedSplitterAggregatorExample();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and running the generated splitter aggregator example", provider =>
            {
                using (provider)
                {
                    var example = provider.GetRequiredService<GeneratedSplitterAggregatorExample>();
                    var summary = example.Runner.RunGenerated();
                    var descriptor = provider.GetServices<PatternKitExampleServiceDescriptor>()
                        .Single(descriptor => descriptor.ExampleName == "Generated Splitter and Aggregator");

                    return new MessageRoutingImportRun(summary, descriptor.Integration);
                }
            })
            .Then("the generated runner returns expected routing metadata", result =>
            {
                ScenarioExpect.Equal("priority", result.Summary.Route);
                ScenarioExpect.Equal(["audit", "billing"], result.Summary.Recipients);
                ScenarioExpect.Equal(2, result.Summary.SplitCount);
                ScenarioExpect.Equal(100m, result.Summary.AggregatedTotal);
            })
            .And("the descriptor advertises DI source generation and messaging", result =>
                result.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)
                && result.Integration.HasFlag(ExampleIntegrationSurface.SourceGenerator)
                && result.Integration.HasFlag(ExampleIntegrationSurface.Messaging))
            .AssertPassed();

    private sealed record MessageRoutingImportRun(RoutingSummary Summary, ExampleIntegrationSurface Integration);
}
