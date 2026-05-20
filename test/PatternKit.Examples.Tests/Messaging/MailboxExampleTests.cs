using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Generated mailbox example")]
public sealed class MailboxExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated mailbox paths process messages in order")]
    [Fact]
    public Task Fluent_And_Generated_Mailbox_Paths_Process_Messages_In_Order()
        => Given("mailbox example entry points", () =>
                new MailboxExampleRunner(MailboxExample.RunFluentAsync, MailboxExample.RunGeneratedAsync))
            .When("running both mailbox paths", async runner => new
            {
                Fluent = await runner.RunFluentAsync(),
                Generated = await runner.RunGeneratedAsync()
            })
            .Then("both paths process correlated work in order", result =>
            {
                ScenarioExpect.Equal(["batch-42:prepare", "batch-42:ship"], result.Fluent);
                ScenarioExpect.Equal(result.Fluent, result.Generated);
            })
            .AssertPassed();

    [Scenario("Generated mailbox example is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Mailbox_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection using the PatternKit mailbox extension", () =>
            {
                var services = new ServiceCollection();
                services.AddGeneratedMailboxExample();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and running the generated mailbox example", async provider =>
            {
                using (provider)
                {
                    var example = provider.GetRequiredService<GeneratedMailboxExample>();
                    var processed = await example.Runner.RunGeneratedAsync();
                    var descriptor = provider.GetServices<PatternKitExampleServiceDescriptor>()
                        .Single(descriptor => descriptor.ExampleName == "Generated Mailbox");

                    return new MailboxImportRun(processed, descriptor.Integration);
                }
            })
            .Then("the generated runner returns expected work item identifiers", result =>
                ScenarioExpect.Equal(["batch-42:prepare", "batch-42:ship"], result.Processed))
            .And("the descriptor advertises DI source generation and messaging", result =>
                result.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)
                && result.Integration.HasFlag(ExampleIntegrationSurface.SourceGenerator)
                && result.Integration.HasFlag(ExampleIntegrationSurface.Messaging))
            .AssertPassed();

    private sealed record MailboxImportRun(IReadOnlyList<string> Processed, ExampleIntegrationSurface Integration);
}
