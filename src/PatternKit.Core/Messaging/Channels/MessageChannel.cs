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

public sealed class InvalidMessageChannel<TPayload>
{
    private readonly MessageChannel<InvalidMessage<TPayload>> _invalidChannel;
    private readonly Func<Message<TPayload>, bool> _predicate;
    private readonly Func<Message<TPayload>, string> _reason;
    private readonly Func<DateTimeOffset> _clock;

    private InvalidMessageChannel(
        string name,
        MessageChannel<InvalidMessage<TPayload>> invalidChannel,
        Func<Message<TPayload>, bool> predicate,
        Func<Message<TPayload>, string> reason,
        Func<DateTimeOffset> clock)
        => (Name, _invalidChannel, _predicate, _reason, _clock) = (name, invalidChannel, predicate, reason, clock);

    public string Name { get; }

    public InvalidMessageRouteResult<TPayload> Route(Message<TPayload> message)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        if (!_predicate(message))
            return new(Name, _invalidChannel.Name, false, null, _invalidChannel.Count, null);

        var reason = _reason(message);
        if (string.IsNullOrWhiteSpace(reason))
            throw new InvalidOperationException("Invalid message reason cannot be null, empty, or whitespace.");

        var invalid = new InvalidMessage<TPayload>(message, reason, _clock());
        var send = _invalidChannel.Send(Message<InvalidMessage<TPayload>>.Create(invalid).WithHeaders(message.Headers));
        if (!send.Accepted)
            throw new InvalidOperationException(send.RejectionReason ?? "Invalid message channel rejected the message.");

        return new(Name, _invalidChannel.Name, true, reason, send.Count, invalid);
    }

    public IReadOnlyList<Message<InvalidMessage<TPayload>>> Snapshot() => _invalidChannel.Snapshot();

    public static Builder Create(string name = "invalid-message-channel") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private MessageChannel<InvalidMessage<TPayload>>? _invalidChannel;
        private Func<Message<TPayload>, bool> _predicate = static _ => true;
        private Func<Message<TPayload>, string> _reason = static _ => "Message failed validation.";
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Invalid message channel name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder To(MessageChannel<InvalidMessage<TPayload>> invalidChannel)
        {
            _invalidChannel = invalidChannel ?? throw new ArgumentNullException(nameof(invalidChannel));
            return this;
        }

        public Builder When(Func<Message<TPayload>, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        public Builder Because(Func<Message<TPayload>, string> reason)
        {
            _reason = reason ?? throw new ArgumentNullException(nameof(reason));
            return this;
        }

        public Builder WithClock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public InvalidMessageChannel<TPayload> Build()
        {
            if (_invalidChannel is null)
                throw new InvalidOperationException("Invalid message channel requires a target message channel.");

            return new(_name, _invalidChannel, _predicate, _reason, _clock);
        }
    }
}

public sealed class InvalidMessage<TPayload>
{
    public InvalidMessage(Message<TPayload> originalMessage, string reason, DateTimeOffset routedAt)
    {
        OriginalMessage = originalMessage ?? throw new ArgumentNullException(nameof(originalMessage));
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Invalid message reason cannot be null, empty, or whitespace.", nameof(reason));

        Reason = reason;
        RoutedAt = routedAt;
    }

    public Message<TPayload> OriginalMessage { get; }

    public string Reason { get; }

    public DateTimeOffset RoutedAt { get; }
}

public sealed class InvalidMessageRouteResult<TPayload>
{
    public InvalidMessageRouteResult(
        string routerName,
        string channelName,
        bool routed,
        string? reason,
        int invalidMessageCount,
        InvalidMessage<TPayload>? invalidMessage)
        => (RouterName, ChannelName, Routed, Reason, InvalidMessageCount, InvalidMessage) = (routerName, channelName, routed, reason, invalidMessageCount, invalidMessage);

    public string RouterName { get; }

    public string ChannelName { get; }

    public bool Routed { get; }

    public string? Reason { get; }

    public int InvalidMessageCount { get; }

    public InvalidMessage<TPayload>? InvalidMessage { get; }
}
