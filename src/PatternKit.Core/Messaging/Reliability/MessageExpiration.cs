namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Stamps and evaluates message expiration metadata so stale messages can be rejected before processing.
/// </summary>
public sealed class MessageExpiration<TPayload>
{
    private readonly string _name;
    private readonly string _headerName;
    private readonly TimeSpan? _defaultTtl;
    private readonly Func<DateTimeOffset> _clock;
    private readonly bool _preserveExisting;
    private readonly string _expiredReason;

    private MessageExpiration(
        string name,
        string headerName,
        TimeSpan? defaultTtl,
        Func<DateTimeOffset> clock,
        bool preserveExisting,
        string expiredReason)
        => (_name, _headerName, _defaultTtl, _clock, _preserveExisting, _expiredReason) =
            (name, headerName, defaultTtl, clock, preserveExisting, expiredReason);

    /// <summary>The header used to store the expiration deadline.</summary>
    public string HeaderName => _headerName;

    /// <summary>Reads the expiration deadline from a message when present and parseable.</summary>
    public DateTimeOffset? Read(Message<TPayload> message)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        return message.Headers.TryGetDateTimeOffset(_headerName, out var expiresAt) ? expiresAt : null;
    }

    /// <summary>Returns a new message with an expiration deadline based on the configured or supplied TTL.</summary>
    public Message<TPayload> Stamp(Message<TPayload> message, TimeSpan? ttl = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        if (_preserveExisting && Read(message) is not null)
            return message;

        var effectiveTtl = ttl ?? _defaultTtl;
        if (effectiveTtl is null || effectiveTtl.Value <= TimeSpan.Zero)
            throw new InvalidOperationException("Message expiration requires a positive TTL.");

        return WithDeadline(message, _clock().Add(effectiveTtl.Value));
    }

    /// <summary>Returns a new message with an explicit expiration deadline.</summary>
    public Message<TPayload> WithDeadline(Message<TPayload> message, DateTimeOffset expiresAt)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        return message.WithHeader(_headerName, expiresAt);
    }

    /// <summary>Evaluates whether a message is expired at the current clock time.</summary>
    public MessageExpirationResult<TPayload> Evaluate(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var checkedAt = _clock();
        var expiresAt = Read(message);
        if (expiresAt is null && context is not null && context.Headers.TryGetDateTimeOffset(_headerName, out var contextDeadline))
            expiresAt = contextDeadline;

        if (expiresAt is null)
            return MessageExpirationResult<TPayload>.Accepted(message, _name, checkedAt, null);

        return expiresAt.Value <= checkedAt
            ? MessageExpirationResult<TPayload>.Reject(message, _name, checkedAt, expiresAt.Value, _expiredReason)
            : MessageExpirationResult<TPayload>.Accepted(message, _name, checkedAt, expiresAt.Value);
    }

    /// <summary>Creates a new message-expiration builder.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for <see cref="MessageExpiration{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private string _name = "message-expiration";
        private string _headerName = "expires-at";
        private TimeSpan? _defaultTtl;
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;
        private bool _preserveExisting = true;
        private string _expiredReason = "Message expired before processing.";

        /// <summary>Assigns a policy name used in evaluation results.</summary>
        public Builder Name(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Message expiration name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
            return this;
        }

        /// <summary>Assigns the header used to store expiration deadlines.</summary>
        public Builder Header(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
                throw new ArgumentException("Message expiration header cannot be null, empty, or whitespace.", nameof(headerName));

            _headerName = headerName;
            return this;
        }

        /// <summary>Configures the default time-to-live used by <see cref="Stamp"/>.</summary>
        public Builder DefaultTtl(TimeSpan ttl)
        {
            if (ttl <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(ttl), "Message expiration TTL must be positive.");

            _defaultTtl = ttl;
            return this;
        }

        /// <summary>Configures the clock used for stamping and evaluation.</summary>
        public Builder Clock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        /// <summary>Configures whether stamping keeps an existing expiration deadline.</summary>
        public Builder PreserveExisting(bool preserveExisting = true)
        {
            _preserveExisting = preserveExisting;
            return this;
        }

        /// <summary>Configures the rejection reason returned for expired messages.</summary>
        public Builder ExpiredReason(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Message expiration reason cannot be null, empty, or whitespace.", nameof(reason));

            _expiredReason = reason;
            return this;
        }

        /// <summary>Builds an immutable message-expiration policy.</summary>
        public MessageExpiration<TPayload> Build()
            => new(_name, _headerName, _defaultTtl, _clock, _preserveExisting, _expiredReason);
    }
}

/// <summary>
/// Result returned by a message-expiration evaluation.
/// </summary>
public sealed class MessageExpirationResult<TPayload>
{
    private MessageExpirationResult(
        Message<TPayload> message,
        string policyName,
        DateTimeOffset checkedAt,
        DateTimeOffset? expiresAt,
        bool expired,
        string? reason)
        => (Message, PolicyName, CheckedAt, ExpiresAt, Expired, Reason) =
            (message, policyName, checkedAt, expiresAt, expired, reason);

    /// <summary>The message evaluated by the policy.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>The policy name that evaluated the message.</summary>
    public string PolicyName { get; }

    /// <summary>The time used to evaluate expiration.</summary>
    public DateTimeOffset CheckedAt { get; }

    /// <summary>The expiration deadline, or null when no expiration metadata was present.</summary>
    public DateTimeOffset? ExpiresAt { get; }

    /// <summary>True when the message has expired.</summary>
    public bool Expired { get; }

    /// <summary>Human-readable rejection reason when the message is expired.</summary>
    public string? Reason { get; }

    /// <summary>Creates an accepted result.</summary>
    public static MessageExpirationResult<TPayload> Accepted(
        Message<TPayload> message,
        string policyName,
        DateTimeOffset checkedAt,
        DateTimeOffset? expiresAt)
        => new(message, policyName, checkedAt, expiresAt, false, null);

    /// <summary>Creates an expired result.</summary>
    public static MessageExpirationResult<TPayload> Reject(
        Message<TPayload> message,
        string policyName,
        DateTimeOffset checkedAt,
        DateTimeOffset expiresAt,
        string reason)
        => new(message, policyName, checkedAt, expiresAt, true, reason);
}
