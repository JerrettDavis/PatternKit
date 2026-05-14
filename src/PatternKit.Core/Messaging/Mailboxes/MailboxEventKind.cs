namespace PatternKit.Messaging.Mailboxes;

/// <summary>
/// Describes mailbox lifecycle and processing events.
/// </summary>
public enum MailboxEventKind
{
    /// <summary>The mailbox started its single-consumer pump.</summary>
    Started = 0,

    /// <summary>A message was accepted into the mailbox.</summary>
    Accepted = 1,

    /// <summary>A message was rejected because the mailbox was stopped or full.</summary>
    Rejected = 2,

    /// <summary>A message was dropped by a configured backpressure or stop policy.</summary>
    Dropped = 3,

    /// <summary>A message handler started.</summary>
    ProcessingStarted = 4,

    /// <summary>A message handler completed successfully.</summary>
    ProcessingCompleted = 5,

    /// <summary>A message handler failed.</summary>
    Failed = 6,

    /// <summary>The mailbox stopped its single-consumer pump.</summary>
    Stopped = 7
}
