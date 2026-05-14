using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class BackplaneFacadeDemoTests
{
    [Fact]
    public async Task RunAsync_RoutesRequestsAndPublishesEventsThroughBackplane()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        Assert.Equal(4, summary.Accepted.Count);
        Assert.Equal("orders.standard", summary.Accepted[0].Endpoint);
        Assert.Equal("orders.standard", summary.Accepted[1].Endpoint);
        Assert.Equal("orders.priority", summary.Accepted[2].Endpoint);
        Assert.Equal("orders.standard", summary.Accepted[3].Endpoint);

        Assert.Equal("orders.standard", summary.Endpoints["order-standard"]);
        Assert.Equal("orders.priority", summary.Endpoints["order-vip"]);
        Assert.Equal("orders.standard", summary.Endpoints["order-declined"]);
    }

    [Fact]
    public async Task RunAsync_ReplaysDuplicateRequestWithoutRepeatingSideEffects()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        Assert.Equal("corr-order-standard", summary.Accepted[0].CorrelationId);
        Assert.Equal(summary.Accepted[0], summary.Accepted[1]);
        Assert.Single(summary.Audit, static entry => entry == "orders:accepted:order-standard:orders.standard");
        Assert.Single(summary.Outbox, static record =>
            record.Address == "orders.submitted"
            && record.CorrelationId == "corr-order-standard");
    }

    [Fact]
    public async Task RunAsync_FansOutEventsToIndependentServices()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        Assert.Contains(summary.Audit, static entry => entry == "billing:received:order-standard");
        Assert.Contains(summary.Audit, static entry => entry == "audit:order-submitted:order-standard:corr-order-standard");
        Assert.Contains(summary.Audit, static entry => entry == "fulfillment:scheduled:order-standard");
        Assert.Contains(summary.Audit, static entry => entry == "notification:order-standard:shipment-scheduled");

        Assert.Contains(summary.DeliveryLog, static entry => entry == "orders.submitted->billing-service:BackplaneOrderSubmitted");
        Assert.Contains(summary.DeliveryLog, static entry => entry == "orders.submitted->audit-service:BackplaneOrderSubmitted");
    }

    [Fact]
    public async Task RunAsync_UsesOutboxBeforeTransportDispatch()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        Assert.All(summary.Outbox, static record => Assert.True(record.Dispatched));
        Assert.Equal(8, summary.Outbox.Count);
        Assert.Equal(3, summary.Outbox.Count(static record => record.Address == "orders.submitted"));
        Assert.Equal(2, summary.Outbox.Count(static record => record.Address == "payments.captured"));
        Assert.Equal(1, summary.Outbox.Count(static record => record.Address == "payments.declined"));
        Assert.Equal(2, summary.Outbox.Count(static record => record.Address == "shipments.scheduled"));
        Assert.All(summary.Outbox, static record => Assert.InRange(record.Delivered, 1, int.MaxValue));
    }

    [Fact]
    public async Task RunAsync_PreservesCorrelationAcrossServices()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        Assert.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-standard", Kind: "shipment-scheduled", CorrelationId: "corr-order-standard" });
        Assert.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-vip", Kind: "shipment-scheduled", CorrelationId: "corr-order-vip" });
        Assert.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-declined", Kind: "payment-declined", CorrelationId: "corr-order-declined" });
    }
}
