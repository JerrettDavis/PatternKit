using System.Collections.ObjectModel;

namespace PatternKit.Messaging.Routing;

/// <summary>
/// Splitter pattern that turns one message into zero or more item messages.
/// </summary>
public sealed class Splitter<TPayload, TItem>
{
    /// <summary>Splits the source message into item payloads.</summary>
    public delegate IEnumerable<TItem> SplitHandler(Message<TPayload> message, MessageContext context);

    private readonly SplitHandler _handler;

    private Splitter(SplitHandler handler) => _handler = handler;

    /// <summary>
    /// Splits <paramref name="message"/> into item envelopes that preserve correlation metadata.
    /// </summary>
    public IReadOnlyList<Message<TItem>> Split(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var items = _handler(message, effectiveContext) ?? throw new InvalidOperationException("Splitter returned null.");
        var result = new List<Message<TItem>>();
        foreach (var item in items)
            result.Add(new Message<TItem>(item, CreateChildHeaders(message.Headers)));

        return new ReadOnlyCollection<Message<TItem>>(result);
    }

    /// <summary>Creates a splitter builder.</summary>
    public static Builder Create() => new();

    private static MessageHeaders CreateChildHeaders(MessageHeaders parentHeaders)
    {
        var headers = parentHeaders;
        if (parentHeaders.MessageId is { Length: > 0 } messageId && parentHeaders.CausationId is null)
            headers = headers.WithCausationId(messageId);

        return headers;
    }

    /// <summary>Fluent builder for <see cref="Splitter{TPayload,TItem}"/>.</summary>
    public sealed class Builder
    {
        private SplitHandler? _handler;

        /// <summary>Sets the split handler.</summary>
        public Builder Use(SplitHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <summary>Builds an immutable splitter.</summary>
        public Splitter<TPayload, TItem> Build()
            => new(_handler ?? throw new InvalidOperationException("A splitter handler is required."));
    }
}
