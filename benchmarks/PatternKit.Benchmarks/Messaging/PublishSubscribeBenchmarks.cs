using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "PublishSubscribe")]
public class PublishSubscribeBenchmarks
{
    private static readonly MessageHeaders Headers =
        MessageHeaders.Empty
            .WithMessageId("msg-order-submitted")
            .WithCorrelationId("corr-order-submitted");

    private static readonly BenchmarkOrderSubmitted Event = new("order-100", 125m);

    [Benchmark(Baseline = true, Description = "Fluent: configure publish-subscribe topology")]
    [BenchmarkCategory("Fluent", "Construction")]
    public BackplaneHostBuilder Fluent_ConfigurePublishSubscribeTopology()
        => ConfigureFluent(new BackplaneHostBuilder(), new BenchmarkPublishSubscribeServices());

    [Benchmark(Description = "Generated: configure publish-subscribe topology")]
    [BenchmarkCategory("Generated", "Construction")]
    public BackplaneHostBuilder Generated_ConfigurePublishSubscribeTopology()
        => BenchmarkPublishSubscribeTopology.Configure(new BackplaneHostBuilder(), new BenchmarkPublishSubscribeServices());

    [Benchmark(Description = "Fluent: publish event to subscribers")]
    [BenchmarkCategory("Fluent", "Execution")]
    public async ValueTask<int> Fluent_PublishEventToSubscribers()
    {
        var services = new BenchmarkPublishSubscribeServices();
        await using var host = await ConfigureFluent(new BackplaneHostBuilder(), services)
            .BuildAsync()
            .ConfigureAwait(false);

        await host.Client.PublishAsync("orders.submitted", Event, Headers).ConfigureAwait(false);
        return services.Deliveries.Count;
    }

    [Benchmark(Description = "Generated: publish event to subscribers")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<int> Generated_PublishEventToSubscribers()
    {
        var services = new BenchmarkPublishSubscribeServices();
        await using var host = await BenchmarkPublishSubscribeTopology.Configure(new BackplaneHostBuilder(), services)
            .BuildAsync()
            .ConfigureAwait(false);

        await host.Client.PublishAsync("orders.submitted", Event, Headers).ConfigureAwait(false);
        return services.Deliveries.Count;
    }

    internal static BackplaneHostBuilder ConfigureFluent(
        BackplaneHostBuilder builder,
        BenchmarkPublishSubscribeServices services)
        => builder
            .ReceiveEndpoint("billing-service", endpoint =>
                endpoint.Subscribe<BenchmarkOrderSubmitted>("orders.submitted", services.CapturePaymentAsync))
            .ReceiveEndpoint("audit-service", endpoint =>
                endpoint.Subscribe<BenchmarkOrderSubmitted>("orders.submitted", services.AuditOrderAsync));
}

[GenerateBackplaneTopology(typeof(BenchmarkPublishSubscribeServices), HostBuilderType = typeof(BackplaneHostBuilder))]
[BackplaneSubscription(
    typeof(BenchmarkOrderSubmitted),
    "orders.submitted",
    "billing-service",
    nameof(BenchmarkPublishSubscribeServices.CapturePaymentAsync))]
[BackplaneSubscription(
    typeof(BenchmarkOrderSubmitted),
    "orders.submitted",
    "audit-service",
    nameof(BenchmarkPublishSubscribeServices.AuditOrderAsync))]
public static partial class BenchmarkPublishSubscribeTopology;

public sealed class BenchmarkPublishSubscribeServices
{
    private readonly ConcurrentQueue<string> _deliveries = new();

    public IReadOnlyCollection<string> Deliveries => _deliveries;

    public ValueTask CapturePaymentAsync(
        Message<BenchmarkOrderSubmitted> message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        _deliveries.Enqueue($"billing:{message.Payload.OrderId}");
        return default;
    }

    public ValueTask AuditOrderAsync(
        Message<BenchmarkOrderSubmitted> message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _deliveries.Enqueue($"audit:{message.Payload.OrderId}:{context.Headers.CorrelationId}");
        return default;
    }
}

public sealed record BenchmarkOrderSubmitted(string OrderId, decimal Total);
