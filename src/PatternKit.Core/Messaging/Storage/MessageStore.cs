namespace PatternKit.Messaging.Storage;

/// <summary>
/// In-memory message store for audit, replay, and operational lookup workflows.
/// </summary>
public sealed class MessageStore<TPayload>
{
    /// <summary>Selects the persisted message identifier.</summary>
    public delegate string MessageIdentitySelector(Message<TPayload> message, MessageContext context);

    /// <summary>Determines whether a stored message should be retained.</summary>
    public delegate bool RetentionPredicate(StoredMessage<TPayload> stored);

    private readonly object _gate = new();
    private readonly string _name;
    private readonly MessageIdentitySelector _identitySelector;
    private readonly RetentionPredicate _retentionPredicate;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Dictionary<string, StoredMessage<TPayload>> _byId = new(StringComparer.Ordinal);
    private readonly List<StoredMessage<TPayload>> _ordered = new();
    private long _sequence;

    private MessageStore(
        string name,
        MessageIdentitySelector identitySelector,
        RetentionPredicate retentionPredicate,
        Func<DateTimeOffset> clock)
        => (_name, _identitySelector, _retentionPredicate, _clock) = (name, identitySelector, retentionPredicate, clock);

    /// <summary>Appends a message when it is not already present.</summary>
    public MessageStoreAppendResult<TPayload> Append(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var messageId = _identitySelector(message, effectiveContext);
        if (string.IsNullOrWhiteSpace(messageId))
            throw new InvalidOperationException("Message store identity selector returned a blank identifier.");

        lock (_gate)
        {
            if (_byId.TryGetValue(messageId, out var existing))
                return MessageStoreAppendResult<TPayload>.ForDuplicate(_name, existing);

            var stored = new StoredMessage<TPayload>(_name, messageId, ++_sequence, _clock(), message, effectiveContext.Headers);
            if (!_retentionPredicate(stored))
                return MessageStoreAppendResult<TPayload>.ForRejected(_name, stored, "Message did not satisfy retention policy.");

            _byId.Add(messageId, stored);
            _ordered.Add(stored);
            return MessageStoreAppendResult<TPayload>.ForStored(_name, stored);
        }
    }

    /// <summary>Returns a stored message by identifier when present.</summary>
    public StoredMessage<TPayload>? Get(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message identifier cannot be null, empty, or whitespace.", nameof(messageId));

        lock (_gate)
            return _byId.TryGetValue(messageId, out var stored) ? stored : null;
    }

    /// <summary>Queries stored messages in append order.</summary>
    public IReadOnlyList<StoredMessage<TPayload>> Query(MessageStoreQuery? query = null)
    {
        var effectiveQuery = query ?? MessageStoreQuery.All;
        lock (_gate)
        {
            IEnumerable<StoredMessage<TPayload>> matches = _ordered;
            if (!string.IsNullOrWhiteSpace(effectiveQuery.CorrelationId))
                matches = matches.Where(stored => stored.Headers.CorrelationId == effectiveQuery.CorrelationId);
            if (effectiveQuery.FromUtc is not null)
                matches = matches.Where(stored => stored.StoredAtUtc >= effectiveQuery.FromUtc.Value);
            if (effectiveQuery.ToUtc is not null)
                matches = matches.Where(stored => stored.StoredAtUtc <= effectiveQuery.ToUtc.Value);
            if (effectiveQuery.MaxCount is not null)
                matches = matches.Take(effectiveQuery.MaxCount.Value);

            return matches.ToArray();
        }
    }

    /// <summary>Returns message envelopes that match a query in replay order.</summary>
    public IReadOnlyList<Message<TPayload>> Replay(MessageStoreQuery? query = null)
        => Query(query).Select(static stored => stored.Message).ToArray();

    /// <summary>Creates a message-store builder.</summary>
    public static Builder Create(string name = "message-store") => new(name);

    /// <summary>Fluent builder for <see cref="MessageStore{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private MessageIdentitySelector _identitySelector = static (message, _) =>
            message.Headers.MessageId ?? Guid.NewGuid().ToString("N");
        private RetentionPredicate _retentionPredicate = static _ => true;
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Message store name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Configures the identity selector used for storage and lookup.</summary>
        public Builder IdentifyBy(MessageIdentitySelector selector)
        {
            _identitySelector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        /// <summary>Configures a retention predicate. Messages returning false are not persisted.</summary>
        public Builder RetainWhen(RetentionPredicate predicate)
        {
            _retentionPredicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        /// <summary>Configures the clock used when storing messages.</summary>
        public Builder UseClock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        /// <summary>Builds the message store.</summary>
        public MessageStore<TPayload> Build() => new(_name, _identitySelector, _retentionPredicate, _clock);
    }
}

/// <summary>Stored message metadata and envelope.</summary>
public sealed class StoredMessage<TPayload>
{
    /// <summary>Creates stored message metadata.</summary>
    public StoredMessage(
        string storeName,
        string messageId,
        long sequence,
        DateTimeOffset storedAtUtc,
        Message<TPayload> message,
        MessageHeaders headers)
    {
        StoreName = storeName;
        MessageId = messageId;
        Sequence = sequence;
        StoredAtUtc = storedAtUtc;
        Message = message;
        Headers = headers;
    }

    /// <summary>Name of the message store.</summary>
    public string StoreName { get; }

    /// <summary>Stable message identifier.</summary>
    public string MessageId { get; }

    /// <summary>Append sequence assigned by the store.</summary>
    public long Sequence { get; }

    /// <summary>UTC time the message was stored.</summary>
    public DateTimeOffset StoredAtUtc { get; }

    /// <summary>Stored message envelope.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>Headers captured for query and replay metadata.</summary>
    public MessageHeaders Headers { get; }
}

/// <summary>Query options for store lookup and replay.</summary>
public sealed class MessageStoreQuery
{
    /// <summary>Query that returns every stored message.</summary>
    public static MessageStoreQuery All { get; } = new();

    /// <summary>Creates a message-store query.</summary>
    public MessageStoreQuery(string? correlationId = null, DateTimeOffset? fromUtc = null, DateTimeOffset? toUtc = null, int? maxCount = null)
    {
        if (maxCount is not null && maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Maximum count must be greater than zero.");

        CorrelationId = correlationId;
        FromUtc = fromUtc;
        ToUtc = toUtc;
        MaxCount = maxCount;
    }

    /// <summary>Correlation identifier filter.</summary>
    public string? CorrelationId { get; }

    /// <summary>Inclusive lower stored-at UTC filter.</summary>
    public DateTimeOffset? FromUtc { get; }

    /// <summary>Inclusive upper stored-at UTC filter.</summary>
    public DateTimeOffset? ToUtc { get; }

    /// <summary>Maximum number of messages to return.</summary>
    public int? MaxCount { get; }

    /// <summary>Creates a query scoped to a correlation identifier.</summary>
    public static MessageStoreQuery ForCorrelation(string correlationId)
    {
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("Correlation identifier cannot be null, empty, or whitespace.", nameof(correlationId));

        return new MessageStoreQuery(correlationId);
    }

    /// <summary>Creates a copy with a maximum result count.</summary>
    public MessageStoreQuery Take(int maxCount)
    {
        if (maxCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCount), "Maximum count must be greater than zero.");

        return new MessageStoreQuery(CorrelationId, FromUtc, ToUtc, maxCount);
    }
}

/// <summary>Result returned when appending to a message store.</summary>
public sealed class MessageStoreAppendResult<TPayload>
{
    private MessageStoreAppendResult(string storeName, StoredMessage<TPayload> storedMessage, bool stored, bool duplicate, string? rejectionReason)
        => (StoreName, StoredMessage, Stored, Duplicate, RejectionReason) = (storeName, storedMessage, stored, duplicate, rejectionReason);

    /// <summary>Name of the message store.</summary>
    public string StoreName { get; }

    /// <summary>Message metadata involved in the append attempt.</summary>
    public StoredMessage<TPayload> StoredMessage { get; }

    /// <summary>True when the message was persisted by this call.</summary>
    public bool Stored { get; }

    /// <summary>True when the message already existed.</summary>
    public bool Duplicate { get; }

    /// <summary>Reason the message was rejected, when applicable.</summary>
    public string? RejectionReason { get; }

    /// <summary>Creates a successful stored result.</summary>
    public static MessageStoreAppendResult<TPayload> ForStored(string storeName, StoredMessage<TPayload> storedMessage)
        => new(storeName, storedMessage, true, false, null);

    /// <summary>Creates a duplicate result.</summary>
    public static MessageStoreAppendResult<TPayload> ForDuplicate(string storeName, StoredMessage<TPayload> storedMessage)
        => new(storeName, storedMessage, false, true, null);

    /// <summary>Creates a retention rejection result.</summary>
    public static MessageStoreAppendResult<TPayload> ForRejected(string storeName, StoredMessage<TPayload> storedMessage, string rejectionReason)
        => new(storeName, storedMessage, false, false, rejectionReason);
}
