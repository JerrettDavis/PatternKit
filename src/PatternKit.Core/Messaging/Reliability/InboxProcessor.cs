namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Inbox processor that applies an idempotent receiver around a message handler.
/// </summary>
public sealed class InboxProcessor<TPayload, TResult>
{
    private readonly IdempotentReceiver<TPayload, TResult> _receiver;

    private InboxProcessor(IdempotentReceiver<TPayload, TResult> receiver)
    {
        _receiver = receiver;
    }

    /// <summary>Creates an inbox processor from an idempotent receiver.</summary>
    public static InboxProcessor<TPayload, TResult> Create(IdempotentReceiver<TPayload, TResult> receiver)
    {
        if (receiver is null)
            throw new ArgumentNullException(nameof(receiver));

        return new InboxProcessor<TPayload, TResult>(receiver);
    }

    /// <summary>Processes an inbox message through the configured idempotent receiver.</summary>
    public ValueTask<IdempotentReceiverResult<TResult>> ProcessAsync(
        Message<TPayload> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
        => _receiver.HandleAsync(message, context, cancellationToken);
}
