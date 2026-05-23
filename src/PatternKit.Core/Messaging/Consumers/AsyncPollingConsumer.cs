namespace PatternKit.Messaging.Consumers;

/// <summary>
/// Back-off policy applied when an empty poll is received.
/// </summary>
public enum BackOffPolicy
{
    /// <summary>Wait the same interval after an empty poll.</summary>
    Constant,

    /// <summary>Double the wait after each consecutive empty poll, up to the configured cap.</summary>
    Exponential,
}

/// <summary>
/// Self-driving async polling consumer with configurable interval, jitter, and empty-poll back-off.
/// </summary>
/// <typeparam name="TPayload">The payload type produced by the source.</typeparam>
public sealed class AsyncPollingConsumer<TPayload>
{
    /// <summary>Async poll source delegate. Return <see langword="null"/> to indicate an empty poll.</summary>
    public delegate ValueTask<Message<TPayload>?> AsyncPollSource(MessageContext context, CancellationToken cancellationToken);

    /// <summary>Async message handler delegate invoked for each non-empty poll result.</summary>
    public delegate ValueTask AsyncMessageHandler(Message<TPayload> message, MessageContext context, CancellationToken cancellationToken);

    private readonly string _name;
    private readonly AsyncPollSource _source;
    private readonly TimeSpan _interval;
    private readonly TimeSpan _jitter;
    private readonly BackOffPolicy _backOffPolicy;
    private readonly TimeSpan _backOffCap;

    private AsyncPollingConsumer(
        string name,
        AsyncPollSource source,
        TimeSpan interval,
        TimeSpan jitter,
        BackOffPolicy backOffPolicy,
        TimeSpan backOffCap)
    {
        _name = name;
        _source = source;
        _interval = interval;
        _jitter = jitter;
        _backOffPolicy = backOffPolicy;
        _backOffCap = backOffCap;
    }

    /// <summary>The consumer name.</summary>
    public string Name => _name;

    /// <summary>Creates a new async polling consumer builder.</summary>
    public static Builder Create(string name = "async-polling-consumer") => new(name);

    /// <summary>
    /// Runs the polling loop until <paramref name="cancellationToken"/> is cancelled.
    /// Polls the source on the configured interval, invokes <paramref name="handler"/> for each message,
    /// and applies empty-poll back-off.
    /// </summary>
    public async ValueTask RunAsync(
        AsyncMessageHandler handler,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var effectiveContext = context ?? MessageContext.Empty;
        var rng = new Random();
        var consecutiveEmpty = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            Message<TPayload>? message = null;
            try
            {
                message = await _source(effectiveContext, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (message is not null)
            {
                consecutiveEmpty = 0;
                try
                {
                    await handler(message, effectiveContext, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
            else
            {
                consecutiveEmpty++;
            }

            var delay = ComputeDelay(consecutiveEmpty, rng);
            try
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Executes a single poll cycle and returns the item received, or <see langword="null"/>
    /// if the source returned no message.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="RunAsync"/>, this method does not enter a loop, does not sleep,
    /// does not apply interval, jitter, or back-off, and does not invoke any registered
    /// run-loop handler. It is intended for caller-driven polling where the caller owns
    /// the loop (e.g., a workflow-framework step-based polling integration).
    /// </remarks>
    /// <param name="ct">Cancellation token propagated directly to the poll source.</param>
    /// <returns>
    /// The message returned by the source, or <see langword="null"/> when the source
    /// returned an empty poll.
    /// </returns>
    public ValueTask<Message<TPayload>?> PollOnceAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return _source(MessageContext.Empty, ct);
    }

    private TimeSpan ComputeDelay(int consecutiveEmpty, Random rng)
    {
        TimeSpan baseDelay;
        if (consecutiveEmpty == 0)
        {
            baseDelay = _interval;
        }
        else
        {
            baseDelay = _backOffPolicy switch
            {
                BackOffPolicy.Exponential => Min(
                    TimeSpan.FromMilliseconds(_interval.TotalMilliseconds * Math.Pow(2, consecutiveEmpty - 1)),
                    _backOffCap),
                _ => _interval,
            };
        }

        if (_jitter > TimeSpan.Zero)
        {
            var jitterMs = rng.NextDouble() * _jitter.TotalMilliseconds;
            baseDelay = baseDelay.Add(TimeSpan.FromMilliseconds(jitterMs));
        }

        return baseDelay;
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

    /// <summary>Fluent builder for <see cref="AsyncPollingConsumer{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private AsyncPollSource? _source;
        private TimeSpan _interval = TimeSpan.FromSeconds(5);
        private TimeSpan _jitter = TimeSpan.Zero;
        private BackOffPolicy _backOffPolicy = BackOffPolicy.Constant;
        private TimeSpan _backOffCap = TimeSpan.FromMinutes(1);

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Polling consumer name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Sets the poll source delegate.</summary>
        public Builder WithSource(AsyncPollSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            return this;
        }

        /// <summary>Sets the polling interval (default: 5 seconds).</summary>
        public Builder WithInterval(TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "Polling interval must be positive.");

            _interval = interval;
            return this;
        }

        /// <summary>Sets the maximum random jitter added to each delay (default: none).</summary>
        public Builder WithJitter(TimeSpan jitter)
        {
            if (jitter < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(jitter), "Jitter must be non-negative.");

            _jitter = jitter;
            return this;
        }

        /// <summary>
        /// Sets the back-off policy applied on empty polls (default: <see cref="BackOffPolicy.Constant"/>).
        /// </summary>
        public Builder OnEmpty(BackOffPolicy policy, TimeSpan? cap = null)
        {
            _backOffPolicy = policy;
            if (cap.HasValue)
            {
                if (cap.Value <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(cap), "Back-off cap must be positive.");
                _backOffCap = cap.Value;
            }
            return this;
        }

        /// <summary>Builds an immutable async polling consumer.</summary>
        public AsyncPollingConsumer<TPayload> Build()
        {
            if (_source is null)
                throw new InvalidOperationException("AsyncPollingConsumer requires a poll source.");

            return new AsyncPollingConsumer<TPayload>(_name, _source, _interval, _jitter, _backOffPolicy, _backOffCap);
        }
    }
}
