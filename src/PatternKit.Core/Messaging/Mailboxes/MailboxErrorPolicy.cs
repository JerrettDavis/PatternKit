namespace PatternKit.Messaging.Mailboxes;

/// <summary>
/// Defines how a mailbox reacts when the handler throws.
/// </summary>
public enum MailboxErrorPolicy
{
    /// <summary>Stops accepting new messages after a handler failure.</summary>
    Stop = 0,

    /// <summary>Continues processing later queued messages after a handler failure.</summary>
    Continue = 1
}
