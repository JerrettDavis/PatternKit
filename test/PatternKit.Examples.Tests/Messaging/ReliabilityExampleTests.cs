using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Generated reliability pipeline example")]
public sealed class ReliabilityExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated reliability paths dispatch one outbox message for duplicate input")]
    [Fact]
    public Task Fluent_And_Generated_Reliability_Paths_Dispatch_One_Outbox_Message_For_Duplicate_Input()
        => Given("reliability example entry points", () =>
                new ReliabilityExampleRunner(ReliabilityExample.RunFluentAsync, ReliabilityExample.RunGeneratedAsync))
            .When("running both reliability paths", async ValueTask<ReliabilityExampleRun> (runner) => new ReliabilityExampleRun(
                await runner.RunFluentAsync(),
                await runner.RunGeneratedAsync()))
            .Then("the fluent path dispatches only one accepted event", result =>
                ScenarioExpect.Equal(["order-42"], result.FluentDispatched))
            .And("the generated path matches the fluent behavior", result =>
                ScenarioExpect.Equal(result.FluentDispatched, result.GeneratedDispatched))
            .AssertPassed();

    [Scenario("Generated reliability pipeline example is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Reliability_Pipeline_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection using the PatternKit reliability extension", () =>
            {
                var services = new ServiceCollection();
                services.AddGeneratedReliabilityPipelineExample();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and running the generated reliability pipeline example", async ValueTask<ReliabilityImportRun> (provider) =>
            {
                using (provider)
                {
                    var example = provider.GetRequiredService<GeneratedReliabilityPipelineExample>();
                    var dispatched = await example.Runner.RunGeneratedAsync();
                    var descriptor = provider.GetServices<PatternKitExampleServiceDescriptor>()
                        .Single(descriptor => descriptor.ExampleName == "Generated Reliability Pipeline");

                    return new ReliabilityImportRun(dispatched, descriptor.Integration);
                }
            })
            .Then("the generated runner dispatches one accepted order", result =>
                ScenarioExpect.Equal(["order-42"], result.Dispatched))
            .And("the descriptor advertises DI source generation and messaging", result =>
                result.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)
                && result.Integration.HasFlag(ExampleIntegrationSurface.SourceGenerator)
                && result.Integration.HasFlag(ExampleIntegrationSurface.Messaging))
            .AssertPassed();

    private sealed record ReliabilityExampleRun(
        IReadOnlyList<string> FluentDispatched,
        IReadOnlyList<string> GeneratedDispatched);

    private sealed record ReliabilityImportRun(IReadOnlyList<string> Dispatched, ExampleIntegrationSurface Integration);
}
