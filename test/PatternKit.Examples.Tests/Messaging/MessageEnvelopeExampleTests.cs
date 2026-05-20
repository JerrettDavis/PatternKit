using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.ProductionReadiness;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Generated message-envelope example")]
public sealed class MessageEnvelopeExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated envelope contracts produce the same metadata")]
    [Fact]
    public Task Fluent_And_Generated_Envelope_Contracts_Produce_The_Same_Metadata()
        => Given("message-envelope example entry points", () =>
                new MessageEnvelopeEntrypoints(MessageEnvelopeExample.RunFluent, MessageEnvelopeExample.RunGenerated))
            .When("running both envelope paths", runners => new
            {
                Fluent = runners.Fluent(),
                Generated = runners.Generated()
            })
            .Then("both paths carry the same envelope metadata", result =>
            {
                ScenarioExpect.Equal(result.Fluent.OrderId, result.Generated.OrderId);
                ScenarioExpect.Equal(result.Fluent.MessageId, result.Generated.MessageId);
                ScenarioExpect.Equal(result.Fluent.CorrelationId, result.Generated.CorrelationId);
                ScenarioExpect.Equal(result.Fluent.CausationId, result.Generated.CausationId);
                ScenarioExpect.Equal(result.Fluent.IdempotencyKey, result.Generated.IdempotencyKey);
                ScenarioExpect.Equal(result.Fluent.ContentType, result.Generated.ContentType);
            })
            .And("both paths preserve execution context metadata", result =>
            {
                ScenarioExpect.Equal(result.Fluent.Route, result.Generated.Route);
                ScenarioExpect.Equal(result.Fluent.Attempt, result.Generated.Attempt);
            })
            .And("the generated path advertises its source-generated contract", result =>
                ScenarioExpect.Equal("source-generated", result.Generated.Path))
            .AssertPassed();

    [Scenario("Generated message-envelope example is importable through IServiceCollection")]
    [Fact]
    public Task Generated_Message_Envelope_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection using the PatternKit message-envelope extension", () =>
            {
                var services = new ServiceCollection();
                services.AddGeneratedMessageEnvelopeExample();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and running the generated message-envelope example", provider =>
            {
                using (provider)
                {
                    var example = provider.GetRequiredService<GeneratedMessageEnvelopeExample>();
                    var summary = example.Runner.RunGenerated();
                    var descriptor = provider.GetServices<PatternKitExampleServiceDescriptor>()
                        .Single(descriptor => descriptor.ExampleName == "Generated Message Envelope");

                    return new MessageEnvelopeImportRun(summary, descriptor.Integration);
                }
            })
            .Then("the generated runner returns expected envelope metadata", result =>
            {
                ScenarioExpect.Equal("order-42", result.Summary.OrderId);
                ScenarioExpect.Equal("msg-100", result.Summary.MessageId);
                ScenarioExpect.Equal("order-42", result.Summary.CorrelationId);
                ScenarioExpect.Equal("billing", result.Summary.Route);
            })
            .And("the descriptor advertises DI source generation and messaging", result =>
                result.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)
                && result.Integration.HasFlag(ExampleIntegrationSurface.SourceGenerator)
                && result.Integration.HasFlag(ExampleIntegrationSurface.Messaging))
            .AssertPassed();

    private sealed record MessageEnvelopeEntrypoints(Func<Summary> Fluent, Func<Summary> Generated);

    private sealed record MessageEnvelopeImportRun(Summary Summary, ExampleIntegrationSurface Integration);
}
