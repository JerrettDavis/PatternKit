using PatternKit.Messaging.Channels;

namespace PatternKit.Messaging.Gateways;

/// <summary>Typed request/response facade over message channels and handlers.</summary>
public sealed class MessagingGateway<TRequest, TResponse>
{
    public delegate Message<TResponse> GatewayHandler(Message<TRequest> request, MessageContext context);

    private readonly MessageChannel<TRequest> _requestChannel;
    private readonly GatewayHandler _handler;

    private MessagingGateway(string name, MessageChannel<TRequest> requestChannel, GatewayHandler handler)
        => (Name, _requestChannel, _handler) = (name, requestChannel, handler);

    public string Name { get; }

    public MessagingGatewayResult<TRequest, TResponse> Invoke(TRequest request, MessageContext? context = null)
    {
        var requestMessage = Message<TRequest>.Create(request);
        var effectiveContext = context ?? MessageContext.From(requestMessage);
        var send = _requestChannel.Send(requestMessage);
        if (!send.Accepted)
            return MessagingGatewayResult<TRequest, TResponse>.CreateRejected(Name, requestMessage, send);

        var response = _handler(requestMessage, effectiveContext);
        if (response is null)
            throw new InvalidOperationException("Messaging gateway handler returned null.");

        return MessagingGatewayResult<TRequest, TResponse>.CreateCompleted(Name, requestMessage, response, send);
    }

    public static Builder Create(string name = "messaging-gateway") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private MessageChannel<TRequest>? _requestChannel;
        private GatewayHandler? _handler;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Messaging gateway name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder SendTo(MessageChannel<TRequest> channel)
        {
            _requestChannel = channel ?? throw new ArgumentNullException(nameof(channel));
            return this;
        }

        public Builder Handle(GatewayHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public MessagingGateway<TRequest, TResponse> Build()
        {
            if (_requestChannel is null)
                throw new InvalidOperationException("Messaging gateway requires a request channel.");
            if (_handler is null)
                throw new InvalidOperationException("Messaging gateway requires a handler.");

            return new(_name, _requestChannel, _handler);
        }
    }
}

public sealed class MessagingGatewayResult<TRequest, TResponse>
{
    private MessagingGatewayResult(
        string gatewayName,
        Message<TRequest> request,
        Message<TResponse>? response,
        MessageChannelSendResult channelResult)
        => (GatewayName, Request, Response, ChannelResult) = (gatewayName, request, response, channelResult);

    public string GatewayName { get; }

    public Message<TRequest> Request { get; }

    public Message<TResponse>? Response { get; }

    public MessageChannelSendResult ChannelResult { get; }

    public bool Completed => ChannelResult.Accepted && Response is not null;

    internal static MessagingGatewayResult<TRequest, TResponse> CreateCompleted(
        string gatewayName,
        Message<TRequest> request,
        Message<TResponse> response,
        MessageChannelSendResult channelResult)
        => new(gatewayName, request, response, channelResult);

    internal static MessagingGatewayResult<TRequest, TResponse> CreateRejected(
        string gatewayName,
        Message<TRequest> request,
        MessageChannelSendResult channelResult)
        => new(gatewayName, request, null, channelResult);
}
