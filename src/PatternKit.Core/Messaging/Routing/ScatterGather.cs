namespace PatternKit.Messaging.Routing;

/// <summary>
/// Sends one request to multiple recipients and aggregates their replies.
/// </summary>
public sealed class ScatterGather<TRequest, TResponse, TResult>
{
    public delegate bool RecipientPredicate(Message<TRequest> message, MessageContext context);

    public delegate ScatterGatherReply<TResponse> RecipientHandler(Message<TRequest> message, MessageContext context);

    public delegate TResult ResponseAggregator(
        IReadOnlyList<ScatterGatherReply<TResponse>> replies,
        Message<TRequest> message,
        MessageContext context);

    private readonly string _name;
    private readonly IReadOnlyList<RecipientRegistration> _recipients;
    private readonly ResponseAggregator _aggregator;

    private ScatterGather(string name, IReadOnlyList<RecipientRegistration> recipients, ResponseAggregator aggregator)
        => (_name, _recipients, _aggregator) = (name, recipients, aggregator);

    public ScatterGatherResult<TResponse, TResult> Dispatch(Message<TRequest> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var replies = new List<ScatterGatherReply<TResponse>>();
        foreach (var recipient in _recipients)
        {
            if (!recipient.Predicate(message, effectiveContext))
                continue;

            var reply = recipient.Handler(message, effectiveContext).WithRecipient(recipient.Name);
            replies.Add(reply);
        }

        if (replies.Count == 0)
            return ScatterGatherResult<TResponse, TResult>.Rejected(_name, replies, "No scatter-gather recipients accepted the request.");

        var result = _aggregator(replies, message, effectiveContext);
        return ScatterGatherResult<TResponse, TResult>.Success(_name, replies, result);
    }

    public static Builder Create(string name = "scatter-gather") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<RecipientRegistration> _recipients = [];
        private ResponseAggregator? _aggregator;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Scatter-gather name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        public Builder AddRecipient(string name, RecipientHandler handler, RecipientPredicate? predicate = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Recipient name cannot be null, empty, or whitespace.", nameof(name));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));
            if (_recipients.Any(recipient => recipient.Name == name))
                throw new InvalidOperationException("Scatter-gather recipient names must be unique.");

            _recipients.Add(new(name, handler, predicate ?? Always));
            return this;
        }

        public Builder AggregateWith(ResponseAggregator aggregator)
        {
            _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
            return this;
        }

        public ScatterGather<TRequest, TResponse, TResult> Build()
        {
            if (_recipients.Count == 0)
                throw new InvalidOperationException("Scatter-gather requires at least one recipient.");
            if (_aggregator is null)
                throw new InvalidOperationException("Scatter-gather requires an aggregator.");

            return new(_name, _recipients.ToArray(), _aggregator);
        }

        private static bool Always(Message<TRequest> message, MessageContext context) => true;
    }

    private sealed class RecipientRegistration
    {
        public RecipientRegistration(string name, RecipientHandler handler, RecipientPredicate predicate)
            => (Name, Handler, Predicate) = (name, handler, predicate);

        public string Name { get; }

        public RecipientHandler Handler { get; }

        public RecipientPredicate Predicate { get; }
    }
}

/// <summary>Reply captured from one scatter-gather recipient.</summary>
public sealed class ScatterGatherReply<TResponse>
{
    private ScatterGatherReply(string recipientName, TResponse? response, bool accepted, string? rejectionReason)
        => (RecipientName, Response, Accepted, RejectionReason) = (recipientName, response, accepted, rejectionReason);

    public string RecipientName { get; }

    public TResponse? Response { get; }

    public bool Accepted { get; }

    public string? RejectionReason { get; }

    public static ScatterGatherReply<TResponse> Success(TResponse response)
        => new(string.Empty, response, true, null);

    public static ScatterGatherReply<TResponse> Failure(string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new ArgumentException("Rejection reason cannot be null, empty, or whitespace.", nameof(rejectionReason));

        return new(string.Empty, default, false, rejectionReason);
    }

    internal ScatterGatherReply<TResponse> WithRecipient(string recipientName)
        => new(recipientName, Response, Accepted, RejectionReason);
}

/// <summary>Aggregated scatter-gather dispatch result.</summary>
public sealed class ScatterGatherResult<TResponse, TResult>
{
    private ScatterGatherResult(string name, IReadOnlyList<ScatterGatherReply<TResponse>> replies, TResult? result, bool succeeded, string? rejectionReason)
        => (Name, Replies, Result, Succeeded, RejectionReason) = (name, replies, result, succeeded, rejectionReason);

    public string Name { get; }

    public IReadOnlyList<ScatterGatherReply<TResponse>> Replies { get; }

    public TResult? Result { get; }

    public bool Succeeded { get; }

    public string? RejectionReason { get; }

    internal static ScatterGatherResult<TResponse, TResult> Success(string name, IReadOnlyList<ScatterGatherReply<TResponse>> replies, TResult result)
        => new(name, replies, result, true, null);

    internal static ScatterGatherResult<TResponse, TResult> Rejected(string name, IReadOnlyList<ScatterGatherReply<TResponse>> replies, string rejectionReason)
        => new(name, replies, default, false, rejectionReason);
}
