namespace PatternKit.Messaging.Channels;

/// <summary>Thread-safe in-memory channel for typed PatternKit messages.</summary>
public sealed class MessageChannel<TPayload>
{
    private readonly object _gate = new();
    private readonly Queue<Message<TPayload>> _messages = new();
    private readonly int? _capacity;
    private readonly MessageChannelBackpressurePolicy _backpressure;

    private MessageChannel(string name, int? capacity, MessageChannelBackpressurePolicy backpressure)
        => (Name, _capacity, _backpressure) = (name, capacity, backpressure);

    public string Name { get; }

    public int Count
    {
        get
        {
            lock (_gate)
                return _messages.Count;
        }
    }

    public MessageChannelSendResult Send(Message<TPayload> message)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        lock (_gate)
        {
            if (_capacity.HasValue && _messages.Count >= _capacity.Value)
            {
                if (_backpressure == MessageChannelBackpressurePolicy.Reject)
                    return MessageChannelSendResult.Failed(Name, _messages.Count, "Channel capacity has been reached.");

                _messages.Dequeue();
            }

            _messages.Enqueue(message);
            return MessageChannelSendResult.Success(Name, _messages.Count);
        }
    }

    public MessageChannelReceiveResult<TPayload> TryReceive()
    {
        lock (_gate)
        {
            if (_messages.Count == 0)
                return MessageChannelReceiveResult<TPayload>.Empty(Name);

            var message = _messages.Dequeue();
            return MessageChannelReceiveResult<TPayload>.Success(Name, message, _messages.Count);
        }
    }

    public IReadOnlyList<Message<TPayload>> Drain(Func<Message<TPayload>, bool>? predicate = null)
    {
        var removed = new List<Message<TPayload>>();

        lock (_gate)
        {
            var messages = _messages.ToArray();
            var retained = new List<Message<TPayload>>(messages.Length);

            foreach (var message in messages)
            {
                if (predicate is null || predicate(message))
                    removed.Add(message);
                else
                    retained.Add(message);
            }

            _messages.Clear();
            foreach (var message in retained)
                _messages.Enqueue(message);
        }

        return removed;
    }

    public IReadOnlyList<Message<TPayload>> Snapshot()
    {
        lock (_gate)
            return _messages.ToArray();
    }

    public static Builder Create(string name = "message-channel") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private int? _capacity;
        private MessageChannelBackpressurePolicy _backpressure = MessageChannelBackpressurePolicy.Reject;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Message channel name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder WithCapacity(int capacity, MessageChannelBackpressurePolicy backpressure = MessageChannelBackpressurePolicy.Reject)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Channel capacity must be greater than zero.");

            _capacity = capacity;
            _backpressure = backpressure;
            return this;
        }

        public MessageChannel<TPayload> Build() => new(_name, _capacity, _backpressure);
    }
}

public enum MessageChannelBackpressurePolicy
{
    Reject,
    DropOldest
}

public sealed class MessageChannelSendResult
{
    private MessageChannelSendResult(string channelName, bool accepted, int count, string? rejectionReason)
        => (ChannelName, Accepted, Count, RejectionReason) = (channelName, accepted, count, rejectionReason);

    public string ChannelName { get; }

    public bool Accepted { get; }

    public int Count { get; }

    public string? RejectionReason { get; }

    internal static MessageChannelSendResult Success(string channelName, int count) => new(channelName, true, count, null);

    internal static MessageChannelSendResult Failed(string channelName, int count, string reason) => new(channelName, false, count, reason);
}

public sealed class MessageChannelReceiveResult<TPayload>
{
    private MessageChannelReceiveResult(string channelName, bool received, Message<TPayload>? message, int count)
        => (ChannelName, Received, Message, Count) = (channelName, received, message, count);

    public string ChannelName { get; }

    public bool Received { get; }

    public Message<TPayload>? Message { get; }

    public int Count { get; }

    internal static MessageChannelReceiveResult<TPayload> Success(string channelName, Message<TPayload> message, int count) => new(channelName, true, message, count);

    internal static MessageChannelReceiveResult<TPayload> Empty(string channelName) => new(channelName, false, null, 0);
}

public sealed class ChannelPurger<TPayload>
{
    private readonly MessageChannel<TPayload> _channel;
    private readonly Func<Message<TPayload>, bool>? _predicate;
    private readonly Action<ChannelPurgeRecord<TPayload>>? _audit;

    private ChannelPurger(
        string name,
        MessageChannel<TPayload> channel,
        Func<Message<TPayload>, bool>? predicate,
        Action<ChannelPurgeRecord<TPayload>>? audit)
        => (Name, _channel, _predicate, _audit) = (name, channel, predicate, audit);

    public string Name { get; }

    public ChannelPurgeResult<TPayload> Purge()
    {
        var purged = _channel.Drain(_predicate);
        foreach (var message in purged)
            _audit?.Invoke(new(Name, _channel.Name, message));

        return new(Name, _channel.Name, purged.Count, _channel.Count, purged);
    }

    public static Builder Create(string name = "channel-purger") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private MessageChannel<TPayload>? _channel;
        private Func<Message<TPayload>, bool>? _predicate;
        private Action<ChannelPurgeRecord<TPayload>>? _audit;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Channel purger name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder From(MessageChannel<TPayload> channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            return this;
        }

        public Builder When(Func<Message<TPayload>, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        public Builder AuditWith(Action<ChannelPurgeRecord<TPayload>> audit)
        {
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            return this;
        }

        public ChannelPurger<TPayload> Build()
        {
            if (_channel is null)
                throw new InvalidOperationException("Channel purger requires a message channel.");

            return new(_name, _channel, _predicate, _audit);
        }
    }
}

public sealed class ChannelPurgeRecord<TPayload>
{
    public ChannelPurgeRecord(string purgerName, string channelName, Message<TPayload> message)
        => (PurgerName, ChannelName, Message) = (purgerName, channelName, message);

    public string PurgerName { get; }

    public string ChannelName { get; }

    public Message<TPayload> Message { get; }
}

public sealed class ChannelPurgeResult<TPayload>
{
    public ChannelPurgeResult(
        string purgerName,
        string channelName,
        int purgedCount,
        int remainingCount,
        IReadOnlyList<Message<TPayload>> purgedMessages)
        => (PurgerName, ChannelName, PurgedCount, RemainingCount, PurgedMessages) = (purgerName, channelName, purgedCount, remainingCount, purgedMessages);

    public string PurgerName { get; }

    public string ChannelName { get; }

    public int PurgedCount { get; }

    public int RemainingCount { get; }

    public IReadOnlyList<Message<TPayload>> PurgedMessages { get; }
}
