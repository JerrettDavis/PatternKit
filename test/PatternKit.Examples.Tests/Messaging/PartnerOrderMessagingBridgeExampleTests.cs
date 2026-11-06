using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Partner order messaging bridge example")]
public sealed class PartnerOrderMessagingBridgeExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static readonly PartnerBridgeOrder[] Orders =
    [
        new("P-100", "accepted", 125m),
        new("P-101", "paid", 250m)
    ];

    [Scenario("Fluent messaging bridge imports partner orders into commerce topics")]
    [Fact]
    public Task Fluent_MessagingBridge_ImportsPartnerOrdersIntoCommerceTopics()
        => Given("partner orders from an external topology", () => Orders)
            .When("the fluent bridge imports them", PartnerOrderMessagingBridgeExampleRunner.RunFluent)
            .Then("both orders are bridged", summary =>
            {
                ScenarioExpect.Equal(2, summary.BridgedCount);
                ScenarioExpect.Equal(2, summary.CommerceEventCount);
            })
            .And("the bridge preserves correlation headers", summary =>
                ScenarioExpect.Equal("partner:P-100", summary.CorrelationId))
            .AssertPassed();

    [Scenario("Generated messaging bridge matches fluent bridge behavior")]
    [Fact]
    public Task Generated_MessagingBridge_MatchesFluentBridgeBehavior()
        => Given("partner orders from an external topology", () => Orders)
            .When("the generated bridge imports them", PartnerOrderMessagingBridgeExampleRunner.RunGeneratedStatic)
            .Then("topics are selected for accepted and paid orders", summary =>
                ScenarioExpect.Equal(["accepted", "paid"], summary.Topics))
            .AssertPassed();

    [Scenario("Messaging bridge demo is importable through IServiceCollection")]
    [Fact]
    public Task MessagingBridge_Demo_IsImportableThroughIServiceCollection()
        => Given("a service collection with the messaging bridge demo", () =>
            {
                var services = new ServiceCollection();
                services.AddPartnerOrderMessagingBridgeDemo();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving and running the bridge", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<PartnerOrderMessagingBridgeExampleRunner>().RunGenerated(Orders);
            })
            .Then("orders are bridged through dependency injection", summary =>
                ScenarioExpect.Equal(2, summary.BridgedCount))
            .AssertPassed();

    [Scenario("Aggregate example registrations include the messaging bridge demo")]
    [Fact]
    public Task Aggregate_ExampleRegistrations_IncludeMessagingBridgeDemo()
        => Given("the aggregate PatternKit examples registration", () =>
            {
                var services = new ServiceCollection();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving the messaging bridge example", provider =>
            {
                using (provider)
                    return provider.GetRequiredService<PartnerOrderMessagingBridgeExampleService>().Runner.RunGenerated(Orders);
            })
            .Then("the aggregate registration imports the bridge", summary =>
                ScenarioExpect.Equal(2, summary.BridgedCount))
            .AssertPassed();
}
