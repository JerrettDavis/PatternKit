namespace PatternKit.Messaging.Consumers;

/// <summary>Pull-based consumer that explicitly polls a message source.</summary>
public sealed class PollingConsumer<TPayload>
{
    public delegate Message<TPayload>? PollSource(MessageContext context);

    private readonly PollSource _source;

    private PollingConsumer(string name, PollSource source)
        => (Name, _source) = (name, source);

    public string Name { get; }

    public PollingConsumerResult<TPayload> Poll(MessageContext? context = null)
    {
        var effectiveContext = context ?? MessageContext.Empty;
        var message = _source(effectiveContext);
        return message is null
            ? PollingConsumerResult<TPayload>.Empty(Name)
            : PollingConsumerResult<TPayload>.Success(Name, message);
    }

    public static Builder Create(string name = "polling-consumer") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private PollSource? _source;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Polling consumer name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder From(PollSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            return this;
        }

        public PollingConsumer<TPayload> Build()
        {
            if (_source is null)
                throw new InvalidOperationException("Polling consumer requires a message source.");

            return new(_name, _source);
        }
    }
}

public sealed class PollingConsumerResult<TPayload>
{
    private PollingConsumerResult(string consumerName, bool received, Message<TPayload>? message)
        => (ConsumerName, Received, Message) = (consumerName, received, message);

    public string ConsumerName { get; }

    public bool Received { get; }

    public Message<TPayload>? Message { get; }

    internal static PollingConsumerResult<TPayload> Success(string consumerName, Message<TPayload> message) => new(consumerName, true, message);

    internal static PollingConsumerResult<TPayload> Empty(string consumerName) => new(consumerName, false, null);
}
