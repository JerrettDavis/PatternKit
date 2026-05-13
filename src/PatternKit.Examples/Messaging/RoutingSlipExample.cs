using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates generated routing slip factories over in-process message steps.
/// </summary>
public static class RoutingSlipExample
{
    /// <summary>Runs a generated order fulfillment routing slip.</summary>
    public static RoutingSlipSummary Run()
    {
        var result = OrderFulfillmentSlip.Create()
            .Execute(Message<FulfillmentOrder>.Create(new FulfillmentOrder("order-42", "new")));

        return new RoutingSlipSummary(
            result.Message.Payload.Status,
            result.CompletedSteps.ToArray(),
            result.Message.Headers.GetString(MessageHeaderNames.RoutingSlipIndex)!);
    }
}

/// <summary>Generated routing slip demo payload.</summary>
public sealed record FulfillmentOrder(string Id, string Status);

/// <summary>Generated routing slip demo summary.</summary>
public sealed record RoutingSlipSummary(string Status, IReadOnlyList<string> Steps, string RoutingIndex);

/// <summary>Generated routing slip demo itinerary.</summary>
[GenerateRoutingSlip(typeof(FulfillmentOrder))]
public static partial class OrderFulfillmentSlip
{
    [RoutingSlipStep("validate", 10)]
    private static Message<FulfillmentOrder> Validate(Message<FulfillmentOrder> message, MessageContext context)
        => message.WithPayload(message.Payload with { Status = "validated" });

    [RoutingSlipStep("reserve-inventory", 20)]
    private static Message<FulfillmentOrder> ReserveInventory(Message<FulfillmentOrder> message, MessageContext context)
        => message.WithPayload(message.Payload with { Status = $"{message.Payload.Status},reserved" });

    [RoutingSlipStep("ship", 30)]
    private static Message<FulfillmentOrder> Ship(Message<FulfillmentOrder> message, MessageContext context)
        => message.WithPayload(message.Payload with { Status = $"{message.Payload.Status},shipped" });
}
