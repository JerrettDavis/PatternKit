using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates fluent and source-generated Recipient List integration for event fan-out.
/// </summary>
public static class RecipientListGeneratorExample
{
    public static RecipientListSummary RunFluent()
    {
        var deliveries = new List<string>();
        var message = Message<GeneratedShipmentEvent>.Create(new("order-42", "priority", 125m));
        var context = MessageContext.From(message).WithItem(GeneratedShipmentRecipients.DeliveryLogKey, deliveries);

        var list = PatternKit.Messaging.Routing.RecipientList<GeneratedShipmentEvent>.Create()
            .When("priority-audit", static (msg, _) => msg.Payload.Priority == "priority")
            .Then(static (_, ctx) => GeneratedShipmentRecipients.Record(ctx, "priority-audit"))
            .When("billing-ledger", static (msg, _) => msg.Payload.Total >= 100m)
            .Then(static (_, ctx) => GeneratedShipmentRecipients.Record(ctx, "billing-ledger"))
            .Build();

        var result = list.Dispatch(message, context);
        return new RecipientListSummary("fluent", result.DeliveredRecipients, deliveries);
    }

    public static RecipientListSummary RunGenerated()
    {
        var deliveries = new List<string>();
        var message = Message<GeneratedShipmentEvent>.Create(new("order-42", "priority", 125m));
        var context = MessageContext.From(message).WithItem(GeneratedShipmentRecipients.DeliveryLogKey, deliveries);

        var result = GeneratedShipmentRecipients.Create().Dispatch(message, context);
        return new RecipientListSummary("source-generated", result.DeliveredRecipients, deliveries);
    }

    public static IServiceCollection AddRecipientListGeneratorExample(this IServiceCollection services)
        => services.AddSingleton(new RecipientListGeneratorExampleRunner(RunFluent, RunGenerated));
}

public sealed record RecipientListGeneratorExampleRunner(
    Func<RecipientListSummary> RunFluent,
    Func<RecipientListSummary> RunGenerated);

public sealed record GeneratedShipmentEvent(string OrderId, string Priority, decimal Total);

public sealed record RecipientListSummary(
    string Path,
    IReadOnlyList<string> DeliveredRecipients,
    IReadOnlyList<string> DeliveryLog);

[GenerateRecipientList(typeof(GeneratedShipmentEvent))]
public static partial class GeneratedShipmentRecipients
{
    internal const string DeliveryLogKey = "recipient-list-deliveries";

    internal static void Record(MessageContext context, string recipient)
    {
        if (context.TryGetItem<List<string>>(DeliveryLogKey, out var deliveries) && deliveries is not null)
            deliveries.Add(recipient);
    }

    private static bool IsPriority(Message<GeneratedShipmentEvent> message, MessageContext context)
        => message.Payload.Priority == "priority";

    private static bool IsBillable(Message<GeneratedShipmentEvent> message, MessageContext context)
        => message.Payload.Total >= 100m;

    [RecipientListRecipient("priority-audit", 10, nameof(IsPriority))]
    private static void PriorityAudit(Message<GeneratedShipmentEvent> message, MessageContext context)
        => Record(context, "priority-audit");

    [RecipientListRecipient("billing-ledger", 20, nameof(IsBillable))]
    private static void BillingLedger(Message<GeneratedShipmentEvent> message, MessageContext context)
        => Record(context, "billing-ledger");
}
