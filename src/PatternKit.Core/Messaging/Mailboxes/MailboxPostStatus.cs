namespace PatternKit.Messaging.Mailboxes;

/// <summary>
/// Describes the outcome of posting a message to a mailbox.
/// </summary>
public enum MailboxPostStatus
{
    /// <summary>The message was accepted into the mailbox.</summary>
    Accepted = 0,

    /// <summary>The message was rejected without being enqueued.</summary>
    Rejected = 1,

    /// <summary>The message was dropped without being processed.</summary>
    Dropped = 2
}
