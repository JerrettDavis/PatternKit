namespace PatternKit.Messaging.Channels;

/// <summary>
/// Determines how a tap failure is handled by <see cref="AsyncWireTap{TPayload}"/>.
/// </summary>
public enum TapErrorPolicy
{
    /// <summary>Swallow the exception; the main flow is unaffected (default).</summary>
    Swallow,

    /// <summary>Log the exception via the configured sink; the main flow is unaffected.</summary>
    Log,

    /// <summary>Re-throw the exception, propagating it to the caller.</summary>
    Propagate,
}

/// <summary>
/// Per-tap execution outcome captured by <see cref="AsyncWireTapResult{TPayload}"/>.
/// </summary>
public sealed class TapResult
{
    private TapResult(string tapName, bool succeeded, Exception? exception)
    {
        TapName = tapName;
        Succeeded = succeeded;
        Exception = exception;
    }

    /// <summary>The name of the tap that produced this result.</summary>
    public string TapName { get; }

    /// <summary>Whether the tap completed without error.</summary>
    public bool Succeeded { get; }

    /// <summary>The exception thrown by the tap, if any.</summary>
    public Exception? Exception { get; }

    internal static TapResult Success(string tapName) => new(tapName, true, null);
    internal static TapResult Failure(string tapName, Exception exception) => new(tapName, false, exception);
}

/// <summary>
/// Result returned by <see cref="AsyncWireTap{TPayload}.PublishAsync"/>.
/// </summary>
public sealed class AsyncWireTapResult<TPayload>
{
    internal AsyncWireTapResult(Message<TPayload> message, string tapName, TapResult[] tapResults)
    {
        Message = message;
        TapName = tapName;
        TapResults = tapResults;
    }

    /// <summary>The unchanged message that was observed.</summary>
    public Message<TPayload> Message { get; }

    /// <summary>The wire-tap name.</summary>
    public string TapName { get; }

    /// <summary>Per-tap execution outcomes.</summary>
    public IReadOnlyList<TapResult> TapResults { get; }
}

/// <summary>
/// Async wire tap that observes messages with named async side-channel handlers, with per-tap error isolation.
/// The main message flow is never disrupted unless the tap policy is <see cref="TapErrorPolicy.Propagate"/>.
/// </summary>
/// <typeparam name="TPayload">The message payload type.</typeparam>
public sealed class AsyncWireTap<TPayload>
{
    /// <summary>Async tap handler delegate.</summary>
    public delegate ValueTask AsyncTapHandler(Message<TPayload> message, MessageContext context, CancellationToken cancellationToken);

    private readonly string _name;
    private readonly Tap[] _taps;

    private AsyncWireTap(string name, Tap[] taps) => (_name, _taps) = (name, taps);

    /// <summary>Creates a new async wire-tap builder.</summary>
    public static Builder Create(string name = "async-wire-tap") => new(name);

    /// <summary>
    /// Publishes <paramref name="message"/> to all taps and returns the unchanged message with per-tap outcomes.
    /// Tap failures are handled according to each tap's configured <see cref="TapErrorPolicy"/>.
    /// </summary>
    public async ValueTask<AsyncWireTapResult<TPayload>> PublishAsync(
        Message<TPayload> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message, cancellationToken);
        var results = new TapResult[_taps.Length];

        for (var i = 0; i < _taps.Length; i++)
        {
            var tap = _taps[i];
            try
            {
                await tap.Handler(message, effectiveContext, cancellationToken).ConfigureAwait(false);
                results[i] = TapResult.Success(tap.Name);
            }
            catch (Exception ex)
            {
                // Re-throw OCE when the caller requested cancellation — tap policy must not swallow it.
                if (ex is OperationCanceledException && cancellationToken.IsCancellationRequested)
                    throw;

                results[i] = TapResult.Failure(tap.Name, ex);
                switch (tap.Policy)
                {
                    case TapErrorPolicy.Log:
                        tap.ErrorSink?.Invoke(ex);
                        break;
                    case TapErrorPolicy.Propagate:
                        throw;
                    case TapErrorPolicy.Swallow:
                    default:
                        break;
                }
            }
        }

        return new AsyncWireTapResult<TPayload>(message, _name, results);
    }

    /// <summary>Fluent builder for <see cref="AsyncWireTap{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Tap> _taps = new(4);
        private TapErrorPolicy _defaultPolicy = TapErrorPolicy.Swallow;
        private Action<Exception>? _defaultErrorSink;

        internal Builder(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Wire tap name cannot be null, empty, or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>Sets the default error policy applied to taps that do not specify their own.</summary>
        public Builder WithDefaultPolicy(TapErrorPolicy policy, Action<Exception>? sink = null)
        {
            _defaultPolicy = policy;
            _defaultErrorSink = sink;
            return this;
        }

        /// <summary>Adds an async tap with the default error policy.</summary>
        public Builder Tap(string name, AsyncTapHandler handler)
            => Tap(name, handler, _defaultPolicy, _defaultErrorSink);

        /// <summary>Adds an async tap with an explicit error policy.</summary>
        public Builder Tap(string name, AsyncTapHandler handler, TapErrorPolicy policy, Action<Exception>? errorSink = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Tap name cannot be null, empty, or whitespace.", nameof(name));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            if (policy == TapErrorPolicy.Log && errorSink is null && _defaultErrorSink is null)
                throw new ArgumentException("An error sink is required when the tap policy is Log.", nameof(errorSink));

            _taps.Add(new Tap(name, handler, policy, errorSink ?? _defaultErrorSink));
            return this;
        }

        /// <summary>Builds an immutable async wire tap.</summary>
        public AsyncWireTap<TPayload> Build()
        {
            if (_taps.Count == 0)
                throw new InvalidOperationException("AsyncWireTap must have at least one tap handler.");

            return new AsyncWireTap<TPayload>(_name, _taps.ToArray());
        }
    }

    private sealed class Tap
    {
        internal Tap(string name, AsyncTapHandler handler, TapErrorPolicy policy, Action<Exception>? errorSink)
            => (Name, Handler, Policy, ErrorSink) = (name, handler, policy, errorSink);

        internal string Name { get; }
        internal AsyncTapHandler Handler { get; }
        internal TapErrorPolicy Policy { get; }
        internal Action<Exception>? ErrorSink { get; }
    }
}
