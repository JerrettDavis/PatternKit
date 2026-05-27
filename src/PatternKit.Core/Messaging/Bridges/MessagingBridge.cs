using PatternKit.Messaging.Channels;

namespace PatternKit.Messaging.Bridges;

/// <summary>
/// Bridges messages from one channel topology into another bus while keeping translation explicit.
/// </summary>
public sealed class MessagingBridge<TInbound, TOutbound>
{
    private readonly MessageChannel<TInbound> _source;
    private readonly MessageBus<TOutbound> _target;
    private readonly Func<Message<TInbound>, Message<TOutbound>> _translator;
    private readonly Func<Message<TInbound>, string> _topicSelector;

    private MessagingBridge(
        string name,
        MessageChannel<TInbound> source,
        MessageBus<TOutbound> target,
        Func<Message<TInbound>, Message<TOutbound>> translator,
        Func<Message<TInbound>, string> topicSelector)
        => (Name, _source, _target, _translator, _topicSelector) = (name, source, target, translator, topicSelector);

    public string Name { get; }

    public MessagingBridgeResult<TOutbound> BridgeNext()
    {
        var received = _source.TryReceive();
        if (!received.Received || received.Message is null)
            return MessagingBridgeResult<TOutbound>.Empty(Name, _source.Name, _target.Name);

        var topic = _topicSelector(received.Message);
        if (string.IsNullOrWhiteSpace(topic))
            throw new InvalidOperationException("Messaging bridge topic selector returned a null, empty, or whitespace topic.");

        var outbound = _translator(received.Message) ?? throw new InvalidOperationException("Messaging bridge translator returned null.");
        var publish = _target.Publish(topic, outbound);
        return MessagingBridgeResult<TOutbound>.Published(Name, _source.Name, _target.Name, topic, outbound, publish);
    }

    public IReadOnlyList<MessagingBridgeResult<TOutbound>> BridgeAll()
    {
        var results = new List<MessagingBridgeResult<TOutbound>>();
        while (true)
        {
            var result = BridgeNext();
            if (!result.Bridged)
                return results;

            results.Add(result);
        }
    }

    public static Builder Create(string name = "messaging-bridge") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private MessageChannel<TInbound>? _source;
        private MessageBus<TOutbound>? _target;
        private Func<Message<TInbound>, Message<TOutbound>>? _translator;
        private Func<Message<TInbound>, string>? _topicSelector;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Messaging bridge name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder From(MessageChannel<TInbound> source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            return this;
        }

        public Builder To(MessageBus<TOutbound> target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            return this;
        }

        public Builder TranslateWith(Func<Message<TInbound>, Message<TOutbound>> translator)
        {
            _translator = translator ?? throw new ArgumentNullException(nameof(translator));
            return this;
        }

        public Builder SelectTopic(Func<Message<TInbound>, string> topicSelector)
        {
            _topicSelector = topicSelector ?? throw new ArgumentNullException(nameof(topicSelector));
            return this;
        }

        public Builder PreserveHeaders(Func<TInbound, TOutbound> payloadTranslator)
        {
            if (payloadTranslator is null)
                throw new ArgumentNullException(nameof(payloadTranslator));

            _translator = message => message.WithPayload(payloadTranslator(message.Payload));
            return this;
        }

        public MessagingBridge<TInbound, TOutbound> Build()
        {
            if (_source is null)
                throw new InvalidOperationException("Messaging bridge source channel is required.");
            if (_target is null)
                throw new InvalidOperationException("Messaging bridge target bus is required.");
            if (_translator is null)
                throw new InvalidOperationException("Messaging bridge translator is required.");
            if (_topicSelector is null)
                throw new InvalidOperationException("Messaging bridge topic selector is required.");

            return new(_name, _source, _target, _translator, _topicSelector);
        }
    }
}

public sealed class MessagingBridgeResult<TOutbound>
{
    private MessagingBridgeResult(
        string bridgeName,
        string sourceChannelName,
        string targetBusName,
        bool bridged,
        string? topic,
        Message<TOutbound>? message,
        MessageBusPublishResult? publishResult)
        => (BridgeName, SourceChannelName, TargetBusName, Bridged, Topic, Message, PublishResult) =
            (bridgeName, sourceChannelName, targetBusName, bridged, topic, message, publishResult);

    public string BridgeName { get; }

    public string SourceChannelName { get; }

    public string TargetBusName { get; }

    public bool Bridged { get; }

    public string? Topic { get; }

    public Message<TOutbound>? Message { get; }

    public MessageBusPublishResult? PublishResult { get; }

    public int AcceptedCount => PublishResult?.AcceptedCount ?? 0;

    public static MessagingBridgeResult<TOutbound> Empty(string bridgeName, string sourceChannelName, string targetBusName)
        => new(bridgeName, sourceChannelName, targetBusName, false, null, null, null);

    public static MessagingBridgeResult<TOutbound> Published(
        string bridgeName,
        string sourceChannelName,
        string targetBusName,
        string topic,
        Message<TOutbound> message,
        MessageBusPublishResult publishResult)
        => new(bridgeName, sourceChannelName, targetBusName, true, topic, message, publishResult);
}
