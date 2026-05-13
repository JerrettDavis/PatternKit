using PatternKit.Messaging;
using PatternKit.Messaging.Routing;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates in-process enterprise message routing primitives.
/// </summary>
public static class MessageRoutingExample
{
    /// <summary>
    /// Runs a small order routing flow through router, recipient list, splitter, and aggregator.
    /// </summary>
    public static RoutingSummary Run()
    {
        var order = new RoutedOrder("order-42", "enterprise", [
            new RoutedLine("sku-1", 30m),
            new RoutedLine("sku-2", 70m)
        ]);

        var message = Message<RoutedOrder>
            .Create(order)
            .WithMessageId("msg-order-42")
            .WithCorrelationId(order.Id);

        var route = ContentRouter<RoutedOrder, string>.Create()
            .When((m, _) => m.Payload.CustomerTier == "enterprise").Then((_, _) => "priority")
            .Default((_, _) => "standard")
            .Build()
            .Route(message);

        var recipients = new List<string>();
        var recipientList = RecipientList<RoutedOrder>.Create()
            .To("audit", (_, _) => recipients.Add("audit"))
            .When("billing", (m, _) => m.Payload.Lines.Count > 0).Then((_, _) => recipients.Add("billing"))
            .Build();

        var recipientResult = recipientList.Dispatch(message);

        var lineMessages = Splitter<RoutedOrder, RoutedLine>.Create()
            .Use((m, _) => m.Payload.Lines)
            .Build()
            .Split(message);

        var aggregator = Aggregator<string, RoutedLine, decimal>.Create()
            .KeyBy((m, _) => m.Headers.CorrelationId ?? m.Payload.Sku)
            .CompleteWhen((_, messages, _) => messages.Count == lineMessages.Count)
            .Project((_, messages, _) => messages.Sum(m => m.Payload.Amount))
            .Build();

        decimal total = 0m;
        foreach (var line in lineMessages)
        {
            var result = aggregator.Add(line.WithMessageId($"line:{line.Payload.Sku}"));
            if (result.Completed)
                total = result.Result;
        }

        return new RoutingSummary(
            route,
            recipientResult.DeliveredRecipients.ToArray(),
            lineMessages.Count,
            total,
            lineMessages[0].Headers.CausationId!);
    }
}

/// <summary>Example order payload routed by the messaging demo.</summary>
public sealed record RoutedOrder(string Id, string CustomerTier, IReadOnlyList<RoutedLine> Lines);

/// <summary>Example split item payload.</summary>
public sealed record RoutedLine(string Sku, decimal Amount);

/// <summary>Example output for the enterprise message routing demo.</summary>
public sealed record RoutingSummary(
    string Route,
    IReadOnlyList<string> Recipients,
    int SplitCount,
    decimal AggregatedTotal,
    string CausationId);
