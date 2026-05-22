using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.EventNotificationDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.EventNotificationDemo;

[Feature("Order Event Notification example")]
public sealed class OrderEventNotificationDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Fluent and generated paths publish compact order notifications")]
    [Fact]
    public Task Fluent_And_Generated_Paths_Publish_Compact_Order_Notifications()
        => Given("an order accepted event", () => new OrderAcceptedNotificationEvent("O-100", "C-900", "web", true))
        .When("fluent and generated notifications handle the event", evt => new
        {
            Fluent = OrderEventNotificationDemoRunner.RunFluent(),
            Generated = BuildServiceProvider().GetRequiredService<OrderEventNotificationDemoRunner>().RunGenerated(evt)
        })
        .Then("both paths publish compact notification metadata", result =>
        {
            ScenarioExpect.Equal("O-100", result.Fluent!.OrderId);
            ScenarioExpect.Equal("C-900", result.Generated!.CorrelationId);
            ScenarioExpect.Equal("web", result.Generated.Source);
        })
        .AssertPassed();

    [Scenario("Order notification is importable through AddPatternKitExamples")]
    [Fact]
    public Task Order_Notification_Is_Importable_Through_AddPatternKitExamples()
        => Given("the aggregate PatternKit example registration", () => new ServiceCollection().AddPatternKitExamples().BuildServiceProvider())
        .When("the order notification example is resolved", provider => provider.GetRequiredService<OrderEventNotificationExample>())
        .Then("the runner and service are available through standard IoC", example =>
        {
            var summary = example.Runner.RunGenerated(new OrderAcceptedNotificationEvent("O-200", "C-901", "mobile", true));
            ScenarioExpect.Equal("O-200", summary!.OrderId);
            ScenarioExpect.NotNull(example.Service);
        })
        .AssertPassed();

    private static ServiceProvider BuildServiceProvider()
        => new ServiceCollection()
            .AddOrderEventNotificationDemo()
            .BuildServiceProvider();
}
