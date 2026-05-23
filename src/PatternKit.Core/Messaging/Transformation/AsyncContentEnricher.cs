namespace PatternKit.Messaging.Transformation;

/// <summary>
/// Determines how an enrichment step failure is handled by <see cref="AsyncContentEnricher{TPayload}"/>.
/// </summary>
public enum EnrichmentErrorPolicy
{
    /// <summary>Throw the exception; the enrichment pipeline aborts.</summary>
    Throw,

    /// <summary>Skip this enrichment step; the payload is unchanged for this step.</summary>
    Skip,

    /// <summary>Use a configured default value for this enrichment step.</summary>
    UseDefault,
}

/// <summary>Per-enrichment-step outcome captured by <see cref="AsyncContentEnricherResult{TPayload}"/>.</summary>
public sealed class EnrichmentStepResult
{
    private EnrichmentStepResult(string stepName, bool applied, bool skipped, Exception? exception)
    {
        StepName = stepName;
        Applied = applied;
        Skipped = skipped;
        Exception = exception;
    }

    /// <summary>The enrichment step name.</summary>
    public string StepName { get; }

    /// <summary>Whether the step was applied successfully.</summary>
    public bool Applied { get; }

    /// <summary>Whether the step was skipped due to an error or policy.</summary>
    public bool Skipped { get; }

    /// <summary>The exception thrown by the step, if any.</summary>
    public Exception? Exception { get; }

    internal static EnrichmentStepResult CreateApplied(string name) => new(name, true, false, null);
    internal static EnrichmentStepResult CreateSkipped(string name, Exception? ex) => new(name, false, true, ex);
}

/// <summary>Result returned by <see cref="AsyncContentEnricher{TPayload}.EnrichAsync"/>.</summary>
public sealed class AsyncContentEnricherResult<TPayload>
{
    internal AsyncContentEnricherResult(Message<TPayload> message, string enricherName, EnrichmentStepResult[] stepResults)
    {
        Message = message;
        EnricherName = enricherName;
        StepResults = stepResults;
    }

    /// <summary>The enriched message.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>The enricher name.</summary>
    public string EnricherName { get; }

    /// <summary>Per-step audit trail.</summary>
    public IReadOnlyList<EnrichmentStepResult> StepResults { get; }
}

/// <summary>
/// Augments a message payload with computed or fetched data without changing the payload type.
/// Each enrichment step is executed in registration order with per-step error isolation.
/// </summary>
/// <typeparam name="TPayload">The payload type to enrich.</typeparam>
public sealed class AsyncContentEnricher<TPayload>
{
    /// <summary>Async enrichment step delegate. Returns the enriched payload copy.</summary>
    public delegate ValueTask<TPayload> AsyncEnrichStep(TPayload payload, MessageContext context, CancellationToken cancellationToken);

    private readonly string _name;
    private readonly Step[] _steps;

    private AsyncContentEnricher(string name, Step[] steps) => (_name, _steps) = (name, steps);

    /// <summary>Creates a new content enricher builder.</summary>
    public static Builder Create(string name = "content-enricher") => new(name);

    /// <summary>
    /// Applies each enrichment step in order, returning the enriched message and a per-step audit trail.
    /// </summary>
    public async ValueTask<AsyncContentEnricherResult<TPayload>> EnrichAsync(
        Message<TPayload> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message, cancellationToken);
        var stepResults = new EnrichmentStepResult[_steps.Length];
        var currentPayload = message.Payload;

        for (var i = 0; i < _steps.Length; i++)
        {
            var step = _steps[i];
            try
            {
                currentPayload = await step.Handler(currentPayload, effectiveContext, cancellationToken).ConfigureAwait(false);
                stepResults[i] = EnrichmentStepResult.CreateApplied(step.Name);
            }
            catch (Exception ex)
            {
                // Re-throw OCE when the caller requested cancellation — enrichment policy must not swallow it.
                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    throw;

                switch (step.Policy)
                {
                    case EnrichmentErrorPolicy.Throw:
                        throw;
                    case EnrichmentErrorPolicy.UseDefault:
                        if (step.DefaultFactory is not null)
                            currentPayload = step.DefaultFactory(currentPayload);
                        stepResults[i] = EnrichmentStepResult.CreateSkipped(step.Name, ex);
                        break;
                    case EnrichmentErrorPolicy.Skip:
                    default:
                        stepResults[i] = EnrichmentStepResult.CreateSkipped(step.Name, ex);
                        break;
                }
            }
        }

        var enrichedMessage = new Message<TPayload>(currentPayload, message.Headers);
        return new AsyncContentEnricherResult<TPayload>(enrichedMessage, _name, stepResults);
    }

    /// <summary>Fluent builder for <see cref="AsyncContentEnricher{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Step> _steps = new(4);
        private EnrichmentErrorPolicy _defaultPolicy = EnrichmentErrorPolicy.Throw;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Content enricher name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Sets the default error policy for steps that do not specify their own.</summary>
        public Builder WithDefaultPolicy(EnrichmentErrorPolicy policy)
        {
            _defaultPolicy = policy;
            return this;
        }

        /// <summary>Adds an enrichment step with the default policy.</summary>
        public Builder Enrich(string name, AsyncEnrichStep handler)
            => Enrich(name, handler, _defaultPolicy);

        /// <summary>Adds an enrichment step with an explicit policy.</summary>
        public Builder Enrich(string name, AsyncEnrichStep handler, EnrichmentErrorPolicy policy, Func<TPayload, TPayload>? defaultFactory = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Enrichment step name cannot be null, empty, or whitespace.", nameof(name));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));
            if (policy == EnrichmentErrorPolicy.UseDefault && defaultFactory is null)
                throw new ArgumentException(
                    $"A non-null {nameof(defaultFactory)} is required when policy is {nameof(EnrichmentErrorPolicy.UseDefault)}.",
                    nameof(defaultFactory));

            _steps.Add(new Step(name, handler, policy, defaultFactory));
            return this;
        }

        /// <summary>Builds an immutable content enricher.</summary>
        public AsyncContentEnricher<TPayload> Build()
        {
            if (_steps.Count == 0)
                throw new InvalidOperationException("AsyncContentEnricher must have at least one enrichment step.");

            return new AsyncContentEnricher<TPayload>(_name, _steps.ToArray());
        }
    }

    private sealed class Step
    {
        internal Step(string name, AsyncEnrichStep handler, EnrichmentErrorPolicy policy, Func<TPayload, TPayload>? defaultFactory)
            => (Name, Handler, Policy, DefaultFactory) = (name, handler, policy, defaultFactory);

        internal string Name { get; }
        internal AsyncEnrichStep Handler { get; }
        internal EnrichmentErrorPolicy Policy { get; }
        internal Func<TPayload, TPayload>? DefaultFactory { get; }
    }
}
