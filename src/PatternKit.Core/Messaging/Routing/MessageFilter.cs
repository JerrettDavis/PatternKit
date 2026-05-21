namespace PatternKit.Messaging.Routing;

/// <summary>
/// Message filter that accepts messages matching at least one named rule and rejects the rest.
/// </summary>
public sealed class MessageFilter<TPayload>
{
    /// <summary>Predicate used to decide whether a message should pass through the filter.</summary>
    public delegate bool FilterPredicate(Message<TPayload> message, MessageContext context);

    private readonly string _name;
    private readonly FilterRule[] _rules;
    private readonly string _rejectionReason;

    private MessageFilter(string name, FilterRule[] rules, string rejectionReason)
        => (_name, _rules, _rejectionReason) = (name, rules, rejectionReason);

    /// <summary>Filters <paramref name="message"/> and returns whether it should continue downstream.</summary>
    public MessageFilterResult<TPayload> Filter(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        foreach (var rule in _rules)
        {
            if (rule.Predicate(message, effectiveContext))
                return MessageFilterResult<TPayload>.Accept(message, _name, rule.Name);
        }

        return MessageFilterResult<TPayload>.Reject(message, _name, _rejectionReason);
    }

    /// <summary>Creates a new message filter builder.</summary>
    public static Builder Create(string name = "message-filter") => new(name);

    /// <summary>Fluent builder for <see cref="MessageFilter{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<FilterRule> _rules = new(4);
        private string _rejectionReason = "Message did not match any allow rule.";

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Message filter name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Adds a named rule that allows matching messages through the filter.</summary>
        public Builder AllowWhen(string name, FilterPredicate predicate)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Message filter rule name cannot be null, empty, or whitespace.", nameof(name));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            _rules.Add(new FilterRule(name, predicate));
            return this;
        }

        /// <summary>Configures the reason returned when no allow rule matches.</summary>
        public Builder RejectUnmatched(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Message filter rejection reason cannot be null, empty, or whitespace.", nameof(reason));

            _rejectionReason = reason;
            return this;
        }

        /// <summary>Builds an immutable message filter.</summary>
        public MessageFilter<TPayload> Build()
        {
            if (_rules.Count == 0)
                throw new InvalidOperationException("Message filter must have at least one allow rule.");

            return new MessageFilter<TPayload>(_name, _rules.ToArray(), _rejectionReason);
        }
    }

    private sealed class FilterRule
    {
        public FilterRule(string name, FilterPredicate predicate)
            => (Name, Predicate) = (name, predicate);

        public string Name { get; }

        public FilterPredicate Predicate { get; }
    }
}

/// <summary>
/// Result returned by <see cref="MessageFilter{TPayload}"/>.
/// </summary>
public sealed class MessageFilterResult<TPayload>
{
    private MessageFilterResult(Message<TPayload> message, string filterName, string? ruleName, bool accepted, string? rejectionReason)
        => (Message, FilterName, RuleName, Accepted, RejectionReason) = (message, filterName, ruleName, accepted, rejectionReason);

    /// <summary>The original message evaluated by the filter.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>The name of the filter that evaluated the message.</summary>
    public string FilterName { get; }

    /// <summary>The name of the allow rule that matched, or null when rejected.</summary>
    public string? RuleName { get; }

    /// <summary>True when the message should continue downstream.</summary>
    public bool Accepted { get; }

    /// <summary>Human-readable rejection reason when the message is rejected.</summary>
    public string? RejectionReason { get; }

    /// <summary>Creates an accepted result.</summary>
    public static MessageFilterResult<TPayload> Accept(Message<TPayload> message, string filterName, string ruleName)
        => new(message, filterName, ruleName, true, null);

    /// <summary>Creates a rejected result.</summary>
    public static MessageFilterResult<TPayload> Reject(Message<TPayload> message, string filterName, string rejectionReason)
        => new(message, filterName, null, false, rejectionReason);
}
