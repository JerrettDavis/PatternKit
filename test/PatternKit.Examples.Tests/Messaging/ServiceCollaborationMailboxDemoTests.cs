using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ServiceCollaborationMailboxDemoTests
{
    [Scenario("RunAsync CoordinatesServicesAndCompensatesDeclinedPayment")]
    [Fact]
    public async Task RunAsync_CoordinatesServicesAndCompensatesDeclinedPayment()
    {
        var summary = await ServiceCollaborationMailboxDemo.RunAsync();

        ScenarioExpect.Contains(summary.Audit, static entry => entry == "inventory:reserved:order-ok:res-order-ok");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "payment:captured:order-ok");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "shipping:scheduled:order-ok");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "notification:order-ok:fulfilled");

        ScenarioExpect.Contains(summary.Audit, static entry => entry == "inventory:reserved:order-declined:res-order-declined");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "payment:declined:order-declined");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "inventory:released:order-declined:res-order-declined");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "notification:order-declined:payment-declined");

        ScenarioExpect.Equal(["order-ok"], summary.OpenReservations);
        ScenarioExpect.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-ok", Status: "fulfilled", CorrelationId: "checkout-ok" });
        ScenarioExpect.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-declined", Status: "payment-declined", CorrelationId: "checkout-declined" });
    }
}
