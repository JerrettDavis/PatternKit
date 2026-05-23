namespace PatternKit.Messaging.Routing;

/// <summary>
/// Defines when <see cref="AsyncScatterGather{TRequest,TResponse,TResult}"/> considers the fan-out complete.
/// </summary>
public abstract class CompletionStrategy
{
    private CompletionStrategy() { }

    /// <summary>Wait for all recipients to respond.</summary>
    public static CompletionStrategy All { get; } = new AllStrategy();

    /// <summary>Wait until at least <paramref name="n"/> recipients have responded.</summary>
    public static CompletionStrategy Quorum(int n) => new QuorumStrategy(n);

    /// <summary>Wait until at least <paramref name="n"/> successful responses are received.</summary>
    public static CompletionStrategy FirstN(int n) => new FirstNStrategy(n);

    /// <summary>Wait up to <paramref name="timeout"/>; use whatever responses arrived by then.</summary>
    public static CompletionStrategy Timeout(TimeSpan timeout) => new TimeoutStrategy(timeout);

    /// <summary>Wait for all responses, but stop waiting after <paramref name="timeout"/>.</summary>
    public static CompletionStrategy AllOrTimeout(TimeSpan timeout) => new AllOrTimeoutStrategy(timeout);

    internal abstract Task<bool> ShouldCompleteAsync(Task<ResponseEnvelope<object?>[]> whenAll, int recipientCount, TimeSpan? overallTimeout, CancellationToken ct);
    internal abstract TimeSpan? GetTimeout();

    private sealed class AllStrategy : CompletionStrategy
    {
        internal override Task<bool> ShouldCompleteAsync(Task<ResponseEnvelope<object?>[]> whenAll, int recipientCount, TimeSpan? overallTimeout, CancellationToken ct)
            => Task.FromResult(true);

        internal override TimeSpan? GetTimeout() => null;
    }

    private sealed class QuorumStrategy : CompletionStrategy
    {
        private readonly int _quorum;
        internal QuorumStrategy(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Quorum must be positive.");
            _quorum = n;
        }

        internal override Task<bool> ShouldCompleteAsync(Task<ResponseEnvelope<object?>[]> whenAll, int recipientCount, TimeSpan? overallTimeout, CancellationToken ct)
            => Task.FromResult(true); // handled externally via tracking count

        internal override TimeSpan? GetTimeout() => null;

        internal int Required => _quorum;
    }

    private sealed class FirstNStrategy : CompletionStrategy
    {
        private readonly int _n;
        internal FirstNStrategy(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "FirstN must be positive.");
            _n = n;
        }

        internal override Task<bool> ShouldCompleteAsync(Task<ResponseEnvelope<object?>[]> whenAll, int recipientCount, TimeSpan? overallTimeout, CancellationToken ct)
            => Task.FromResult(true);

        internal override TimeSpan? GetTimeout() => null;

        internal int Required => _n;
    }

    private sealed class TimeoutStrategy : CompletionStrategy
    {
        private readonly TimeSpan _timeout;
        internal TimeoutStrategy(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            _timeout = timeout;
        }

        internal override Task<bool> ShouldCompleteAsync(Task<ResponseEnvelope<object?>[]> whenAll, int recipientCount, TimeSpan? overallTimeout, CancellationToken ct)
            => Task.FromResult(true);

        internal override TimeSpan? GetTimeout() => _timeout;
    }

    private sealed class AllOrTimeoutStrategy : CompletionStrategy
    {
        private readonly TimeSpan _timeout;
        internal AllOrTimeoutStrategy(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be positive.");
            _timeout = timeout;
        }

        internal override Task<bool> ShouldCompleteAsync(Task<ResponseEnvelope<object?>[]> whenAll, int recipientCount, TimeSpan? overallTimeout, CancellationToken ct)
            => Task.FromResult(true);

        internal override TimeSpan? GetTimeout() => _timeout;

        internal bool RequireAll => true;
    }

    internal bool IsQuorum(out int n) { if (this is QuorumStrategy q) { n = q.Required; return true; } n = 0; return false; }
    internal bool IsFirstN(out int n) { if (this is FirstNStrategy f) { n = f.Required; return true; } n = 0; return false; }
    internal bool IsAllOrTimeout(out TimeSpan timeout) { if (this is AllOrTimeoutStrategy a) { timeout = a.GetTimeout()!.Value; return true; } timeout = default; return false; }
}

/// <summary>Envelope wrapping one recipient's response.</summary>
public sealed class ResponseEnvelope<TResponse>
{
    private ResponseEnvelope(string recipientName, TResponse? response, bool succeeded, Exception? exception)
    {
        RecipientName = recipientName;
        Response = response;
        Succeeded = succeeded;
        Exception = exception;
    }

    /// <summary>The recipient name.</summary>
    public string RecipientName { get; }

    /// <summary>The response value, when successful.</summary>
    public TResponse? Response { get; }

    /// <summary>Whether the recipient completed without error.</summary>
    public bool Succeeded { get; }

    /// <summary>The exception thrown by the recipient, if any.</summary>
    public Exception? Exception { get; }

    internal static ResponseEnvelope<TResponse> Success(string name, TResponse response) => new(name, response, true, null);
    internal static ResponseEnvelope<TResponse> Failure(string name, Exception ex) => new(name, default, false, ex);
}

/// <summary>
/// Async scatter-gather with pluggable completion strategy, per-branch error isolation,
/// and concurrent fan-out via <see cref="Task.WhenAll"/>.
/// </summary>
/// <typeparam name="TRequest">The fan-out request type.</typeparam>
/// <typeparam name="TResponse">The per-recipient response type.</typeparam>
/// <typeparam name="TResult">The aggregated result type.</typeparam>
public sealed class AsyncScatterGather<TRequest, TResponse, TResult>
{
    /// <summary>Async recipient delegate.</summary>
    public delegate ValueTask<TResponse> AsyncRecipientHandler(
        Message<TRequest> message,
        MessageContext context,
        CancellationToken cancellationToken);

    /// <summary>Aggregation delegate receiving all envelopes that completed before the strategy fired.</summary>
    public delegate TResult ResponseAggregator(
        IReadOnlyList<ResponseEnvelope<TResponse>> envelopes,
        Message<TRequest> request,
        MessageContext context);

    private readonly string _name;
    private readonly Recipient[] _recipients;
    private readonly CompletionStrategy _strategy;
    private readonly ResponseAggregator _aggregator;

    private AsyncScatterGather(
        string name,
        Recipient[] recipients,
        CompletionStrategy strategy,
        ResponseAggregator aggregator)
    {
        _name = name;
        _recipients = recipients;
        _strategy = strategy;
        _aggregator = aggregator;
    }

    /// <summary>Creates a new async scatter-gather builder.</summary>
    public static Builder Create(string name = "async-scatter-gather") => new(name);

    /// <summary>
    /// Fans out <paramref name="message"/> to all recipients concurrently, waits per strategy,
    /// and aggregates the results using concurrent fan-out.
    /// </summary>
    public async ValueTask<AsyncScatterGatherResult<TResponse, TResult>> DispatchAsync(
        Message<TRequest> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message, cancellationToken);
        var timeout = _strategy.GetTimeout();

        using var cts = timeout.HasValue
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;

        if (timeout.HasValue && cts != null)
            cts.CancelAfter(timeout.Value);

        var linkedToken = cts?.Token ?? cancellationToken;

        // Track completed envelopes in a thread-safe list
        var envelopes = new System.Collections.Concurrent.ConcurrentBag<ResponseEnvelope<TResponse>>();

        // Check if we need early-exit on FirstN or Quorum
        _strategy.IsFirstN(out var firstN);
        _strategy.IsQuorum(out var quorum);
        var earlyExitCount = firstN > 0 ? firstN : (quorum > 0 ? quorum : 0);

        using var earlyCts = earlyExitCount > 0
            ? (cts != null
                ? CancellationTokenSource.CreateLinkedTokenSource(cts.Token)
                : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            : null;

        var earlyCounter = new EarlyExitCounter(earlyExitCount, earlyCts);

        var tasks = _recipients.Select(recipient => RunRecipientAsync(
            recipient, message, effectiveContext, earlyCts?.Token ?? linkedToken,
            envelopes, earlyCounter)).ToArray();

        try
        {
            if (timeout.HasValue)
            {
                var timeoutTask = Task.Delay(timeout.Value, cancellationToken);
                var whenAll = Task.WhenAll(tasks);
                await Task.WhenAny(whenAll, timeoutTask).ConfigureAwait(false);
            }
            else
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
        catch
        {
            // Individual task errors are captured in envelopes; swallow aggregate exception
        }

        var collected = envelopes.ToArray();
        if (collected.Length == 0)
            return AsyncScatterGatherResult<TResponse, TResult>.Rejected(_name, collected, "No scatter-gather recipients produced a result.");

        var aggregated = _aggregator(collected, message, effectiveContext);
        return AsyncScatterGatherResult<TResponse, TResult>.Success(_name, collected, aggregated);
    }

    private static async Task RunRecipientAsync(
        Recipient recipient,
        Message<TRequest> message,
        MessageContext context,
        CancellationToken ct,
        System.Collections.Concurrent.ConcurrentBag<ResponseEnvelope<TResponse>> envelopes,
        EarlyExitCounter earlyCounter)
    {
        try
        {
            var response = await recipient.Handler(message, context, ct).ConfigureAwait(false);
            envelopes.Add(ResponseEnvelope<TResponse>.Success(recipient.Name, response));
            earlyCounter.RecordSuccess();
        }
        catch (OperationCanceledException)
        {
            // Cancellation from early-exit or timeout — not a recipient error
        }
        catch (Exception ex)
        {
            envelopes.Add(ResponseEnvelope<TResponse>.Failure(recipient.Name, ex));
        }
    }

    private sealed class EarlyExitCounter
    {
        private readonly int _required;
        private readonly CancellationTokenSource? _cts;
        private int _count;

        internal EarlyExitCounter(int required, CancellationTokenSource? cts)
            => (_required, _cts) = (required, cts);

        internal void RecordSuccess()
        {
            if (_required <= 0 || _cts is null)
                return;

            var count = System.Threading.Interlocked.Increment(ref _count);
            if (count >= _required)
            {
                try { _cts.Cancel(); } catch (ObjectDisposedException) { }
            }
        }
    }

    /// <summary>Fluent builder for <see cref="AsyncScatterGather{TRequest,TResponse,TResult}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Recipient> _recipients = [];
        private CompletionStrategy _strategy = CompletionStrategy.All;
        private ResponseAggregator? _aggregator;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Scatter-gather name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Adds a named async recipient.</summary>
        public Builder Recipient(string name, AsyncRecipientHandler handler)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Recipient name cannot be null, empty, or whitespace.", nameof(name));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            _recipients.Add(new Recipient(name, handler));
            return this;
        }

        /// <summary>Sets the completion strategy (default: <see cref="CompletionStrategy.All"/>).</summary>
        public Builder CompleteWith(CompletionStrategy strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            return this;
        }

        /// <summary>Sets the aggregation delegate.</summary>
        public Builder WithAggregator(ResponseAggregator aggregator)
        {
            _aggregator = aggregator ?? throw new ArgumentNullException(nameof(aggregator));
            return this;
        }

        /// <summary>Builds an immutable async scatter-gather.</summary>
        public AsyncScatterGather<TRequest, TResponse, TResult> Build()
        {
            if (_recipients.Count == 0)
                throw new InvalidOperationException("AsyncScatterGather requires at least one recipient.");
            if (_aggregator is null)
                throw new InvalidOperationException("AsyncScatterGather requires an aggregator.");

            return new AsyncScatterGather<TRequest, TResponse, TResult>(_name, _recipients.ToArray(), _strategy, _aggregator);
        }
    }

    private sealed class Recipient
    {
        internal Recipient(string name, AsyncRecipientHandler handler) => (Name, Handler) = (name, handler);
        internal string Name { get; }
        internal AsyncRecipientHandler Handler { get; }
    }
}

/// <summary>Aggregated async scatter-gather result.</summary>
public sealed class AsyncScatterGatherResult<TResponse, TResult>
{
    private AsyncScatterGatherResult(string name, ResponseEnvelope<TResponse>[] envelopes, TResult? result, bool succeeded, string? rejectionReason)
    {
        Name = name;
        Envelopes = envelopes;
        Result = result;
        Succeeded = succeeded;
        RejectionReason = rejectionReason;
    }

    /// <summary>The scatter-gather name.</summary>
    public string Name { get; }

    /// <summary>Per-recipient response envelopes.</summary>
    public IReadOnlyList<ResponseEnvelope<TResponse>> Envelopes { get; }

    /// <summary>The aggregated result.</summary>
    public TResult? Result { get; }

    /// <summary>Whether any responses were aggregated.</summary>
    public bool Succeeded { get; }

    /// <summary>Reason for failure when <see cref="Succeeded"/> is false.</summary>
    public string? RejectionReason { get; }

    internal static AsyncScatterGatherResult<TResponse, TResult> Success(string name, ResponseEnvelope<TResponse>[] envelopes, TResult result)
        => new(name, envelopes, result, true, null);

    internal static AsyncScatterGatherResult<TResponse, TResult> Rejected(string name, ResponseEnvelope<TResponse>[] envelopes, string reason)
        => new(name, envelopes, default, false, reason);
}
