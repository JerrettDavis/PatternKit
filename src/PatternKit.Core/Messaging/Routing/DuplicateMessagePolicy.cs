namespace PatternKit.Messaging.Routing;

/// <summary>
/// Duplicate handling for aggregator groups when messages share the same message id.
/// </summary>
public enum DuplicateMessagePolicy
{
    /// <summary>Keep the first message and ignore later duplicates.</summary>
    Ignore,

    /// <summary>Replace the existing message with the later duplicate.</summary>
    Replace,

    /// <summary>Include duplicate messages in the group.</summary>
    Include
}
