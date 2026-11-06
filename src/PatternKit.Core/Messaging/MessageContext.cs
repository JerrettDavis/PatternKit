using System.Collections.ObjectModel;

namespace PatternKit.Messaging;

/// <summary>
/// Immutable per-execution context for message routing, diagnostics, and pattern composition.
/// </summary>
public sealed class MessageContext
{
    private static readonly StringComparer ItemComparer = StringComparer.Ordinal;
    private readonly Dictionary<string, object?> _items;

    /// <summary>An empty message context.</summary>
    public static MessageContext Empty { get; } = new(MessageHeaders.Empty, new Dictionary<string, object?>(ItemComparer), cloneItems: false);

    /// <summary>
    /// Creates a new message context.
    /// </summary>
    /// <param name="headers">Headers available to the current execution.</param>
    /// <param name="cancellationToken">Cancellation token for async message processing.</param>
    public MessageContext(MessageHeaders? headers = null, CancellationToken cancellationToken = default)
        : this(headers ?? MessageHeaders.Empty, new Dictionary<string, object?>(ItemComparer), cancellationToken, cloneItems: false)
    {
    }

    private MessageContext(
        MessageHeaders headers,
        Dictionary<string, object?> items,
        CancellationToken cancellationToken = default,
        bool cloneItems = true)
    {
        Headers = headers ?? throw new ArgumentNullException(nameof(headers));
        _items = cloneItems ? new Dictionary<string, object?>(items, ItemComparer) : items;
        CancellationToken = cancellationToken;
    }

    /// <summary>Headers available to the current execution.</summary>
    public MessageHeaders Headers { get; }

    /// <summary>Cancellation token for async message processing.</summary>
    public CancellationToken CancellationToken { get; }

    /// <summary>Scoped execution items for diagnostics and pattern composition.</summary>
    public IReadOnlyDictionary<string, object?> Items => new ReadOnlyDictionary<string, object?>(_items);

    /// <summary>
    /// Creates a context from a message, copying the message headers.
    /// </summary>
    public static MessageContext From<TPayload>(Message<TPayload> message, CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        return new MessageContext(message.Headers, cancellationToken);
    }

    /// <summary>
    /// Returns a new context with the supplied headers.
    /// </summary>
    public MessageContext WithHeaders(MessageHeaders headers)
    {
        if (headers is null)
            throw new ArgumentNullException(nameof(headers));

        return new MessageContext(headers, _items, CancellationToken);
    }

    /// <summary>
    /// Returns a new context with a header value set.
    /// </summary>
    public MessageContext WithHeader(string name, object? value) => WithHeaders(Headers.With(name, value));

    /// <summary>
    /// Returns a new context with the supplied cancellation token.
    /// </summary>
    public MessageContext WithCancellation(CancellationToken cancellationToken) => new(Headers, _items, cancellationToken);

    /// <summary>
    /// Returns a new context with an execution item set.
    /// </summary>
    public MessageContext WithItem(string key, object? value)
    {
        ValidateItemKey(key);
        var copy = new Dictionary<string, object?>(_items, ItemComparer)
        {
            [key] = value
        };

        return new MessageContext(Headers, copy, CancellationToken, cloneItems: false);
    }

    /// <summary>
    /// Returns a new context without an execution item.
    /// </summary>
    public MessageContext WithoutItem(string key)
    {
        ValidateItemKey(key);
        if (!_items.ContainsKey(key))
            return this;

        var copy = new Dictionary<string, object?>(_items, ItemComparer);
        copy.Remove(key);
        return new MessageContext(Headers, copy, CancellationToken, cloneItems: false);
    }

    /// <summary>
    /// Attempts to read an execution item as <typeparamref name="T"/>.
    /// </summary>
    public bool TryGetItem<T>(string key, out T? value)
    {
        if (_items.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    private static void ValidateItemKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Item key cannot be null, empty, or whitespace.", nameof(key));
    }
}
