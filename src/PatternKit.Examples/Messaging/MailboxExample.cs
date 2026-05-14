using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates a bounded mailbox that serializes background-style message handling.
/// </summary>
public static class MailboxExample
{
    /// <summary>Runs a bounded mailbox and returns the processed work item identifiers.</summary>
    public static async ValueTask<IReadOnlyList<string>> RunAsync()
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
}

/// <summary>Mailbox example payload.</summary>
public sealed record MailboxWorkItem(string Id);
