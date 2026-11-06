namespace PatternKit.Messaging.Diagnostics;

/// <summary>
/// Records the systems, components, or operations that handled a message.
/// </summary>
public sealed class MessageHistory<TPayload>
{
    public const string DefaultHeaderName = "Message-History";

    private readonly string _component;
    private readonly string _action;
    private readonly string _headerName;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Func<Message<TPayload>, string?>? _details;

    private MessageHistory(
        string component,
        string action,
        string headerName,
        Func<DateTimeOffset> clock,
        Func<Message<TPayload>, string?>? details)
        => (_component, _action, _headerName, _clock, _details) = (component, action, headerName, clock, details);

    /// <summary>Appends this history step to the supplied message.</summary>
    public Message<TPayload> Record(Message<TPayload> message)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var entries = Read(message, _headerName);
        var next = entries.Concat([
            new MessageHistoryEntry(_component, _action, _clock(), _details?.Invoke(message))
        ]).ToArray();

        return message.WithHeader(_headerName, next);
    }

    /// <summary>Reads message history entries from the default header.</summary>
    public static IReadOnlyList<MessageHistoryEntry> Read(Message<TPayload> message)
        => Read(message, DefaultHeaderName);

    /// <summary>Reads message history entries from the configured header.</summary>
    public static IReadOnlyList<MessageHistoryEntry> Read(Message<TPayload> message, string headerName)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));
        if (string.IsNullOrWhiteSpace(headerName))
            throw new ArgumentException("Message history header name cannot be null, empty, or whitespace.", nameof(headerName));

        return message.Headers.TryGet<IReadOnlyList<MessageHistoryEntry>>(headerName, out var list) && list is not null
            ? list
            : [];
    }

    /// <summary>Creates a message history builder.</summary>
    public static Builder Create(string component) => new(component);

    /// <summary>Fluent builder for <see cref="MessageHistory{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _component;
        private string _action = "handled";
        private string _headerName = DefaultHeaderName;
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;
        private Func<Message<TPayload>, string?>? _details;

        internal Builder(string component)
        {
            if (string.IsNullOrWhiteSpace(component))
                throw new ArgumentException("Message history component cannot be null, empty, or whitespace.", nameof(component));

            _component = component;
        }

        /// <summary>Sets the logical action this component performed.</summary>
        public Builder Action(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                throw new ArgumentException("Message history action cannot be null, empty, or whitespace.", nameof(action));

            _action = action;
            return this;
        }

        /// <summary>Sets the header used to store history entries.</summary>
        public Builder Header(string headerName)
        {
            if (string.IsNullOrWhiteSpace(headerName))
                throw new ArgumentException("Message history header name cannot be null, empty, or whitespace.", nameof(headerName));

            _headerName = headerName;
            return this;
        }

        /// <summary>Sets a deterministic clock, useful for tests and replayable workflows.</summary>
        public Builder Clock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        /// <summary>Adds per-message details to each recorded history entry.</summary>
        public Builder Details(Func<Message<TPayload>, string?> details)
        {
            _details = details ?? throw new ArgumentNullException(nameof(details));
            return this;
        }

        /// <summary>Builds an immutable message history recorder.</summary>
        public MessageHistory<TPayload> Build()
            => new(_component, _action, _headerName, _clock, _details);
    }
}

/// <summary>One message history hop.</summary>
public sealed class MessageHistoryEntry
{
    public MessageHistoryEntry(string component, string action, DateTimeOffset timestamp, string? details)
        => (Component, Action, Timestamp, Details) = (component, action, timestamp, details);

    /// <summary>The component that handled the message.</summary>
    public string Component { get; }

    /// <summary>The action performed by the component.</summary>
    public string Action { get; }

    /// <summary>The time the action was recorded.</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>Optional component-specific details.</summary>
    public string? Details { get; }
}
