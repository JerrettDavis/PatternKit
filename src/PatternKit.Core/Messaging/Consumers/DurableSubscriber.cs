using PatternKit.Messaging.Storage;

namespace PatternKit.Messaging.Consumers;

/// <summary>
/// Replays stored messages for a named subscriber and records a durable checkpoint after successful handling.
/// </summary>
public sealed class DurableSubscriber<TPayload>
{
    public delegate DurableSubscriberHandlerResult Handler(StoredMessage<TPayload> message, MessageContext context);

    private readonly MessageStore<TPayload> _store;
    private readonly IDurableSubscriberCheckpointStore _checkpoints;
    private readonly IReadOnlyList<HandlerRegistration> _handlers;
    private readonly DurableSubscriberErrorPolicy _errorPolicy;

    private DurableSubscriber(
        string name,
        MessageStore<TPayload> store,
        IDurableSubscriberCheckpointStore checkpoints,
        IReadOnlyList<HandlerRegistration> handlers,
        DurableSubscriberErrorPolicy errorPolicy)
        => (Name, _store, _checkpoints, _handlers, _errorPolicy) = (name, store, checkpoints, handlers, errorPolicy);

    public string Name { get; }

    public DurableSubscriberResult<TPayload> CatchUp(MessageStoreQuery? query = null)
    {
        var checkpoint = _checkpoints.Load(Name);
        var delivered = 0;
        var skipped = 0;
        var lastSequence = checkpoint.LastSequence;
        var failures = new List<DurableSubscriberHandlerResult>();

        foreach (var stored in _store.Query(query))
        {
            if (stored.Sequence <= checkpoint.LastSequence)
            {
                skipped++;
                continue;
            }

            var context = MessageContext.From(stored.Message);
            var messageFailed = false;
            foreach (var registration in _handlers)
            {
                DurableSubscriberHandlerResult result;
                try
                {
                    result = registration.Handler(stored, context);
                }
                catch (Exception ex)
                {
                    result = DurableSubscriberHandlerResult.Failure(registration.Name, ex.Message, ex);
                }

                if (!result.Succeeded)
                {
                    failures.Add(result);
                    messageFailed = true;
                    if (_errorPolicy == DurableSubscriberErrorPolicy.StopOnFirstFailure)
                        break;
                }
            }

            if (messageFailed)
                break;

            if (!messageFailed)
            {
                delivered++;
                lastSequence = stored.Sequence;
                _checkpoints.Save(new DurableSubscriberCheckpoint(Name, stored.Sequence, stored.MessageId, DateTimeOffset.UtcNow));
            }
        }

        return new DurableSubscriberResult<TPayload>(Name, delivered, skipped, lastSequence, failures.AsReadOnly());
    }

    public static Builder Create(string name = "durable-subscriber") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<HandlerRegistration> _handlers = new();
        private MessageStore<TPayload>? _store;
        private IDurableSubscriberCheckpointStore? _checkpoints;
        private DurableSubscriberErrorPolicy _errorPolicy = DurableSubscriberErrorPolicy.StopOnFirstFailure;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Durable subscriber name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder From(MessageStore<TPayload> store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            return this;
        }

        public Builder TrackWith(IDurableSubscriberCheckpointStore checkpoints)
        {
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            return this;
        }

        public Builder Handle(string handlerName, Handler handler)
        {
            if (string.IsNullOrWhiteSpace(handlerName))
                throw new ArgumentException("Handler name cannot be null, empty, or whitespace.", nameof(handlerName));

            _handlers.Add(new HandlerRegistration(handlerName, handler ?? throw new ArgumentNullException(nameof(handler))));
            return this;
        }

        public Builder Handle(string handlerName, Action<StoredMessage<TPayload>, MessageContext> handler)
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            return Handle(handlerName, (stored, context) =>
            {
                handler(stored, context);
                return DurableSubscriberHandlerResult.Success(handlerName);
            });
        }

        public Builder OnError(DurableSubscriberErrorPolicy policy)
        {
            _errorPolicy = policy;
            return this;
        }

        public DurableSubscriber<TPayload> Build()
        {
            if (_store is null)
                throw new InvalidOperationException("Durable subscriber requires a message store.");
            if (_checkpoints is null)
                throw new InvalidOperationException("Durable subscriber requires a checkpoint store.");
            if (_handlers.Count == 0)
                throw new InvalidOperationException("Durable subscriber requires at least one handler.");

            return new(_name, _store, _checkpoints, _handlers.ToArray(), _errorPolicy);
        }
    }

    private sealed class HandlerRegistration
    {
        public HandlerRegistration(string name, Handler handler)
            => (Name, Handler) = (name, handler);

        public string Name { get; }

        public Handler Handler { get; }
    }
}

public enum DurableSubscriberErrorPolicy
{
    StopOnFirstFailure,
    Continue
}

public sealed class DurableSubscriberHandlerResult
{
    private DurableSubscriberHandlerResult(string handlerName, bool succeeded, string? reason, Exception? exception)
        => (HandlerName, Succeeded, Reason, Exception) = (handlerName, succeeded, reason, exception);

    public string HandlerName { get; }

    public bool Succeeded { get; }

    public string? Reason { get; }

    public Exception? Exception { get; }

    public static DurableSubscriberHandlerResult Success(string handlerName)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
            throw new ArgumentException("Handler name cannot be null, empty, or whitespace.", nameof(handlerName));

        return new(handlerName, true, null, null);
    }

    public static DurableSubscriberHandlerResult Failure(string handlerName, string reason, Exception? exception = null)
    {
        if (string.IsNullOrWhiteSpace(handlerName))
            throw new ArgumentException("Handler name cannot be null, empty, or whitespace.", nameof(handlerName));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason cannot be null, empty, or whitespace.", nameof(reason));

        return new(handlerName, false, reason, exception);
    }
}

public sealed class DurableSubscriberResult<TPayload>
{
    internal DurableSubscriberResult(
        string subscriberName,
        int deliveredCount,
        int skippedCount,
        long lastSequence,
        IReadOnlyList<DurableSubscriberHandlerResult> failures)
        => (SubscriberName, DeliveredCount, SkippedCount, LastSequence, Failures) =
            (subscriberName, deliveredCount, skippedCount, lastSequence, failures);

    public string SubscriberName { get; }

    public int DeliveredCount { get; }

    public int SkippedCount { get; }

    public long LastSequence { get; }

    public IReadOnlyList<DurableSubscriberHandlerResult> Failures { get; }

    public bool Completed => Failures.Count == 0;
}

public interface IDurableSubscriberCheckpointStore
{
    DurableSubscriberCheckpoint Load(string subscriberName);

    void Save(DurableSubscriberCheckpoint checkpoint);
}

public sealed class DurableSubscriberCheckpoint
{
    public DurableSubscriberCheckpoint(string subscriberName, long lastSequence, string? lastMessageId, DateTimeOffset updatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(subscriberName))
            throw new ArgumentException("Subscriber name cannot be null, empty, or whitespace.", nameof(subscriberName));
        if (lastSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(lastSequence), "Last sequence cannot be negative.");

        SubscriberName = subscriberName;
        LastSequence = lastSequence;
        LastMessageId = lastMessageId;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string SubscriberName { get; }

    public long LastSequence { get; }

    public string? LastMessageId { get; }

    public DateTimeOffset UpdatedAtUtc { get; }
}

public sealed class InMemoryDurableSubscriberCheckpointStore : IDurableSubscriberCheckpointStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, DurableSubscriberCheckpoint> _checkpoints = new(StringComparer.Ordinal);

    public DurableSubscriberCheckpoint Load(string subscriberName)
    {
        if (string.IsNullOrWhiteSpace(subscriberName))
            throw new ArgumentException("Subscriber name cannot be null, empty, or whitespace.", nameof(subscriberName));

        lock (_gate)
            return _checkpoints.TryGetValue(subscriberName, out var checkpoint)
                ? checkpoint
                : new DurableSubscriberCheckpoint(subscriberName, 0, null, DateTimeOffset.MinValue);
    }

    public void Save(DurableSubscriberCheckpoint checkpoint)
    {
        if (checkpoint is null)
            throw new ArgumentNullException(nameof(checkpoint));

        lock (_gate)
            _checkpoints[checkpoint.SubscriberName] = checkpoint;
    }
}
