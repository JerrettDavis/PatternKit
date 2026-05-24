using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "RoutingSlip")]
public class RoutingSlipBenchmarks
{
    private static readonly Message<FulfillmentOrder> Order = Message<FulfillmentOrder>.Create(new("order-42", "new"));

    [Benchmark(Baseline = true, Description = "Fluent: create routing slip")]
    [BenchmarkCategory("Fluent", "Construction")]
    public RoutingSlip<FulfillmentOrder> Fluent_CreateRoutingSlip()
        => CreateFluentRoutingSlip();

    [Benchmark(Description = "Generated: create routing slip")]
    [BenchmarkCategory("Generated", "Construction")]
    public RoutingSlip<FulfillmentOrder> Generated_CreateRoutingSlip()
        => OrderFulfillmentSlip.Create();

    [Benchmark(Description = "Fluent: execute fulfillment itinerary")]
    [BenchmarkCategory("Fluent", "Execution")]
    public RoutingSlipSummary Fluent_ExecuteFulfillmentItinerary()
    {
        var result = CreateFluentRoutingSlip().Execute(Order);
        return ToSummary(result);
    }

    [Benchmark(Description = "Generated: execute fulfillment itinerary")]
    [BenchmarkCategory("Generated", "Execution")]
    public RoutingSlipSummary Generated_ExecuteFulfillmentItinerary()
        => RoutingSlipExample.Run();

    private static RoutingSlip<FulfillmentOrder> CreateFluentRoutingSlip()
        => RoutingSlip<FulfillmentOrder>.Create()
            .Step("validate", static (message, _) => message.WithPayload(message.Payload with { Status = "validated" }))
            .Step("reserve-inventory", static (message, _) => message.WithPayload(message.Payload with { Status = $"{message.Payload.Status},reserved" }))
            .Step("ship", static (message, _) => message.WithPayload(message.Payload with { Status = $"{message.Payload.Status},shipped" }))
            .Build();

    private static RoutingSlipSummary ToSummary(RoutingSlipResult<FulfillmentOrder> result)
        => new(
            result.Message.Payload.Status,
            result.CompletedSteps.ToArray(),
            result.Message.Headers.GetString(MessageHeaderNames.RoutingSlipIndex)!);
}
