namespace PatternKit.Messaging.Mailboxes;

/// <summary>
/// Lightweight mailbox event payload for diagnostics and metrics adapters.
/// </summary>
public sealed class MailboxEvent
{
    internal MailboxEvent(MailboxEventKind kind, long sequence, int queuedCount, Exception? exception = null)
    {
        Kind = kind;
        Sequence = sequence;
        QueuedCount = queuedCount;
        Exception = exception;
    }

    /// <summary>The event kind.</summary>
    public MailboxEventKind Kind { get; }

    /// <summary>The mailbox sequence number associated with the event, or zero for lifecycle events.</summary>
    public long Sequence { get; }

    /// <summary>The queued message count observed when the event was raised.</summary>
    public int QueuedCount { get; }

    /// <summary>The exception associated with a failure event.</summary>
    public Exception? Exception { get; }
}
