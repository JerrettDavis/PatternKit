namespace PatternKit.Messaging.Reliability;

/// <summary>
/// Captures failed or undeliverable messages with operational metadata and replay handoff support.
/// </summary>
public sealed class DeadLetterChannel<TPayload>
{
    private readonly string _name;
    private readonly string? _source;
    private readonly IDeadLetterStore<TPayload> _store;
    private readonly DeadLetterIdFactory<TPayload> _idFactory;
    private readonly Func<DateTimeOffset> _clock;
    private readonly bool _includeExceptionDetails;

    private DeadLetterChannel(
        string name,
        string? source,
        IDeadLetterStore<TPayload> store,
        DeadLetterIdFactory<TPayload> idFactory,
        Func<DateTimeOffset> clock,
        bool includeExceptionDetails)
    {
        _name = name;
        _source = source;
        _store = store;
        _idFactory = idFactory;
        _clock = clock;
        _includeExceptionDetails = includeExceptionDetails;
    }

    /// <summary>Creates a dead-letter channel builder.</summary>
    public static Builder Create(string name = "dead-letter-channel")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dead-letter channel name cannot be null, empty, or whitespace.", nameof(name));

        return new Builder(name);
    }

    /// <summary>Captures a failed message using a synchronous store call.</summary>
    public DeadLetterMessage<TPayload> Capture(
        Message<TPayload> message,
        string reason,
        Exception? exception = null,
        int attempts = 0,
        MessageContext? context = null)
        => CaptureAsync(message, reason, exception, attempts, context).AsTask().GetAwaiter().GetResult();

    /// <summary>Captures a failed message with reason, attempts, original headers, and replay metadata.</summary>
    public async ValueTask<DeadLetterMessage<TPayload>> CaptureAsync(
        Message<TPayload> message,
        string reason,
        Exception? exception = null,
        int attempts = 0,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Dead-letter reason cannot be null, empty, or whitespace.", nameof(reason));
        if (attempts < 0)
            throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "Attempt count cannot be negative.");

        context ??= MessageContext.From(message, cancellationToken);
        var id = _idFactory(message, reason, context);
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Dead-letter id factory returned null, empty, or whitespace.");

        var headers = message.Headers
            .With("dead-letter-id", id)
            .With("dead-letter-channel", _name)
            .With("dead-letter-reason", reason)
            .With("dead-letter-attempts", attempts)
            .With("dead-letter-failed-at", _clock());

        if (!string.IsNullOrWhiteSpace(_source))
            headers = headers.With("dead-letter-source", _source);

        var deadLetter = new DeadLetterMessage<TPayload>(
            id,
            message.WithHeaders(headers),
            reason,
            headers.TryGetDateTimeOffset("dead-letter-failed-at", out var failedAt) ? failedAt : _clock(),
            attempts,
            _source,
            _includeExceptionDetails ? exception?.GetType().FullName : null,
            _includeExceptionDetails ? exception?.Message : null);

        await _store.EnqueueAsync(deadLetter, cancellationToken).ConfigureAwait(false);
        return deadLetter;
    }

    /// <summary>Attempts to load a dead-lettered message for replay handoff.</summary>
    public ValueTask<DeadLetterReplayResult<TPayload>> PrepareReplayAsync(
        string deadLetterId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deadLetterId))
            throw new ArgumentException("Dead-letter id cannot be null, empty, or whitespace.", nameof(deadLetterId));

        return PrepareReplayCoreAsync(deadLetterId, cancellationToken);
    }

    private async ValueTask<DeadLetterReplayResult<TPayload>> PrepareReplayCoreAsync(
        string deadLetterId,
        CancellationToken cancellationToken)
    {
        var deadLetter = await _store.TryLoadAsync(deadLetterId, cancellationToken).ConfigureAwait(false);
        if (deadLetter is null)
            return DeadLetterReplayResult<TPayload>.Miss(deadLetterId, "Dead-letter message was not found.");

        var replayMessage = deadLetter.Message.Enrich(headers => headers
            .With("dead-letter-replay-id", Guid.NewGuid().ToString("N"))
            .With("dead-letter-replayed-from", deadLetter.Id)
            .With("dead-letter-replayed-at", _clock()));

        return DeadLetterReplayResult<TPayload>.Ready(deadLetter, replayMessage);
    }

    /// <summary>Dead-letter channel fluent builder.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private string? _source;
        private IDeadLetterStore<TPayload>? _store;
        private DeadLetterIdFactory<TPayload>? _idFactory;
        private Func<DateTimeOffset> _clock = () => DateTimeOffset.UtcNow;
        private bool _includeExceptionDetails = true;

        internal Builder(string name)
        {
            _name = name;
        }

        /// <summary>Sets the pipeline, endpoint, or transport source that produced failures.</summary>
        public Builder FromSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                throw new ArgumentException("Dead-letter source cannot be null, empty, or whitespace.", nameof(source));

            _source = source;
            return this;
        }

        /// <summary>Uses a custom durable dead-letter store.</summary>
        public Builder UseStore(IDeadLetterStore<TPayload> store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            return this;
        }

        /// <summary>Uses a custom dead-letter id factory.</summary>
        public Builder UseIds(DeadLetterIdFactory<TPayload> idFactory)
        {
            _idFactory = idFactory ?? throw new ArgumentNullException(nameof(idFactory));
            return this;
        }

        /// <summary>Uses a deterministic clock for tests or persistence coordination.</summary>
        public Builder UseClock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        /// <summary>Controls whether captured exception type and message are persisted.</summary>
        public Builder IncludeExceptionDetails(bool include = true)
        {
            _includeExceptionDetails = include;
            return this;
        }

        /// <summary>Builds the dead-letter channel.</summary>
        public DeadLetterChannel<TPayload> Build()
            => new(
                _name,
                _source,
                _store ?? new InMemoryDeadLetterStore<TPayload>(),
                _idFactory ?? DefaultIdFactory,
                _clock,
                _includeExceptionDetails);

        private static string DefaultIdFactory(Message<TPayload> message, string reason, MessageContext context)
            => message.Headers.MessageId is { Length: > 0 } messageId
                ? $"dead:{messageId}"
                : $"dead:{Guid.NewGuid():N}";
    }
}

/// <summary>Creates a dead-letter id from the failed message and failure reason.</summary>
public delegate string DeadLetterIdFactory<TPayload>(
    Message<TPayload> message,
    string reason,
    MessageContext context);

/// <summary>Store abstraction for durable dead-letter records.</summary>
public interface IDeadLetterStore<TPayload>
{
    /// <summary>Persists a dead-letter message.</summary>
    ValueTask EnqueueAsync(DeadLetterMessage<TPayload> message, CancellationToken cancellationToken = default);

    /// <summary>Attempts to load a dead-letter message by id.</summary>
    ValueTask<DeadLetterMessage<TPayload>?> TryLoadAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>In-memory dead-letter store suitable for tests, samples, and embedded applications.</summary>
public sealed class InMemoryDeadLetterStore<TPayload> : IDeadLetterStore<TPayload>
{
    private readonly List<DeadLetterMessage<TPayload>> _messages = new();

    /// <summary>Captured dead-letter messages.</summary>
    public IReadOnlyList<DeadLetterMessage<TPayload>> Messages => _messages;

    /// <inheritdoc />
    public ValueTask EnqueueAsync(DeadLetterMessage<TPayload> message, CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        cancellationToken.ThrowIfCancellationRequested();
        _messages.Add(message);
        return default;
    }

    /// <inheritdoc />
    public ValueTask<DeadLetterMessage<TPayload>?> TryLoadAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Dead-letter id cannot be null, empty, or whitespace.", nameof(id));

        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<DeadLetterMessage<TPayload>?>(_messages.LastOrDefault(message =>
            string.Equals(message.Id, id, StringComparison.Ordinal)));
    }
}

/// <summary>Captured failed message with operational failure metadata.</summary>
public sealed class DeadLetterMessage<TPayload>
{
    /// <summary>Creates a dead-letter message.</summary>
    public DeadLetterMessage(
        string id,
        Message<TPayload> message,
        string reason,
        DateTimeOffset failedAt,
        int attempts,
        string? source = null,
        string? exceptionType = null,
        string? exceptionMessage = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Dead-letter id cannot be null, empty, or whitespace.", nameof(id));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Dead-letter reason cannot be null, empty, or whitespace.", nameof(reason));
        if (attempts < 0)
            throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "Attempt count cannot be negative.");

        Id = id;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Reason = reason;
        FailedAt = failedAt;
        Attempts = attempts;
        Source = source;
        ExceptionType = exceptionType;
        ExceptionMessage = exceptionMessage;
    }

    /// <summary>Dead-letter record identifier.</summary>
    public string Id { get; }

    /// <summary>The original message with dead-letter metadata headers attached.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>Operational failure reason.</summary>
    public string Reason { get; }

    /// <summary>When the message failed.</summary>
    public DateTimeOffset FailedAt { get; }

    /// <summary>Number of attempts before dead-lettering.</summary>
    public int Attempts { get; }

    /// <summary>Pipeline, endpoint, or transport source that captured the failure.</summary>
    public string? Source { get; }

    /// <summary>Captured exception type when exception details are enabled.</summary>
    public string? ExceptionType { get; }

    /// <summary>Captured exception message when exception details are enabled.</summary>
    public string? ExceptionMessage { get; }
}

/// <summary>Replay handoff result for a dead-lettered message.</summary>
public sealed class DeadLetterReplayResult<TPayload>
{
    private DeadLetterReplayResult(
        DeadLetterMessage<TPayload>? deadLetter,
        Message<TPayload>? message,
        bool found,
        string? missingReason)
    {
        DeadLetter = deadLetter;
        Message = message;
        Found = found;
        MissingReason = missingReason;
    }

    /// <summary>The loaded dead-letter record when found.</summary>
    public DeadLetterMessage<TPayload>? DeadLetter { get; }

    /// <summary>The message prepared for replay when found.</summary>
    public Message<TPayload>? Message { get; }

    /// <summary>Gets whether the dead-letter record was found.</summary>
    public bool Found { get; }

    /// <summary>Gets whether the message is ready for replay.</summary>
    public bool ReadyForReplay => Found && Message is not null;

    /// <summary>Reason the dead-letter record could not be loaded.</summary>
    public string? MissingReason { get; }

    /// <summary>Creates a successful replay result.</summary>
    public static DeadLetterReplayResult<TPayload> Ready(
        DeadLetterMessage<TPayload> deadLetter,
        Message<TPayload> message)
        => new(deadLetter ?? throw new ArgumentNullException(nameof(deadLetter)),
            message ?? throw new ArgumentNullException(nameof(message)),
            true,
            null);

    /// <summary>Creates a missing replay result.</summary>
    public static DeadLetterReplayResult<TPayload> Miss(string id, string reason)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Dead-letter id cannot be null, empty, or whitespace.", nameof(id));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Missing reason cannot be null, empty, or whitespace.", nameof(reason));

        return new(null, null, false, reason);
    }
}
