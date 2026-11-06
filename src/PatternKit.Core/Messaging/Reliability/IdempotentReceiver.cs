namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Decorates a message handler with idempotency-key claim, completion, and duplicate handling.
/// </summary>
/// <typeparam name="TPayload">The message payload type.</typeparam>
/// <typeparam name="TResult">The handler result type.</typeparam>
public sealed class IdempotentReceiver<TPayload, TResult>
{
    /// <summary>Async handler protected by the idempotent receiver.</summary>
    public delegate ValueTask<TResult> MessageHandler(
        Message<TPayload> message,
        MessageContext context,
        CancellationToken cancellationToken);

    /// <summary>Extracts an idempotency key from a message and context.</summary>
    public delegate string? KeySelector(Message<TPayload> message, MessageContext context);

    private readonly IIdempotencyStore _store;
    private readonly MessageHandler _handler;
    private readonly KeySelector _keySelector;
    private readonly DuplicateMessagePolicy _duplicatePolicy;
    private readonly MissingIdempotencyKeyPolicy _missingKeyPolicy;

    private IdempotentReceiver(
        IIdempotencyStore store,
        MessageHandler handler,
        KeySelector keySelector,
        DuplicateMessagePolicy duplicatePolicy,
        MissingIdempotencyKeyPolicy missingKeyPolicy)
    {
        _store = store;
        _handler = handler;
        _keySelector = keySelector;
        _duplicatePolicy = duplicatePolicy;
        _missingKeyPolicy = missingKeyPolicy;
    }

    /// <summary>Creates an idempotent receiver builder.</summary>
    public static Builder Create(IIdempotencyStore store, MessageHandler handler) => new(store, handler);

    /// <summary>Handles a message through the idempotent receiver.</summary>
    public async ValueTask<IdempotentReceiverResult<TResult>> HandleAsync(
        Message<TPayload> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = CreateContext(message, context, cancellationToken);
        var key = _keySelector(message, effectiveContext);
        if (string.IsNullOrWhiteSpace(key))
            return await HandleMissingKeyAsync(message, effectiveContext, cancellationToken).ConfigureAwait(false);

        var claim = await _store.TryClaimAsync(key!, cancellationToken).ConfigureAwait(false);
        if (!claim.Claimed)
            return HandleDuplicate(claim);

        try
        {
            var result = await _handler(message, effectiveContext, effectiveContext.CancellationToken).ConfigureAwait(false);
            await _store.MarkCompletedAsync(key!, result, cancellationToken).ConfigureAwait(false);
            return IdempotentReceiverResult<TResult>.ProcessedResult(key, result);
        }
        catch (Exception exception)
        {
            await _store.MarkFailedAsync(key!, exception.Message, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<IdempotentReceiverResult<TResult>> HandleMissingKeyAsync(
        Message<TPayload> message,
        MessageContext context,
        CancellationToken cancellationToken)
    {
        if (_missingKeyPolicy == MissingIdempotencyKeyPolicy.Reject)
            return IdempotentReceiverResult<TResult>.MissingKey();

        var result = await _handler(message, context, context.CancellationToken).ConfigureAwait(false);
        return IdempotentReceiverResult<TResult>.ProcessedResult(null, result);
    }

    private IdempotentReceiverResult<TResult> HandleDuplicate(IdempotencyClaim claim)
    {
        if (_duplicatePolicy == DuplicateMessagePolicy.ReplayCompleted
            && claim.Status == IdempotencyEntryStatus.Completed
            && claim.Result is TResult result)
        {
            return IdempotentReceiverResult<TResult>.Replayed(claim.Key, result);
        }

        return IdempotentReceiverResult<TResult>.Duplicate(claim.Key);
    }

    private static MessageContext CreateContext(
        Message<TPayload> message,
        MessageContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null)
            return MessageContext.From(message, cancellationToken);

        return cancellationToken.CanBeCanceled
            ? context.WithCancellation(cancellationToken)
            : context;
    }

    /// <summary>Fluent builder for <see cref="IdempotentReceiver{TPayload,TResult}"/>.</summary>
    public sealed class Builder
    {
        private readonly IIdempotencyStore _store;
        private readonly MessageHandler _handler;
        private KeySelector _keySelector = static (message, _) => message.Headers.IdempotencyKey;
        private DuplicateMessagePolicy _duplicatePolicy = DuplicateMessagePolicy.Suppress;
        private MissingIdempotencyKeyPolicy _missingKeyPolicy = MissingIdempotencyKeyPolicy.Reject;

        internal Builder(IIdempotencyStore store, MessageHandler handler)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>Configures a custom idempotency key selector.</summary>
        public Builder KeyBy(KeySelector keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            return this;
        }

        /// <summary>Configures duplicate handling.</summary>
        public Builder OnDuplicate(DuplicateMessagePolicy policy)
        {
            _duplicatePolicy = policy;
            return this;
        }

        /// <summary>Configures missing idempotency key handling.</summary>
        public Builder OnMissingKey(MissingIdempotencyKeyPolicy policy)
        {
            _missingKeyPolicy = policy;
            return this;
        }

        /// <summary>Builds an immutable idempotent receiver.</summary>
        public IdempotentReceiver<TPayload, TResult> Build() => new(
            _store,
            _handler,
            _keySelector,
            _duplicatePolicy,
            _missingKeyPolicy);
    }
}
