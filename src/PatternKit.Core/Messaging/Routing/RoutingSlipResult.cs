namespace PatternKit.Messaging.Routing;

/// <summary>
/// Result returned after executing a routing slip.
/// </summary>
public sealed class RoutingSlipResult<TPayload>
{
    /// <summary>Creates a routing slip execution result.</summary>
    public RoutingSlipResult(Message<TPayload> message, IEnumerable<string> completedSteps)
    {
        Message = message ?? throw new ArgumentNullException(nameof(message));
        CompletedSteps = completedSteps?.ToArray() ?? throw new ArgumentNullException(nameof(completedSteps));
    }

    /// <summary>The final message after all completed steps.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>Step names completed during execution.</summary>
    public IReadOnlyList<string> CompletedSteps { get; }

    /// <summary>The number of completed steps.</summary>
    public int Count => CompletedSteps.Count;
}
