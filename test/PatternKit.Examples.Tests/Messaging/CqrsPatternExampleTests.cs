using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.Messaging.SourceGenerated;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("CQRS dispatcher example")]
public sealed class CqrsPatternExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent mediator path separates command writes from query reads")]
    [Fact]
    public Task Fluent_Mediator_Path_Separates_Command_Writes_From_Query_Reads()
        => Given("the fluent CQRS example", () => CqrsPatternExample.RunFluentAsync)
            .When("running the command and query flow", async run => await run(CancellationToken.None))
            .Then("the query read model matches the command write", summary =>
                summary.Path == "fluent" && summary.QueryMatchedCommand)
            .And("the write total is calculated by the command handler", summary =>
                ScenarioExpect.Equal(39.90m, summary.Total))
            .And("pipeline and event logs were captured", summary =>
            {
                ScenarioExpect.Contains(summary.Log, entry => entry.StartsWith("pre:CreateCqrsOrder", StringComparison.Ordinal));
                ScenarioExpect.Contains(summary.Log, entry => entry.StartsWith("event:order-created:", StringComparison.Ordinal));
                ScenarioExpect.Contains(summary.Log, entry => entry.StartsWith("post:GetCqrsOrder", StringComparison.Ordinal));
            })
            .AssertPassed();

    [Scenario("Source-generated dispatcher path separates command writes from query reads")]
    [Fact]
    public Task Source_Generated_Dispatcher_Path_Separates_Command_Writes_From_Query_Reads()
        => Given("service provider configured for the source-generated CQRS example", () =>
            {
                var services = new ServiceCollection();
                services.AddSourceGeneratedCqrsServices();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("running the generated command and query flow", async provider =>
            {
                using (provider)
                    return await CqrsPatternExample.RunSourceGeneratedAsync(provider, CancellationToken.None);
            })
            .Then("the query read model matches the command write", summary =>
                summary.Path == "source-generated" && summary.QueryMatchedCommand)
            .And("the write total is calculated by the generated command handler", summary =>
                ScenarioExpect.Equal(100m, summary.Total))
            .And("command and notification handlers logged operations", summary =>
            {
                ScenarioExpect.Contains(summary.Log, entry => entry.Contains("Creating customer", StringComparison.Ordinal));
                ScenarioExpect.Contains(summary.Log, entry => entry.Contains("Placing order", StringComparison.Ordinal));
                ScenarioExpect.Contains(summary.Log, entry => entry.Contains("Sending order confirmation", StringComparison.Ordinal));
            })
            .AssertPassed();

    [Scenario("CQRS example is importable through IServiceCollection")]
    [Fact]
    public Task Cqrs_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection using the PatternKit CQRS extension", () =>
            {
                var services = new ServiceCollection();
                services.AddCqrsDispatcherExample();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and running both CQRS paths", async provider =>
            {
                using (provider)
                {
                    var example = provider.GetRequiredService<CqrsDispatcherExample>();
                    var fluent = await example.RunFluentAsync(CancellationToken.None);
                    var generated = await example.RunSourceGeneratedAsync(provider, CancellationToken.None);
                    var descriptor = provider.GetServices<PatternKitExampleServiceDescriptor>()
                        .Single(descriptor => descriptor.ExampleName == "CQRS Dispatcher");

                    return new
                    {
                        Fluent = fluent,
                        Generated = generated,
                        descriptor.Integration
                    };
                }
            })
            .Then("both entry points produce matched command and query results", result =>
                result.Fluent.QueryMatchedCommand && result.Generated.QueryMatchedCommand)
            .And("the service descriptor advertises DI and source generation", result =>
                result.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)
                && result.Integration.HasFlag(ExampleIntegrationSurface.SourceGenerator))
            .AssertPassed();
}
