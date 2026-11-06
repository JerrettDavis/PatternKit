namespace PatternKit.Messaging.Routing;

/// <summary>
/// Async in-process Routing Slip pattern that executes named steps in order over a message.
/// </summary>
public sealed class AsyncRoutingSlip<TPayload>
{
    /// <summary>Async step handler used by a routing slip.</summary>
    public delegate ValueTask<Message<TPayload>> StepHandler(
        Message<TPayload> message,
        MessageContext context,
        CancellationToken cancellationToken);

    private readonly Step[] _steps;

    private AsyncRoutingSlip(Step[] steps) => _steps = steps;

    /// <summary>
    /// Executes every configured async step and returns the final message with routing progress headers.
    /// </summary>
    public async ValueTask<RoutingSlipResult<TPayload>> ExecuteAsync(
        Message<TPayload> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var itinerary = _steps.Select(step => step.Name).ToArray();
        var current = InitializeHeaders(message, itinerary);
        var effectiveContext = CreateContext(current, context, cancellationToken);
        var completed = new List<string>(_steps.Length);

        for (var i = 0; i < _steps.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = _steps[i];
            current = UpdateProgress(current, i);
            current = await step.Handler(current, effectiveContext.WithHeaders(current.Headers), cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Routing slip step '{step.Name}' returned null.");
            completed.Add(step.Name);
        }

        current = current.Enrich(headers => headers
            .With(MessageHeaderNames.RoutingSlipIndex, _steps.Length)
            .With(MessageHeaderNames.RoutingSlipCompleted, completed.ToArray()));

        return new RoutingSlipResult<TPayload>(current, completed);
    }

    /// <summary>Creates a new async routing slip builder.</summary>
    public static Builder Create() => new();

    private static MessageContext CreateContext(
        Message<TPayload> message,
        MessageContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null)
            return MessageContext.From(message, cancellationToken);

        return cancellationToken.CanBeCanceled
            ? context.WithCancellation(cancellationToken)
            : context;
    }

    private static Message<TPayload> InitializeHeaders(Message<TPayload> message, IReadOnlyList<string> itinerary)
        => message.Enrich(headers => headers
            .With(MessageHeaderNames.RoutingSlip, itinerary.ToArray())
            .With(MessageHeaderNames.RoutingSlipIndex, 0)
            .Without(MessageHeaderNames.RoutingSlipCompleted));

    private static Message<TPayload> UpdateProgress(Message<TPayload> message, int index)
        => message.WithHeader(MessageHeaderNames.RoutingSlipIndex, index);

    private sealed class Step
    {
        internal Step(string name, StepHandler handler) => (Name, Handler) = (name, handler);

        internal string Name { get; }

        internal StepHandler Handler { get; }
    }

    /// <summary>Fluent builder for <see cref="AsyncRoutingSlip{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly List<Step> _steps = new();

        /// <summary>Adds a named async step to the itinerary.</summary>
        public Builder Step(string name, StepHandler handler)
        {
            ValidateName(name);
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            _steps.Add(new Step(name, handler));
            return this;
        }

        /// <summary>Builds an immutable async routing slip.</summary>
        public AsyncRoutingSlip<TPayload> Build() => new(_steps.ToArray());

        private static void ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Routing slip step name cannot be null, empty, or whitespace.", nameof(name));
        }
    }
}
