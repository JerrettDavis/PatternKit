using System.Collections;

namespace PatternKit.Messaging;

/// <summary>
/// Immutable message metadata for in-process messaging and enterprise integration patterns.
/// </summary>
/// <remarks>
/// Header names are compared with <see cref="StringComparer.OrdinalIgnoreCase"/> so transport-style
/// names such as <c>Correlation-Id</c> and <c>correlation-id</c> resolve to the same value.
/// </remarks>
public sealed class MessageHeaders : IReadOnlyDictionary<string, object?>
{
    private static readonly StringComparer HeaderComparer = StringComparer.OrdinalIgnoreCase;
    private readonly Dictionary<string, object?> _values;

    /// <summary>An empty immutable header collection.</summary>
    public static MessageHeaders Empty { get; } = new();

    /// <summary>
    /// Creates an empty header collection.
    /// </summary>
    public MessageHeaders()
        : this(new Dictionary<string, object?>(HeaderComparer), clone: false)
    {
    }

    /// <summary>
    /// Creates a header collection from existing values.
    /// </summary>
    /// <param name="values">The values to copy into the immutable collection.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when a header name is null, empty, or whitespace.</exception>
    public MessageHeaders(IEnumerable<KeyValuePair<string, object?>> values)
        : this(CopyFrom(values), clone: false)
    {
    }

    private MessageHeaders(Dictionary<string, object?> values, bool clone)
    {
        _values = clone ? CopyFrom(values) : values;
    }

    /// <inheritdoc />
    public int Count => _values.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => _values.Keys;

    /// <inheritdoc />
    public IEnumerable<object?> Values => _values.Values;

    /// <inheritdoc />
    public object? this[string key] => _values[key];

    /// <summary>Gets the well-known message identifier header when present.</summary>
    public string? MessageId => GetString(MessageHeaderNames.MessageId);

    /// <summary>Gets the well-known correlation identifier header when present.</summary>
    public string? CorrelationId => GetString(MessageHeaderNames.CorrelationId);

    /// <summary>Gets the well-known causation identifier header when present.</summary>
    public string? CausationId => GetString(MessageHeaderNames.CausationId);

    /// <summary>Gets the well-known idempotency key header when present.</summary>
    public string? IdempotencyKey => GetString(MessageHeaderNames.IdempotencyKey);

    /// <summary>Gets the well-known content type header when present.</summary>
    public string? ContentType => GetString(MessageHeaderNames.ContentType);

    /// <summary>Gets the well-known reply address header when present.</summary>
    public string? ReplyTo => GetString(MessageHeaderNames.ReplyTo);

    /// <summary>Gets the well-known timestamp header when present and parseable.</summary>
    public DateTimeOffset? Timestamp => TryGetDateTimeOffset(MessageHeaderNames.Timestamp, out var timestamp) ? timestamp : null;

    /// <summary>
    /// Returns a new header collection with <paramref name="name"/> set to <paramref name="value"/>.
    /// </summary>
    public MessageHeaders With(string name, object? value)
    {
        ValidateName(name);
        var copy = new Dictionary<string, object?>(_values, HeaderComparer)
        {
            [name] = value
        };

        return new MessageHeaders(copy, clone: false);
    }

    /// <summary>
    /// Returns a new header collection without <paramref name="name"/>.
    /// </summary>
    public MessageHeaders Without(string name)
    {
        ValidateName(name);
        if (!_values.ContainsKey(name))
            return this;

        var copy = new Dictionary<string, object?>(_values, HeaderComparer);
        copy.Remove(name);
        return copy.Count == 0 ? Empty : new MessageHeaders(copy, clone: false);
    }

    /// <summary>Returns a new collection with <see cref="MessageHeaderNames.MessageId"/> set.</summary>
    public MessageHeaders WithMessageId(string messageId) => WithRequiredString(MessageHeaderNames.MessageId, messageId);

    /// <summary>Returns a new collection with <see cref="MessageHeaderNames.CorrelationId"/> set.</summary>
    public MessageHeaders WithCorrelationId(string correlationId) => WithRequiredString(MessageHeaderNames.CorrelationId, correlationId);

    /// <summary>Returns a new collection with <see cref="MessageHeaderNames.CausationId"/> set.</summary>
    public MessageHeaders WithCausationId(string causationId) => WithRequiredString(MessageHeaderNames.CausationId, causationId);

    /// <summary>Returns a new collection with <see cref="MessageHeaderNames.IdempotencyKey"/> set.</summary>
    public MessageHeaders WithIdempotencyKey(string idempotencyKey) => WithRequiredString(MessageHeaderNames.IdempotencyKey, idempotencyKey);

    /// <summary>Returns a new collection with <see cref="MessageHeaderNames.ContentType"/> set.</summary>
    public MessageHeaders WithContentType(string contentType) => WithRequiredString(MessageHeaderNames.ContentType, contentType);

    /// <summary>Returns a new collection with <see cref="MessageHeaderNames.ReplyTo"/> set.</summary>
    public MessageHeaders WithReplyTo(string replyTo) => WithRequiredString(MessageHeaderNames.ReplyTo, replyTo);

    /// <summary>Returns a new collection with <see cref="MessageHeaderNames.Timestamp"/> set.</summary>
    public MessageHeaders WithTimestamp(DateTimeOffset timestamp) => With(MessageHeaderNames.Timestamp, timestamp);

    /// <inheritdoc />
    public bool ContainsKey(string key) => _values.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out object? value) => _values.TryGetValue(key, out value);

    /// <summary>
    /// Attempts to read a header as <typeparamref name="T"/>.
    /// </summary>
    public bool TryGet<T>(string name, out T? value)
    {
        if (_values.TryGetValue(name, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets a string header value when present. Non-string values are converted with <see cref="object.ToString"/>.
    /// </summary>
    public string? GetString(string name)
    {
        if (!_values.TryGetValue(name, out var value) || value is null)
            return null;

        return value as string ?? value.ToString();
    }

    /// <summary>
    /// Attempts to read a header as a <see cref="Guid"/> or parse a string header as a <see cref="Guid"/>.
    /// </summary>
    public bool TryGetGuid(string name, out Guid value)
    {
        if (_values.TryGetValue(name, out var raw))
        {
            if (raw is Guid guid)
            {
                value = guid;
                return true;
            }

            if (raw is string text && Guid.TryParse(text, out value))
                return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to read a header as a <see cref="DateTimeOffset"/> or parse a string header.
    /// </summary>
    public bool TryGetDateTimeOffset(string name, out DateTimeOffset value)
    {
        if (_values.TryGetValue(name, out var raw))
        {
            if (raw is DateTimeOffset dto)
            {
                value = dto;
                return true;
            }

            if (raw is DateTime dateTime)
            {
                value = new DateTimeOffset(dateTime);
                return true;
            }

            if (raw is string text && DateTimeOffset.TryParse(text, out value))
                return true;
        }

        value = default;
        return false;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private MessageHeaders WithRequiredString(string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Header value cannot be null, empty, or whitespace.", nameof(value));

        return With(name, value);
    }

    private static Dictionary<string, object?> CopyFrom(IEnumerable<KeyValuePair<string, object?>> values)
    {
        if (values is null)
            throw new ArgumentNullException(nameof(values));

        var copy = new Dictionary<string, object?>(HeaderComparer);
        foreach (var pair in values)
        {
            ValidateName(pair.Key);
            copy[pair.Key] = pair.Value;
        }

        return copy.Count == 0 ? new Dictionary<string, object?>(HeaderComparer) : copy;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Header name cannot be null, empty, or whitespace.", nameof(name));
    }
}
