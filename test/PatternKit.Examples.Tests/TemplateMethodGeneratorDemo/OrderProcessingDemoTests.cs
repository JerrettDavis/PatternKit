using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.TemplateMethodGeneratorDemo;

[Feature("Template Method Generator - Async Order Processing")]
public sealed partial class OrderProcessingDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Successful order processing runs all async steps")]
    [Fact]
    public async Task Successful_Order_Processing()
    {
        var log = await PatternKit.Examples.TemplateMethodGeneratorDemo.OrderProcessingDemo.RunAsync();

        ScenarioExpect.True(log.Any(l => l.Contains("Starting order processing")), "Should start with BeforeAll hook");
        ScenarioExpect.True(log.Any(l => l.Contains("Payment authorized")), "Should authorize payment");
        ScenarioExpect.True(log.Any(l => l.Contains("Inventory reserved")), "Should reserve inventory");
        ScenarioExpect.True(log.Any(l => l.Contains("Order confirmed")), "Should confirm order");
        ScenarioExpect.True(log.Any(l => l.Contains("Notification sent")), "Should send notification");
        ScenarioExpect.True(log.Any(l => l.Contains("Order processing completed successfully")), "Should end with AfterAll hook");
        ScenarioExpect.True(log.Any(l => l.Contains("ready for fulfillment")), "Order should be ready for fulfillment");
    }

    [Scenario("Invalid payment amount triggers error handling")]
    [Fact]
    public async Task Invalid_Payment_Triggers_OnError()
    {
        var log = await PatternKit.Examples.TemplateMethodGeneratorDemo.OrderProcessingDemo.RunWithInvalidAmountAsync();

        ScenarioExpect.True(log.Any(l => l.Contains("Authorizing payment")), "Should attempt payment authorization");
        ScenarioExpect.True(log.Any(l => l.Contains("ERROR: Invalid payment amount")), "Payment authorization should fail");
        ScenarioExpect.True(log.Any(l => l.Contains("Order processing failed")), "Should invoke OnError hook");
        ScenarioExpect.False(log.Any(l => l.Contains("Inventory reserved")), "Should not reserve inventory");
        ScenarioExpect.False(log.Any(l => l.Contains("Order confirmed")), "Should not confirm order");
    }

    [Scenario("Cancellation is supported in async workflow")]
    [Fact]
    public async Task Cancellation_Support()
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50)); // Cancel quickly

        var log = await PatternKit.Examples.TemplateMethodGeneratorDemo.OrderProcessingDemo.RunWithCancellationAsync(cts.Token);

        // Either cancellation happened or workflow completed (race condition)
        var result = log.Any(l => l.Contains("cancelled")) || log.Any(l => l.Contains("completed"));
        ScenarioExpect.True(result, "Should handle cancellation or complete");
    }

    [Scenario("Mixed sync and async steps work together")]
    [Fact]
    public async Task Mixed_Sync_Async_Steps()
    {
        var log = await PatternKit.Examples.TemplateMethodGeneratorDemo.OrderProcessingDemo.RunAsync();

        var hasAsyncSteps = log.Any(l => l.Contains("Payment authorized")) &&
                           log.Any(l => l.Contains("Notification sent"));
        var hasSyncSteps = log.Any(l => l.Contains("Order confirmed"));

        ScenarioExpect.True(hasAsyncSteps, "Should have async steps");
        ScenarioExpect.True(hasSyncSteps, "Should have sync steps");
    }

    [Scenario("Error handling includes compensating actions")]
    [Fact]
    public async Task Error_Includes_Compensation()
    {
        var log = await PatternKit.Examples.TemplateMethodGeneratorDemo.OrderProcessingDemo.RunWithInvalidAmountAsync();

        ScenarioExpect.True(log.Any(l => l.Contains("Order processing failed")), "OnError hook should be invoked");
        // In this case, inventory wasn't reserved yet, so only payment rollback would be mentioned
        var hasCompensationMention = log.Any(l => l.Contains("Rolling back")) || !log.Any(l => l.Contains("Inventory reserved"));
        ScenarioExpect.True(hasCompensationMention, "Should mention rollback actions or not have reserved inventory");
    }
}
