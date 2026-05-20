using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Generated recipient-list example")]
public sealed class RecipientListGeneratorExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated recipient lists deliver the same recipients")]
    [Fact]
    public Task Fluent_And_Generated_Recipient_Lists_Deliver_The_Same_Recipients()
        => Given("recipient-list example entry points", () =>
                new RecipientListExampleEntrypoints(
                    RecipientListGeneratorExample.RunFluent,
                    RecipientListGeneratorExample.RunGenerated))
            .When("running both recipient-list paths", runners => new
            {
                Fluent = runners.Fluent(),
                Generated = runners.Generated()
            })
            .Then("both paths fan out to the same recipients", result =>
                ScenarioExpect.Equal(result.Fluent.DeliveredRecipients, result.Generated.DeliveredRecipients))
            .And("both handlers record delivery side effects", result =>
                ScenarioExpect.Equal(["priority-audit", "billing-ledger"], result.Generated.DeliveryLog))
            .And("the generated path advertises its source-generated route", result =>
                ScenarioExpect.Equal("source-generated", result.Generated.Path))
            .AssertPassed();

    [Scenario("Generated recipient-list example is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Recipient_List_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection using the PatternKit recipient-list extension", () =>
            {
                var services = new ServiceCollection();
                services.AddGeneratedRecipientListExample();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and running the generated recipient-list example", provider =>
            {
                using (provider)
                {
                    var example = provider.GetRequiredService<GeneratedRecipientListExample>();
                    var summary = example.Runner.RunGenerated();
                    var descriptor = provider.GetServices<PatternKitExampleServiceDescriptor>()
                        .Single(descriptor => descriptor.ExampleName == "Generated Recipient List");

                    return new RecipientListImportRun(summary, descriptor.Integration);
                }
            })
            .Then("the generated runner dispatches to expected recipients", result =>
                ScenarioExpect.Equal(["priority-audit", "billing-ledger"], result.Summary.DeliveredRecipients))
            .And("the descriptor advertises DI and source generation", result =>
                result.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)
                && result.Integration.HasFlag(ExampleIntegrationSurface.SourceGenerator))
            .AssertPassed();

    private sealed record RecipientListImportRun(
        RecipientListSummary Summary,
        ExampleIntegrationSurface Integration);

    private sealed record RecipientListExampleEntrypoints(
        Func<RecipientListSummary> Fluent,
        Func<RecipientListSummary> Generated);
}
