namespace PatternKit.Messaging.Correlation;

/// <summary>
/// Ensures messages that participate in the same business flow carry a stable correlation identifier.
/// </summary>
/// <typeparam name="TPayload">The payload type carried by the message.</typeparam>
public sealed class CorrelationIdentifier<TPayload>
{
    private readonly string _headerName;
    private readonly Func<Message<TPayload>, MessageContext, string?> _selector;
    private readonly Func<string> _generator;
    private readonly bool _preserveExisting;

    private CorrelationIdentifier(
        string headerName,
        Func<Message<TPayload>, MessageContext, string?> selector,
        Func<string> generator,
        bool preserveExisting)
    {
        _headerName = headerName;
        _selector = selector;
        _generator = generator;
        _preserveExisting = preserveExisting;
    }

    /// <summary>Gets the header used to store the correlation identifier.</summary>
    public string HeaderName => _headerName;

    /// <summary>Creates a correlation identifier builder.</summary>
    public static Builder Create() => new();

    /// <summary>
    /// Returns the correlation identifier currently carried by <paramref name="message"/>.
    /// </summary>
    public string? Read(Message<TPayload> message)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        return message.Headers.GetString(_headerName);
    }

    /// <summary>
    /// Returns a message with a correlation identifier, preserving an existing identifier by default.
    /// </summary>
    public Message<TPayload> Ensure(Message<TPayload> message)
        => Ensure(message, MessageContext.Empty);

    /// <summary>
    /// Returns a message with a correlation identifier, preserving an existing identifier by default.
    /// </summary>
    public Message<TPayload> Ensure(Message<TPayload> message, MessageContext context)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var existing = message.Headers.GetString(_headerName);
        if (_preserveExisting && !string.IsNullOrWhiteSpace(existing))
            return message;

        var selected = _selector(message, context);
        var value = string.IsNullOrWhiteSpace(selected) ? _generator() : selected!;
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Correlation identifier selector returned an empty value.");

        return message.WithHeader(_headerName, value);
    }

    /// <summary>
    /// Correlates a reply to the request by copying the request correlation id or, when absent, its message id.
    /// </summary>
    public Message<TReply> CorrelateReply<TReply>(Message<TReply> reply, Message<TPayload> request)
    {
        if (reply is null)
            throw new ArgumentNullException(nameof(reply));
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var correlationId = request.Headers.GetString(_headerName) ?? request.Headers.MessageId;
        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = _generator();

        return reply.WithHeader(_headerName, correlationId);
    }

    /// <summary>Fluent builder for <see cref="CorrelationIdentifier{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private string _headerName = MessageHeaderNames.CorrelationId;
        private Func<Message<TPayload>, MessageContext, string?> _selector =
            static (message, context) => message.Headers.CorrelationId ?? context.Headers.CorrelationId ?? message.Headers.MessageId;
        private Func<string> _generator = static () => Guid.NewGuid().ToString("N");
        private bool _preserveExisting = true;

        /// <summary>Uses a custom header for the correlation identifier.</summary>
        public Builder Header(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
                throw new ArgumentException("Header name cannot be null, empty, or whitespace.", nameof(headerName));

            _headerName = headerName;
            return this;
        }

        /// <summary>Controls whether an existing correlation identifier is kept.</summary>
        public Builder PreserveExisting(bool preserveExisting = true)
        {
            _preserveExisting = preserveExisting;
            return this;
        }

        /// <summary>Uses a selector to derive the identifier from the message and context.</summary>
        public Builder Select(Func<Message<TPayload>, MessageContext, string?> selector)
        {
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            return this;
        }

        /// <summary>Uses the message id as the correlation identifier when one is available.</summary>
        public Builder UseMessageId()
            => Select(static (message, _) => message.Headers.MessageId);

        /// <summary>Uses a custom generator when no identifier can be selected.</summary>
        public Builder GenerateWith(Func<string> generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            return this;
        }

        /// <summary>Builds the configured correlation identifier.</summary>
        public CorrelationIdentifier<TPayload> Build()
            => new(_headerName, _selector, _generator, _preserveExisting);
    }
}
