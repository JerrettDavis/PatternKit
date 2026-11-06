using PatternKit.EnterpriseIntegration.EventNotification;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.EnterpriseIntegration.EventNotification;

[Feature("Event Notification")]
public sealed class EventNotificationTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Event notification publishes compact metadata")]
    [Fact]
    public Task Event_Notification_Publishes_Compact_Metadata()
        => Given("an order event notification", CreateNotification)
        .When("an accepted order event is notified", notification => notification.Notify(new OrderAccepted("O-100", "C-900", "web", true)))
        .Then("subscribers receive the key correlation and compact metadata", result =>
        {
            ScenarioExpect.True(result.Published);
            ScenarioExpect.Equal("order-accepted", result.NotificationName);
            ScenarioExpect.Equal("O-100", result.Key);
            ScenarioExpect.Equal("C-900", result.CorrelationId);
            ScenarioExpect.Equal("web", result.Metadata["source"]);
        })
        .AssertPassed();

    [Scenario("Event notification can skip events before publishing")]
    [Fact]
    public Task Event_Notification_Can_Skip_Events_Before_Publishing()
        => Given("an order notification with a dispatch rule", CreateNotification)
        .When("an event does not satisfy the rule", notification => notification.Notify(new OrderAccepted("O-100", "C-900", "web", false)))
        .Then("the notification is skipped without failure", result =>
        {
            ScenarioExpect.True(result.Skipped);
            ScenarioExpect.False(result.Failed);
        })
        .AssertPassed();

    [Scenario("Event notification validates configuration")]
    [Fact]
    public Task Event_Notification_Validates_Configuration()
        => Given("invalid notification configuration", () => true)
        .Then("invalid names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => EventNotification<OrderAccepted, string>.Create("")
                .WithKey(static evt => evt.OrderId)
                .Build()))
        .And("missing key selectors are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => EventNotification<OrderAccepted, string>.Create().Build()))
        .And("null callbacks are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => EventNotification<OrderAccepted, string>.Create().WithKey(null!)))
        .And("duplicate metadata names are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => EventNotification<OrderAccepted, string>.Create()
                .WithKey(static evt => evt.OrderId)
                .WithMetadata("source", static evt => evt.Source)
                .WithMetadata("SOURCE", static evt => evt.Source)))
        .And("null events are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateNotification().Notify(null!)))
        .AssertPassed();

    private static EventNotification<OrderAccepted, string> CreateNotification()
        => EventNotification<OrderAccepted, string>.Create("order-accepted")
            .When(static evt => evt.NotifySubscribers)
            .WithKey(static evt => evt.OrderId)
            .WithCorrelation(static evt => evt.CorrelationId)
            .WithMetadata("source", static evt => evt.Source)
            .Build();

    private sealed record OrderAccepted(string OrderId, string CorrelationId, string Source, bool NotifySubscribers);
}
