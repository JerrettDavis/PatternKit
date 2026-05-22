using PatternKit.Messaging.Channels;

namespace PatternKit.Messaging.Adapters;

/// <summary>Bridges external transport messages to PatternKit message channels.</summary>
public sealed class ChannelAdapter<TExternal, TPayload>
{
    public delegate Message<TPayload> InboundTranslator(TExternal external, MessageContext context);

    public delegate TExternal OutboundTranslator(Message<TPayload> message, MessageContext context);

    private readonly MessageChannel<TPayload> _inboundChannel;
    private readonly MessageChannel<TPayload> _outboundChannel;
    private readonly InboundTranslator _inbound;
    private readonly OutboundTranslator _outbound;

    private ChannelAdapter(
        string name,
        MessageChannel<TPayload> inboundChannel,
        MessageChannel<TPayload> outboundChannel,
        InboundTranslator inbound,
        OutboundTranslator outbound)
        => (Name, _inboundChannel, _outboundChannel, _inbound, _outbound) = (name, inboundChannel, outboundChannel, inbound, outbound);

    public string Name { get; }

    public ChannelAdapterInboundResult<TPayload> AcceptExternal(TExternal external, MessageContext? context = null)
    {
        var message = _inbound(external, context ?? MessageContext.Empty);
        if (message is null)
            throw new InvalidOperationException("Inbound channel adapter translator returned null.");

        var result = _inboundChannel.Send(message);
        return new ChannelAdapterInboundResult<TPayload>(Name, message, result);
    }

    public ChannelAdapterOutboundResult<TExternal> TryTakeExternal(MessageContext? context = null)
    {
        var received = _outboundChannel.TryReceive();
        if (!received.Received)
            return ChannelAdapterOutboundResult<TExternal>.Empty(Name, received.ChannelName);

        var external = _outbound(received.Message!, context ?? MessageContext.From(received.Message!));
        return ChannelAdapterOutboundResult<TExternal>.Success(Name, received.ChannelName, external);
    }

    public static Builder Create(string name = "channel-adapter") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private MessageChannel<TPayload>? _inboundChannel;
        private MessageChannel<TPayload>? _outboundChannel;
        private InboundTranslator? _inbound;
        private OutboundTranslator? _outbound;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Channel adapter name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder ReceiveInto(MessageChannel<TPayload> channel)
        {
            _inboundChannel = channel ?? throw new ArgumentNullException(nameof(channel));
            return this;
        }

        public Builder SendFrom(MessageChannel<TPayload> channel)
        {
            _outboundChannel = channel ?? throw new ArgumentNullException(nameof(channel));
            return this;
        }

        public Builder MapInbound(InboundTranslator translator)
        {
            _inbound = translator ?? throw new ArgumentNullException(nameof(translator));
            return this;
        }

        public Builder MapOutbound(OutboundTranslator translator)
        {
            _outbound = translator ?? throw new ArgumentNullException(nameof(translator));
            return this;
        }

        public ChannelAdapter<TExternal, TPayload> Build()
        {
            if (_inboundChannel is null)
                throw new InvalidOperationException("Channel adapter requires an inbound channel.");
            if (_outboundChannel is null)
                throw new InvalidOperationException("Channel adapter requires an outbound channel.");
            if (_inbound is null)
                throw new InvalidOperationException("Channel adapter requires an inbound translator.");
            if (_outbound is null)
                throw new InvalidOperationException("Channel adapter requires an outbound translator.");

            return new(_name, _inboundChannel, _outboundChannel, _inbound, _outbound);
        }
    }
}

public sealed class ChannelAdapterInboundResult<TPayload>
{
    internal ChannelAdapterInboundResult(string adapterName, Message<TPayload> message, MessageChannelSendResult channelResult)
        => (AdapterName, Message, ChannelResult) = (adapterName, message, channelResult);

    public string AdapterName { get; }

    public Message<TPayload> Message { get; }

    public MessageChannelSendResult ChannelResult { get; }

    public bool Accepted => ChannelResult.Accepted;
}

public sealed class ChannelAdapterOutboundResult<TExternal>
{
    private ChannelAdapterOutboundResult(string adapterName, string channelName, bool produced, TExternal? external)
        => (AdapterName, ChannelName, Produced, External) = (adapterName, channelName, produced, external);

    public string AdapterName { get; }

    public string ChannelName { get; }

    public bool Produced { get; }

    public TExternal? External { get; }

    internal static ChannelAdapterOutboundResult<TExternal> Success(string adapterName, string channelName, TExternal external)
        => new(adapterName, channelName, true, external);

    internal static ChannelAdapterOutboundResult<TExternal> Empty(string adapterName, string channelName)
        => new(adapterName, channelName, false, default);
}
