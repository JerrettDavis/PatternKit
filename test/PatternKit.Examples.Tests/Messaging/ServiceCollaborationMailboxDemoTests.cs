using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ServiceCollaborationMailboxDemoTests
{
    [Fact]
    public async Task RunAsync_CoordinatesServicesAndCompensatesDeclinedPayment()
    {
        var summary = await ServiceCollaborationMailboxDemo.RunAsync();

        Assert.Contains(summary.Audit, static entry => entry == "inventory:reserved:order-ok:res-order-ok");
        Assert.Contains(summary.Audit, static entry => entry == "payment:captured:order-ok");
        Assert.Contains(summary.Audit, static entry => entry == "shipping:scheduled:order-ok");
        Assert.Contains(summary.Audit, static entry => entry == "notification:order-ok:fulfilled");

        Assert.Contains(summary.Audit, static entry => entry == "inventory:reserved:order-declined:res-order-declined");
        Assert.Contains(summary.Audit, static entry => entry == "payment:declined:order-declined");
        Assert.Contains(summary.Audit, static entry => entry == "inventory:released:order-declined:res-order-declined");
        Assert.Contains(summary.Audit, static entry => entry == "notification:order-declined:payment-declined");

        Assert.Equal(["order-ok"], summary.OpenReservations);
        Assert.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-ok", Status: "fulfilled", CorrelationId: "checkout-ok" });
        Assert.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-declined", Status: "payment-declined", CorrelationId: "checkout-declined" });
    }
}
