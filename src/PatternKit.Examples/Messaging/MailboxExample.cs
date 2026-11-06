using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates a bounded mailbox that serializes background-style message handling.
/// </summary>
public static class MailboxExample
{
    /// <summary>Runs a bounded mailbox and returns the processed work item identifiers.</summary>
    public static ValueTask<IReadOnlyList<string>> RunAsync() => RunFluentAsync();

    /// <summary>Runs a bounded mailbox built with the fluent runtime API.</summary>
    public static async ValueTask<IReadOnlyList<string>> RunFluentAsync()
    {
        var processed = new List<string>();
        using var mailbox = Mailbox<MailboxWorkItem>.Create((message, context, cancellationToken) =>
            {
                processed.Add($"{context.Headers.GetString(MessageHeaderNames.CorrelationId)}:{message.Payload.Id}");
                return default;
            })
            .Bounded(8, MailboxBackpressurePolicy.Wait)
            .OnError(MailboxErrorPolicy.Continue)
            .Build();

        await mailbox.StartAsync();

        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("batch-42"));
        await mailbox.PostAsync(Message<MailboxWorkItem>.Create(new MailboxWorkItem("prepare")), context);
        await mailbox.PostAsync(Message<MailboxWorkItem>.Create(new MailboxWorkItem("ship")), context);

        await mailbox.StopAsync();
        return processed;
    }

    /// <summary>Runs a bounded mailbox built with the source-generated factory.</summary>
    public static async ValueTask<IReadOnlyList<string>> RunGeneratedAsync()
    {
        GeneratedMailboxWorkQueue.Processed.Clear();
        using var mailbox = GeneratedMailboxWorkQueue.CreateWorkQueue();

        await mailbox.StartAsync();

        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("batch-42"));
        await mailbox.PostAsync(Message<MailboxWorkItem>.Create(new MailboxWorkItem("prepare")), context);
        await mailbox.PostAsync(Message<MailboxWorkItem>.Create(new MailboxWorkItem("ship")), context);

        await mailbox.StopAsync();
        return GeneratedMailboxWorkQueue.Processed.ToArray();
    }
}

/// <summary>DI-friendly entry points for fluent and generated mailbox examples.</summary>
public sealed record MailboxExampleRunner(
    Func<ValueTask<IReadOnlyList<string>>> RunFluentAsync,
    Func<ValueTask<IReadOnlyList<string>>> RunGeneratedAsync);

/// <summary>Mailbox example payload.</summary>
public sealed record MailboxWorkItem(string Id);

[GenerateMailbox(typeof(MailboxWorkItem), FactoryName = "CreateWorkQueue", Capacity = 8, BackpressurePolicy = "Wait", ErrorPolicy = "Continue")]
public static partial class GeneratedMailboxWorkQueue
{
    public static readonly List<string> Processed = [];

    [MailboxHandler]
    private static ValueTask Handle(Message<MailboxWorkItem> message, MessageContext context, CancellationToken cancellationToken)
    {
        Processed.Add($"{context.Headers.GetString(MessageHeaderNames.CorrelationId)}:{message.Payload.Id}");
        return default;
    }
}
