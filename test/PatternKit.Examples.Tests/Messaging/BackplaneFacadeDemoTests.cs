using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class BackplaneFacadeDemoTests
{
    [Scenario("BackplaneHostBuilder ConfiguresNativeMessagingPlatformSurface")]
    [Fact]
    public async Task BackplaneHostBuilder_ConfiguresNativeMessagingPlatformSurface()
    {
        var transport = new InMemoryBackplaneTransport();
        var outbox = new BackplaneOutbox();
        var idempotency = new InMemoryIdempotencyStore();

        await using var host = await BackplaneHost.Create()
            .UseTransport(() => transport)
            .UseOutbox(outbox)
            .UseIdempotencyStore(idempotency)
            .MapDefaultCommand<SubmitOrder, BackplaneOrderAccepted>("orders.standard")
            .ReceiveEndpoint("orders.standard", endpoint =>
                endpoint.HandleCommand<SubmitOrder, BackplaneOrderAccepted>((message, context, _) =>
                    new ValueTask<BackplaneOrderAccepted>(new BackplaneOrderAccepted(
                        message.Payload.OrderId,
                        "orders.standard",
                        context.Headers.CorrelationId ?? string.Empty))))
            .BuildAsync();

        var response = await host.Client.RequestAsync<SubmitOrder, BackplaneOrderAccepted>(
            Message<SubmitOrder>
                .Create(new SubmitOrder("order-builder", 10m, CustomerTier.Standard))
                .WithCorrelationId("corr-builder")
                .WithIdempotencyKey("idem-builder"));

        ScenarioExpect.Same(outbox, host.Outbox);
        ScenarioExpect.Same(idempotency, host.IdempotencyStore);
        ScenarioExpect.Single(host.Endpoints);
        ScenarioExpect.Equal("orders.standard", host.Endpoints[0].Name);
        ScenarioExpect.Single(host.Endpoints[0].Handlers);
        ScenarioExpect.Equal(new BackplaneOrderAccepted("order-builder", "orders.standard", "corr-builder"), response);
        ScenarioExpect.Contains(transport.DeliveryLog, static entry => entry.Contains("orders.standard->orders.standard", StringComparison.Ordinal));
    }

    [Scenario("RunAsync RoutesRequestsAndPublishesEventsThroughBackplane")]
    [Fact]
    public async Task RunAsync_RoutesRequestsAndPublishesEventsThroughBackplane()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        ScenarioExpect.Equal(4, summary.Accepted.Count);
        ScenarioExpect.Equal("orders.standard", summary.Accepted[0].Endpoint);
        ScenarioExpect.Equal("orders.standard", summary.Accepted[1].Endpoint);
        ScenarioExpect.Equal("orders.priority", summary.Accepted[2].Endpoint);
        ScenarioExpect.Equal("orders.standard", summary.Accepted[3].Endpoint);

        ScenarioExpect.Equal("orders.standard", summary.Endpoints["order-standard"]);
        ScenarioExpect.Equal("orders.priority", summary.Endpoints["order-vip"]);
        ScenarioExpect.Equal("orders.standard", summary.Endpoints["order-declined"]);
    }

    [Scenario("RunAsync ReplaysDuplicateRequestWithoutRepeatingSideEffects")]
    [Fact]
    public async Task RunAsync_ReplaysDuplicateRequestWithoutRepeatingSideEffects()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        ScenarioExpect.Equal("corr-order-standard", summary.Accepted[0].CorrelationId);
        ScenarioExpect.Equal(summary.Accepted[0], summary.Accepted[1]);
        ScenarioExpect.Single(summary.Audit, static entry => entry == "orders:accepted:order-standard:orders.standard");
        ScenarioExpect.Single(summary.Outbox, static record =>
            record.Address == "orders.submitted"
            && record.CorrelationId == "corr-order-standard");
    }

    [Scenario("RunAsync FansOutEventsToIndependentServices")]
    [Fact]
    public async Task RunAsync_FansOutEventsToIndependentServices()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        ScenarioExpect.Contains(summary.Audit, static entry => entry == "billing:received:order-standard");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "audit:order-submitted:order-standard:corr-order-standard");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "fulfillment:scheduled:order-standard");
        ScenarioExpect.Contains(summary.Audit, static entry => entry == "notification:order-standard:shipment-scheduled");

        ScenarioExpect.Contains(summary.DeliveryLog, static entry => entry == "orders.submitted->billing-service:BackplaneOrderSubmitted");
        ScenarioExpect.Contains(summary.DeliveryLog, static entry => entry == "orders.submitted->audit-service:BackplaneOrderSubmitted");
    }

    [Scenario("RunAsync UsesOutboxBeforeTransportDispatch")]
    [Fact]
    public async Task RunAsync_UsesOutboxBeforeTransportDispatch()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        ScenarioExpect.All(summary.Outbox, static record => ScenarioExpect.True(record.Dispatched));
        ScenarioExpect.Equal(8, summary.Outbox.Count);
        ScenarioExpect.Equal(3, summary.Outbox.Count(static record => record.Address == "orders.submitted"));
        ScenarioExpect.Equal(2, summary.Outbox.Count(static record => record.Address == "payments.captured"));
        ScenarioExpect.Equal(1, summary.Outbox.Count(static record => record.Address == "payments.declined"));
        ScenarioExpect.Equal(2, summary.Outbox.Count(static record => record.Address == "shipments.scheduled"));
        ScenarioExpect.All(summary.Outbox, static record => ScenarioExpect.InRange(record.Delivered, 1, int.MaxValue));
    }

    [Scenario("RunAsync PreservesCorrelationAcrossServices")]
    [Fact]
    public async Task RunAsync_PreservesCorrelationAcrossServices()
    {
        var summary = await BackplaneFacadeDemo.RunAsync();

        ScenarioExpect.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-standard", Kind: "shipment-scheduled", CorrelationId: "corr-order-standard" });
        ScenarioExpect.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-vip", Kind: "shipment-scheduled", CorrelationId: "corr-order-vip" });
        ScenarioExpect.Contains(summary.Notifications, static notification =>
            notification is { OrderId: "order-declined", Kind: "payment-declined", CorrelationId: "corr-order-declined" });
    }
}
