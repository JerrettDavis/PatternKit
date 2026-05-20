using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using PatternKit.Generators.Messaging;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates in-process enterprise message routing primitives.
/// </summary>
public static class MessageRoutingExample
{
    /// <summary>
    /// Runs a small order routing flow through router, recipient list, splitter, and aggregator.
    /// </summary>
    public static RoutingSummary Run() => RunFluent();

    /// <summary>
    /// Runs the routing flow with fluent Splitter and Aggregator factories.
    /// </summary>
    public static RoutingSummary RunFluent()
    {
        var message = CreateOrderMessage();
        var splitter = Splitter<RoutedOrder, RoutedLine>.Create()
            .Use((m, _) => m.Payload.Lines)
            .Build();
        var lineMessages = splitter.Split(message);
        var aggregator = Aggregator<string, RoutedLine, decimal>.Create()
            .KeyBy((m, _) => m.Headers.CorrelationId ?? m.Payload.Sku)
            .CompleteWhen((_, messages, _) => messages.Count == lineMessages.Count)
            .Project((_, messages, _) => messages.Sum(m => m.Payload.Amount))
            .Build();

        return RunWith(message, lineMessages, aggregator, "fluent");
    }

    /// <summary>
    /// Runs the routing flow with source-generated Splitter and Aggregator factories.
    /// </summary>
    public static RoutingSummary RunGenerated()
    {
        var message = CreateOrderMessage();
        var lineMessages = GeneratedOrderLineSplitter.CreateLineSplitter().Split(message);
        var aggregator = GeneratedOrderLineAggregator.CreateLineTotalAggregator();
        return RunWith(message, lineMessages, aggregator, "source-generated");
    }

    private static RoutingSummary RunWith(
        Message<RoutedOrder> message,
        IReadOnlyList<Message<RoutedLine>> lineMessages,
        Aggregator<string, RoutedLine, decimal> aggregator,
        string path)
    {
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
            lineMessages[0].Headers.CausationId!,
            path);
    }

    private static Message<RoutedOrder> CreateOrderMessage()
    {
        var order = new RoutedOrder("order-42", "enterprise", [
            new RoutedLine("sku-1", 30m),
            new RoutedLine("sku-2", 70m)
        ]);

        return Message<RoutedOrder>
            .Create(order)
            .WithMessageId("msg-order-42")
            .WithCorrelationId(order.Id);
    }
}

/// <summary>DI-friendly entry points for fluent and generated message routing examples.</summary>
public sealed record MessageRoutingExampleRunner(Func<RoutingSummary> RunFluent, Func<RoutingSummary> RunGenerated);

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
    string CausationId,
    string Path);

[GenerateSplitter(typeof(RoutedOrder), typeof(RoutedLine), FactoryName = "CreateLineSplitter")]
public static partial class GeneratedOrderLineSplitter
{
    [SplitterProjection]
    private static IEnumerable<RoutedLine> ProjectLines(Message<RoutedOrder> message, MessageContext context)
        => message.Payload.Lines;
}

[GenerateAggregator(typeof(string), typeof(RoutedLine), typeof(decimal), FactoryName = "CreateLineTotalAggregator", DuplicatePolicy = "Ignore")]
public static partial class GeneratedOrderLineAggregator
{
    [AggregatorCorrelation]
    private static string Correlate(Message<RoutedLine> message, MessageContext context)
        => message.Headers.CorrelationId ?? message.Payload.Sku;

    [AggregatorCompletion]
    private static bool Complete(string key, IReadOnlyList<Message<RoutedLine>> messages, MessageContext context)
        => messages.Count == 2;

    [AggregatorProjection]
    private static decimal Project(string key, IReadOnlyList<Message<RoutedLine>> messages, MessageContext context)
        => messages.Sum(message => message.Payload.Amount);
}
