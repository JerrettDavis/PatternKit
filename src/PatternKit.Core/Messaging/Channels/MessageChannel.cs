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
