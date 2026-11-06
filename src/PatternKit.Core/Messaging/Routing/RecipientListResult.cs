using System.Collections.ObjectModel;

namespace PatternKit.Messaging.Routing;

/// <summary>
/// Result returned after dispatching a message through a recipient list.
/// </summary>
public sealed class RecipientListResult
{
    /// <summary>
    /// Creates a recipient list result.
    /// </summary>
    public RecipientListResult(IEnumerable<string> deliveredRecipients)
    {
        if (deliveredRecipients is null)
            throw new ArgumentNullException(nameof(deliveredRecipients));

        DeliveredRecipients = new ReadOnlyCollection<string>(deliveredRecipients.ToArray());
    }

    /// <summary>Recipient names that received the message, in dispatch order.</summary>
    public IReadOnlyList<string> DeliveredRecipients { get; }

    /// <summary>Number of recipients that received the message.</summary>
    public int Count => DeliveredRecipients.Count;
}
