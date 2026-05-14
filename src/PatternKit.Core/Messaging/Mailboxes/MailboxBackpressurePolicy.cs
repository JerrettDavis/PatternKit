namespace PatternKit.Messaging.Mailboxes;

/// <summary>
/// Defines how a bounded mailbox reacts when its queue is full.
/// </summary>
public enum MailboxBackpressurePolicy
{
    /// <summary>Waits until space is available or the enqueue cancellation token is canceled.</summary>
    Wait = 0,

    /// <summary>Rejects the incoming message without enqueueing it.</summary>
    Reject = 1,

    /// <summary>Drops the incoming message without enqueueing it.</summary>
    DropNewest = 2,

    /// <summary>Drops the oldest queued message and enqueues the incoming message.</summary>
    DropOldest = 3
}
