namespace PatternKit.Messaging.Mailboxes;

/// <summary>
/// In-process mailbox that serializes asynchronous message handling through a single consumer.
/// </summary>
/// <typeparam name="TPayload">The payload type accepted by the mailbox.</typeparam>
public sealed class Mailbox<TPayload> : IDisposable
{
    /// <summary>Async message handler used by a mailbox.</summary>
    public delegate ValueTask MessageHandler(
        Message<TPayload> message,
        MessageContext context,
        CancellationToken cancellationToken);

    /// <summary>Async error handler used when mailbox message handling fails.</summary>
    public delegate ValueTask ErrorHandler(
        Exception exception,
        Message<TPayload> message,
        MessageContext context,
        CancellationToken cancellationToken);

    private readonly object _gate = new();
    private readonly Queue<Envelope> _queue = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly SemaphoreSlim? _waitSlots;
    private readonly MessageHandler _handler;
    private readonly ErrorHandler? _errorHandler;
    private readonly Action<MailboxEvent>? _eventSink;
    private readonly int? _capacity;
    private readonly MailboxBackpressurePolicy _backpressurePolicy;
    private readonly MailboxErrorPolicy _errorPolicy;
    private CancellationTokenSource? _stopSource;
    private Task? _pump;
    private bool _accepting;
    private bool _disposed;
    private long _nextSequence;

    private Mailbox(
        MessageHandler handler,
        int? capacity,
        MailboxBackpressurePolicy backpressurePolicy,
        MailboxErrorPolicy errorPolicy,
        ErrorHandler? errorHandler,
        Action<MailboxEvent>? eventSink)
    {
        _handler = handler;
        _capacity = capacity;
        _backpressurePolicy = backpressurePolicy;
        _errorPolicy = errorPolicy;
        _errorHandler = errorHandler;
        _eventSink = eventSink;
        _waitSlots = capacity is null || backpressurePolicy != MailboxBackpressurePolicy.Wait
            ? null
            : new SemaphoreSlim(capacity.Value, capacity.Value);
    }

    /// <summary>The configured bounded capacity, or null for an unbounded mailbox.</summary>
    public int? Capacity => _capacity;

    /// <summary>The current queued message count.</summary>
    public int QueuedCount
    {
        get
        {
            lock (_gate)
                return _queue.Count;
        }
    }

    /// <summary>Gets whether the mailbox is accepting posts.</summary>
    public bool IsAccepting
    {
        get
        {
            lock (_gate)
                return _accepting;
        }
    }

    /// <summary>Creates a mailbox builder with the supplied handler.</summary>
    public static Builder Create(MessageHandler handler) => new(handler);

    /// <summary>Starts the mailbox single-consumer pump.</summary>
    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        lock (_gate)
        {
            if (_pump is { IsCompleted: false })
                return default;

            cancellationToken.ThrowIfCancellationRequested();
            _stopSource?.Dispose();
            _stopSource = new CancellationTokenSource();
            _accepting = true;
            _pump = Task.Run(ProcessAsync);
        }

        Emit(MailboxEventKind.Started, 0);
        return default;
    }

    /// <summary>
    /// Posts a message to the mailbox.
    /// </summary>
    public async ValueTask<MailboxPostResult> PostAsync(
        Message<TPayload> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        ThrowIfDisposed();

        if (_waitSlots is not null)
            return await PostWithWaitAsync(message, context, cancellationToken).ConfigureAwait(false);

        return PostWithoutWait(message, context, cancellationToken);
    }

    /// <summary>
    /// Stops the mailbox and optionally drains queued messages before completing.
    /// </summary>
    public async ValueTask StopAsync(bool drain = true, CancellationToken cancellationToken = default)
    {
        Task? pump;
        CancellationTokenSource? stopSource;

        lock (_gate)
        {
            _accepting = false;
            pump = _pump;
            stopSource = _stopSource;

            if (!drain)
            {
                while (_queue.Count > 0)
                {
                    var dropped = _queue.Dequeue();
                    CompleteDropped(dropped, "mailbox-stopped");
                }

                stopSource?.Cancel();
            }
        }

        _available.Release();

        if (pump is not null)
            await WaitForPumpAsync(pump, cancellationToken).ConfigureAwait(false);

        stopSource?.Dispose();
        lock (_gate)
        {
            if (ReferenceEquals(_stopSource, stopSource))
                _stopSource = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _stopSource?.Cancel();
        _available.Dispose();
        _waitSlots?.Dispose();
        _stopSource?.Dispose();
    }

    private async ValueTask<MailboxPostResult> PostWithWaitAsync(
        Message<TPayload> message,
        MessageContext? context,
        CancellationToken cancellationToken)
    {
        await _waitSlots!.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            lock (_gate)
            {
                if (!_accepting)
                {
                    _waitSlots.Release();
                    var rejected = MailboxPostResult.Rejected("mailbox-not-accepting");
                    Emit(MailboxEventKind.Rejected, 0);
                    return rejected;
                }

                return EnqueueCore(message, context, slotAcquired: true);
            }
        }
        catch
        {
            _waitSlots.Release();
            throw;
        }
    }

    private MailboxPostResult PostWithoutWait(
        Message<TPayload> message,
        MessageContext? context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_accepting)
            {
                Emit(MailboxEventKind.Rejected, 0);
                return MailboxPostResult.Rejected("mailbox-not-accepting");
            }

            if (_capacity is not null && _queue.Count >= _capacity.Value)
                return HandleFullQueue(message, context);

            return EnqueueCore(message, context, slotAcquired: false);
        }
    }

    private MailboxPostResult HandleFullQueue(Message<TPayload> message, MessageContext? context)
    {
        switch (_backpressurePolicy)
        {
            case MailboxBackpressurePolicy.Reject:
                Emit(MailboxEventKind.Rejected, 0);
                return MailboxPostResult.Rejected("mailbox-full");
            case MailboxBackpressurePolicy.DropNewest:
                Emit(MailboxEventKind.Dropped, 0);
                return MailboxPostResult.Dropped("mailbox-full");
            case MailboxBackpressurePolicy.DropOldest:
                var dropped = _queue.Dequeue();
                CompleteDropped(dropped, "mailbox-full");
                return EnqueueCore(message, context, slotAcquired: false);
            default:
                throw new InvalidOperationException($"Unsupported mailbox backpressure policy '{_backpressurePolicy}'.");
        }
    }

    private MailboxPostResult EnqueueCore(Message<TPayload> message, MessageContext? context, bool slotAcquired)
    {
        var sequence = ++_nextSequence;
        _queue.Enqueue(new Envelope(sequence, message, context, slotAcquired));
        _available.Release();
        Emit(MailboxEventKind.Accepted, sequence);
        return MailboxPostResult.AcceptedResult(sequence);
    }

    private async Task ProcessAsync()
    {
        var stopToken = _stopSource!.Token;

        try
        {
            while (true)
            {
                await _available.WaitAsync(stopToken).ConfigureAwait(false);

                Envelope? envelope;
                lock (_gate)
                {
                    if (_queue.Count == 0)
                    {
                        if (!_accepting)
                            break;

                        continue;
                    }

                    envelope = _queue.Dequeue();
                }

                ReleaseSlot(envelope);
                await ProcessEnvelopeAsync(envelope, stopToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stopToken.IsCancellationRequested)
        {
        }
        finally
        {
            Emit(MailboxEventKind.Stopped, 0);
        }
    }

    private async ValueTask ProcessEnvelopeAsync(Envelope envelope, CancellationToken stopToken)
    {
        var context = envelope.Context ?? MessageContext.From(envelope.Message);

        using var linked = CreateLinkedSource(context.CancellationToken, stopToken);
        var processingToken = linked?.Token ?? context.CancellationToken;
        var effectiveContext = processingToken.CanBeCanceled
            ? context.WithCancellation(processingToken)
            : context;

        Emit(MailboxEventKind.ProcessingStarted, envelope.Sequence);

        try
        {
            await _handler(envelope.Message, effectiveContext, processingToken).ConfigureAwait(false);
            Emit(MailboxEventKind.ProcessingCompleted, envelope.Sequence);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !stopToken.IsCancellationRequested)
        {
            Emit(MailboxEventKind.Failed, envelope.Sequence, exception);

            if (_errorHandler is not null)
                await _errorHandler(exception, envelope.Message, effectiveContext, processingToken).ConfigureAwait(false);

            if (_errorPolicy == MailboxErrorPolicy.Stop)
                StopAfterFailure();
        }
    }

    private void StopAfterFailure()
    {
        lock (_gate)
        {
            _accepting = false;
            while (_queue.Count > 0)
            {
                var dropped = _queue.Dequeue();
                CompleteDropped(dropped, "handler-failed");
            }
        }

        _available.Release();
    }

    private void CompleteDropped(Envelope envelope, string reason)
    {
        _ = reason;
        ReleaseSlot(envelope);
        Emit(MailboxEventKind.Dropped, envelope.Sequence);
    }

    private void ReleaseSlot(Envelope envelope)
    {
        if (envelope.SlotAcquired)
            _waitSlots?.Release();
    }

    private static CancellationTokenSource? CreateLinkedSource(CancellationToken contextToken, CancellationToken stopToken)
    {
        if (contextToken.CanBeCanceled && stopToken.CanBeCanceled)
            return CancellationTokenSource.CreateLinkedTokenSource(contextToken, stopToken);

        if (!contextToken.CanBeCanceled && stopToken.CanBeCanceled)
            return CancellationTokenSource.CreateLinkedTokenSource(stopToken);

        return null;
    }

    private static async Task WaitForPumpAsync(Task pump, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            await pump.ConfigureAwait(false);
            return;
        }

        var delay = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        var completed = await Task.WhenAny(pump, delay).ConfigureAwait(false);
        if (completed == delay)
            cancellationToken.ThrowIfCancellationRequested();

        await pump.ConfigureAwait(false);
    }

    private void Emit(MailboxEventKind kind, long sequence, Exception? exception = null)
    {
        var sink = _eventSink;
        if (sink is null)
            return;

        int queued;
        lock (_gate)
            queued = _queue.Count;

        try
        {
            sink(new MailboxEvent(kind, sequence, queued, exception));
        }
        catch
        {
            // Diagnostics hooks must not affect mailbox processing or backpressure state.
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    private sealed class Envelope
    {
        internal Envelope(long sequence, Message<TPayload> message, MessageContext? context, bool slotAcquired)
        {
            Sequence = sequence;
            Message = message;
            Context = context;
            SlotAcquired = slotAcquired;
        }

        internal long Sequence { get; }

        internal Message<TPayload> Message { get; }

        internal MessageContext? Context { get; }

        internal bool SlotAcquired { get; }
    }

    /// <summary>Fluent builder for <see cref="Mailbox{TPayload}"/>.</summary>
    public sealed class Builder
    {
        private readonly MessageHandler _handler;
        private int? _capacity;
        private MailboxBackpressurePolicy _backpressurePolicy = MailboxBackpressurePolicy.Wait;
        private MailboxErrorPolicy _errorPolicy = MailboxErrorPolicy.Stop;
        private ErrorHandler? _errorHandler;
        private Action<MailboxEvent>? _eventSink;

        internal Builder(MessageHandler handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>Configures the mailbox as unbounded.</summary>
        public Builder Unbounded()
        {
            _capacity = null;
            _backpressurePolicy = MailboxBackpressurePolicy.Wait;
            return this;
        }

        /// <summary>Configures a bounded mailbox capacity and backpressure policy.</summary>
        public Builder Bounded(int capacity, MailboxBackpressurePolicy backpressurePolicy = MailboxBackpressurePolicy.Wait)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Mailbox capacity must be greater than zero.");

            _capacity = capacity;
            _backpressurePolicy = backpressurePolicy;
            return this;
        }

        /// <summary>Configures how handler failures affect the mailbox.</summary>
        public Builder OnError(MailboxErrorPolicy policy, ErrorHandler? handler = null)
        {
            _errorPolicy = policy;
            _errorHandler = handler;
            return this;
        }

        /// <summary>Configures a lightweight event sink for metrics or diagnostics adapters.</summary>
        public Builder OnEvent(Action<MailboxEvent> eventSink)
        {
            _eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
            return this;
        }

        /// <summary>Builds an immutable mailbox.</summary>
        public Mailbox<TPayload> Build() => new(
            _handler,
            _capacity,
            _backpressurePolicy,
            _errorPolicy,
            _errorHandler,
            _eventSink);
    }
}
