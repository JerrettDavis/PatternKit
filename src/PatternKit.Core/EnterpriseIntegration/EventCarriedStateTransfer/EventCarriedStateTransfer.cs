namespace PatternKit.EnterpriseIntegration.EventCarriedStateTransfer;

public sealed class EventCarriedStateTransferResult<TKey, TState>
{
    private EventCarriedStateTransferResult(string transferName, TKey? key, long version, TState? state, Exception? exception, bool transferred)
        => (TransferName, Key, Version, State, Exception, Transferred) = (transferName, key, version, state, exception, transferred);

    public string TransferName { get; }

    public TKey? Key { get; }

    public long Version { get; }

    public TState? State { get; }

    public Exception? Exception { get; }

    public bool Transferred { get; }

    public bool Failed => !Transferred;

    public static EventCarriedStateTransferResult<TKey, TState> Success(string transferName, TKey key, long version, TState state)
        => new(transferName, key, version, state, null, true);

    public static EventCarriedStateTransferResult<TKey, TState> Failure(string transferName, Exception exception)
        => new(transferName, default, 0, default, exception ?? throw new ArgumentNullException(nameof(exception)), false);
}

public sealed class EventCarriedStateTransfer<TEvent, TKey, TState>
{
    private readonly Func<TEvent, TKey> _keySelector;
    private readonly Func<TEvent, long> _versionSelector;
    private readonly Func<TEvent, TState> _stateSelector;

    private EventCarriedStateTransfer(
        string name,
        Func<TEvent, TKey>? keySelector,
        Func<TEvent, long>? versionSelector,
        Func<TEvent, TState>? stateSelector)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Event-carried state transfer name is required.", nameof(name));

        Name = name;
        _keySelector = keySelector ?? throw new InvalidOperationException("Event-carried state transfer requires a key selector.");
        _versionSelector = versionSelector ?? throw new InvalidOperationException("Event-carried state transfer requires a version selector.");
        _stateSelector = stateSelector ?? throw new InvalidOperationException("Event-carried state transfer requires a state mapper.");
    }

    public string Name { get; }

    public EventCarriedStateTransferResult<TKey, TState> Transfer(TEvent @event)
    {
        if (@event is null)
            throw new ArgumentNullException(nameof(@event));

        try
        {
            var key = _keySelector(@event);
            if (key is null)
                return EventCarriedStateTransferResult<TKey, TState>.Failure(Name, new InvalidOperationException("Event-carried state key selector returned null."));

            var state = _stateSelector(@event);
            if (state is null)
                return EventCarriedStateTransferResult<TKey, TState>.Failure(Name, new InvalidOperationException("Event-carried state mapper returned null."));

            return EventCarriedStateTransferResult<TKey, TState>.Success(Name, key, _versionSelector(@event), state);
        }
        catch (Exception ex)
        {
            return EventCarriedStateTransferResult<TKey, TState>.Failure(Name, ex);
        }
    }

    public static Builder Create(string name = "event-carried-state-transfer") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private Func<TEvent, TKey>? _keySelector;
        private Func<TEvent, long>? _versionSelector;
        private Func<TEvent, TState>? _stateSelector;

        internal Builder(string name) => _name = name;

        public Builder WithKey(Func<TEvent, TKey> keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            return this;
        }

        public Builder WithVersion(Func<TEvent, long> versionSelector)
        {
            _versionSelector = versionSelector ?? throw new ArgumentNullException(nameof(versionSelector));
            return this;
        }

        public Builder WithState(Func<TEvent, TState> stateSelector)
        {
            _stateSelector = stateSelector ?? throw new ArgumentNullException(nameof(stateSelector));
            return this;
        }

        public EventCarriedStateTransfer<TEvent, TKey, TState> Build()
            => new(_name, _keySelector, _versionSelector, _stateSelector);
    }
}
