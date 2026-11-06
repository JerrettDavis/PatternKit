namespace PatternKit.Messaging.Mailboxes;

/// <summary>
/// Result returned when a message is posted to a mailbox.
/// </summary>
public sealed class MailboxPostResult
{
    internal MailboxPostResult(MailboxPostStatus status, long sequence, string? reason)
    {
        Status = status;
        Sequence = sequence;
        Reason = reason;
    }

    /// <summary>The post status.</summary>
    public MailboxPostStatus Status { get; }

    /// <summary>The mailbox-assigned sequence number, or zero when the message was not accepted.</summary>
    public long Sequence { get; }

    /// <summary>A short machine-readable reason for rejected or dropped posts.</summary>
    public string? Reason { get; }

    /// <summary>Gets whether the message was accepted into the mailbox.</summary>
    public bool Accepted => Status == MailboxPostStatus.Accepted;

    /// <summary>Creates an accepted result.</summary>
    public static MailboxPostResult AcceptedResult(long sequence) => new(MailboxPostStatus.Accepted, sequence, null);

    /// <summary>Creates a rejected result.</summary>
    public static MailboxPostResult Rejected(string reason) => new(MailboxPostStatus.Rejected, 0, reason);

    /// <summary>Creates a dropped result.</summary>
    public static MailboxPostResult Dropped(string reason) => new(MailboxPostStatus.Dropped, 0, reason);
}
