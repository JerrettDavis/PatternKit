namespace PatternKit.EnterpriseIntegration.EventNotification;

public sealed class EventNotificationResult<TKey>
{
    private EventNotificationResult(
        string notificationName,
        TKey? key,
        string correlationId,
        IReadOnlyDictionary<string, string> metadata,
        Exception? exception,
        bool published,
        bool skipped)
        => (NotificationName, Key, CorrelationId, Metadata, Exception, Published, Skipped) = (notificationName, key, correlationId, metadata, exception, published, skipped);

    public string NotificationName { get; }

    public TKey? Key { get; }

    public string CorrelationId { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public Exception? Exception { get; }

    public bool Published { get; }

    public bool Skipped { get; }

    public bool Failed => !Published && !Skipped;

    public static EventNotificationResult<TKey> Publish(string notificationName, TKey key, string correlationId, IReadOnlyDictionary<string, string> metadata)
        => new(notificationName, key, correlationId, metadata, null, true, false);

    public static EventNotificationResult<TKey> Skip(string notificationName)
        => new(notificationName, default, string.Empty, new Dictionary<string, string>(), null, false, true);

    public static EventNotificationResult<TKey> Failure(string notificationName, Exception exception)
        => new(notificationName, default, string.Empty, new Dictionary<string, string>(), exception ?? throw new ArgumentNullException(nameof(exception)), false, false);
}

public sealed class EventNotification<TEvent, TKey>
{
    private readonly Func<TEvent, bool> _predicate;
    private readonly Func<TEvent, TKey> _keySelector;
    private readonly Func<TEvent, string>? _correlationSelector;
    private readonly IReadOnlyList<MetadataSelector> _metadataSelectors;

    private EventNotification(
        string name,
        Func<TEvent, bool>? predicate,
        Func<TEvent, TKey>? keySelector,
        Func<TEvent, string>? correlationSelector,
        IReadOnlyList<MetadataSelector> metadataSelectors)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Event notification name is required.", nameof(name));

        Name = name;
        _predicate = predicate ?? (_ => true);
        _keySelector = keySelector ?? throw new InvalidOperationException("Event notification requires a key selector.");
        _correlationSelector = correlationSelector;
        _metadataSelectors = metadataSelectors ?? throw new ArgumentNullException(nameof(metadataSelectors));
    }

    public string Name { get; }

    public EventNotificationResult<TKey> Notify(TEvent @event)
    {
        if (@event is null)
            throw new ArgumentNullException(nameof(@event));

        try
        {
            if (!_predicate(@event))
                return EventNotificationResult<TKey>.Skip(Name);

            var key = _keySelector(@event);
            if (key is null)
                return EventNotificationResult<TKey>.Failure(Name, new InvalidOperationException("Event notification key selector returned null."));

            var correlationId = _correlationSelector?.Invoke(@event) ?? string.Empty;
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var selector in _metadataSelectors)
            {
                var value = selector.Select(@event);
                if (!string.IsNullOrWhiteSpace(value))
                    metadata[selector.Name] = value!;
            }

            return EventNotificationResult<TKey>.Publish(Name, key, correlationId, metadata);
        }
        catch (Exception ex)
        {
            return EventNotificationResult<TKey>.Failure(Name, ex);
        }
    }

    public static Builder Create(string name = "event-notification") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<MetadataSelector> _metadataSelectors = [];
        private Func<TEvent, bool>? _predicate;
        private Func<TEvent, TKey>? _keySelector;
        private Func<TEvent, string>? _correlationSelector;

        internal Builder(string name) => _name = name;

        public Builder When(Func<TEvent, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        public Builder WithKey(Func<TEvent, TKey> keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            return this;
        }

        public Builder WithCorrelation(Func<TEvent, string> correlationSelector)
        {
            _correlationSelector = correlationSelector ?? throw new ArgumentNullException(nameof(correlationSelector));
            return this;
        }

        public Builder WithMetadata(string name, Func<TEvent, string?> metadataSelector)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Event notification metadata name is required.", nameof(name));
            if (metadataSelector is null)
                throw new ArgumentNullException(nameof(metadataSelector));
            if (_metadataSelectors.Any(selector => string.Equals(selector.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Event notification metadata '{name}' is already registered.");

            _metadataSelectors.Add(new(name, metadataSelector));
            return this;
        }

        public EventNotification<TEvent, TKey> Build()
            => new(_name, _predicate, _keySelector, _correlationSelector, _metadataSelectors.ToArray());
    }

    private sealed class MetadataSelector
    {
        public MetadataSelector(string name, Func<TEvent, string?> select)
            => (Name, Select) = (name, select);

        public string Name { get; }

        public Func<TEvent, string?> Select { get; }
    }
}
