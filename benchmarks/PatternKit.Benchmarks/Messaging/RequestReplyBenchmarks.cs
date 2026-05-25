using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "RequestReply")]
public class RequestReplyBenchmarks
{
    private static readonly Message<BenchmarkOrderRequest> Request =
        Message<BenchmarkOrderRequest>
            .Create(new("order-100", 125m))
            .WithCorrelationId("corr-order-100");

    [Benchmark(Baseline = true, Description = "Fluent: configure request-reply topology")]
    [BenchmarkCategory("Fluent", "Construction")]
    public BackplaneHostBuilder Fluent_ConfigureRequestReplyTopology()
        => ConfigureFluent(new BackplaneHostBuilder(), new BenchmarkRequestReplyServices());

    [Benchmark(Description = "Generated: configure request-reply topology")]
    [BenchmarkCategory("Generated", "Construction")]
    public BackplaneHostBuilder Generated_ConfigureRequestReplyTopology()
        => BenchmarkRequestReplyTopology.Configure(new BackplaneHostBuilder(), new BenchmarkRequestReplyServices());

    [Benchmark(Description = "Fluent: send request and receive reply")]
    [BenchmarkCategory("Fluent", "Execution")]
    public async ValueTask<BenchmarkOrderReply> Fluent_SendRequestAndReceiveReply()
    {
        await using var host = await ConfigureFluent(new BackplaneHostBuilder(), new BenchmarkRequestReplyServices())
            .BuildAsync()
            .ConfigureAwait(false);

        return await host.Client.RequestAsync<BenchmarkOrderRequest, BenchmarkOrderReply>(Request).ConfigureAwait(false);
    }

    [Benchmark(Description = "Generated: send request and receive reply")]
    [BenchmarkCategory("Generated", "Execution")]
    public async ValueTask<BenchmarkOrderReply> Generated_SendRequestAndReceiveReply()
    {
        await using var host = await BenchmarkRequestReplyTopology.Configure(
                new BackplaneHostBuilder(),
                new BenchmarkRequestReplyServices())
            .BuildAsync()
            .ConfigureAwait(false);

        return await host.Client.RequestAsync<BenchmarkOrderRequest, BenchmarkOrderReply>(Request).ConfigureAwait(false);
    }

    internal static BackplaneHostBuilder ConfigureFluent(
        BackplaneHostBuilder builder,
        BenchmarkRequestReplyServices services)
        => builder
            .MapDefaultCommand<BenchmarkOrderRequest, BenchmarkOrderReply>("orders.request-reply")
            .ReceiveEndpoint("orders.request-reply", endpoint =>
                endpoint.HandleCommand<BenchmarkOrderRequest, BenchmarkOrderReply>(services.AcceptOrderAsync));
}

[GenerateBackplaneTopology(typeof(BenchmarkRequestReplyServices), HostBuilderType = typeof(BackplaneHostBuilder))]
[BackplaneRequestReply(
    typeof(BenchmarkOrderRequest),
    typeof(BenchmarkOrderReply),
    "orders.request-reply",
    nameof(BenchmarkRequestReplyServices.AcceptOrderAsync))]
public static partial class BenchmarkRequestReplyTopology;

public sealed class BenchmarkRequestReplyServices
{
    public ValueTask<BenchmarkOrderReply> AcceptOrderAsync(
        Message<BenchmarkOrderRequest> message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return new ValueTask<BenchmarkOrderReply>(new BenchmarkOrderReply(
            message.Payload.OrderId,
            "accepted",
            context.Headers.CorrelationId ?? string.Empty));
    }
}

public sealed record BenchmarkOrderRequest(string OrderId, decimal Total);

public sealed record BenchmarkOrderReply(string OrderId, string Status, string CorrelationId);
