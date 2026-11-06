using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Messaging;

[Feature("Fulfillment dead-letter channel example")]
public sealed class FulfillmentDeadLetterChannelExampleTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent dead letter channel captures and prepares replay")]
    [Fact]
    public Task Fluent_Dead_Letter_Channel_Captures_And_Prepares_Replay()
        => Given("the fluent fulfillment dead-letter channel example", FulfillmentDeadLetterChannelExample.RunFluent)
            .Then("the failed message is captured with replay metadata", summary =>
            {
                ScenarioExpect.Equal("fulfillment-dead:fulfillment:order-100", summary.DeadLetterId);
                ScenarioExpect.Equal("carrier timeout", summary.Reason);
                ScenarioExpect.Equal(4, summary.Attempts);
                ScenarioExpect.Equal("checkout:order-100", summary.CorrelationId);
                ScenarioExpect.True(summary.ReadyForReplay);
                ScenarioExpect.Equal(summary.DeadLetterId, summary.ReplayedFrom);
            })
            .AssertPassed();

    [Scenario("Generated dead letter channel captures and prepares replay")]
    [Fact]
    public Task Generated_Dead_Letter_Channel_Captures_And_Prepares_Replay()
        => Given("the generated fulfillment dead-letter channel example", FulfillmentDeadLetterChannelExample.RunGenerated)
            .Then("the failed message is captured with generated channel metadata", summary =>
            {
                ScenarioExpect.Equal("fulfillment-dead:fulfillment:order-200", summary.DeadLetterId);
                ScenarioExpect.Equal("warehouse rejected request", summary.Reason);
                ScenarioExpect.True(summary.ReadyForReplay);
                ScenarioExpect.Equal(summary.DeadLetterId, summary.ReplayedFrom);
            })
            .AssertPassed();

    [Scenario("Dead letter channel example is importable through IServiceCollection")]
    [Fact]
    public Task Dead_Letter_Channel_Example_Is_Importable_Through_IServiceCollection()
        => Given("a service collection importing the dead-letter channel example", () =>
            {
                var services = new ServiceCollection();
                services.AddFulfillmentDeadLetterChannelExample();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("the importing application captures a failed fulfillment command", provider =>
            {
                using (provider)
                {
                    var workflow = provider.GetRequiredService<FulfillmentDeadLetterWorkflow>();
                    return workflow.Capture(
                        FulfillmentDeadLetterChannelExample.CreateCommand("order-300"),
                        "fulfillment adapter failed");
                }
            })
            .Then("the registered workflow preserves headers and replay handoff data", summary =>
            {
                ScenarioExpect.Equal("fulfillment-dead:fulfillment:order-300", summary.DeadLetterId);
                ScenarioExpect.Equal("checkout:order-300", summary.CorrelationId);
                ScenarioExpect.True(summary.ReadyForReplay);
            })
            .AssertPassed();
}
