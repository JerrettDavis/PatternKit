namespace PatternKit.Messaging.CompetingConsumers;

public sealed class CompetingConsumerResult<TResult>
{
    public CompetingConsumerResult(TResult? value, bool accepted, bool rejected, string? consumerName, int activeConsumers)
    {
        Value = value;
        Accepted = accepted;
        Rejected = rejected;
        ConsumerName = consumerName;
        ActiveConsumers = activeConsumers;
    }

    public TResult? Value { get; }

    public bool Accepted { get; }

    public bool Rejected { get; }

    public string? ConsumerName { get; }

    public int ActiveConsumers { get; }

    public static CompetingConsumerResult<TResult> Success(TResult value, string consumerName, int activeConsumers)
        => new(value, true, false, consumerName, activeConsumers);

    public static CompetingConsumerResult<TResult> Rejection(int activeConsumers)
        => new(default, false, true, null, activeConsumers);
}

public sealed class CompetingConsumerGroup<TMessage, TResult>
{
    private readonly IReadOnlyList<CompetingConsumer<TMessage, TResult>> _consumers;
    private readonly SemaphoreSlim _deliveries;
    private int _nextConsumer;

    private CompetingConsumerGroup(
        string name,
        IReadOnlyList<CompetingConsumer<TMessage, TResult>> consumers,
        int maxConcurrentDeliveries)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Competing consumer group name is required.", nameof(name));
        if (consumers.Count == 0)
            throw new ArgumentException("At least one competing consumer is required.", nameof(consumers));
        if (maxConcurrentDeliveries < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentDeliveries), maxConcurrentDeliveries, "Competing consumer concurrency must be at least one.");

        Name = name;
        MaxConcurrentDeliveries = maxConcurrentDeliveries;
        _consumers = consumers;
        _deliveries = new SemaphoreSlim(maxConcurrentDeliveries, maxConcurrentDeliveries);
    }

    public string Name { get; }

    public int MaxConcurrentDeliveries { get; }

    public int ConsumerCount => _consumers.Count;

    public int ActiveDeliveries => MaxConcurrentDeliveries - _deliveries.CurrentCount;

    public static Builder Create(string name = "competing-consumers") => new(name);

    public async ValueTask<CompetingConsumerResult<TResult>> DispatchAsync(
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _deliveries.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var consumer = NextConsumer();
            var result = await consumer.HandleAsync(message, cancellationToken).ConfigureAwait(false);
            return CompetingConsumerResult<TResult>.Success(result, consumer.Name, ActiveDeliveries);
        }
        finally
        {
            _deliveries.Release();
        }
    }

    public async ValueTask<CompetingConsumerResult<TResult>> TryDispatchAsync(
        TMessage message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!await _deliveries.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return CompetingConsumerResult<TResult>.Rejection(ActiveDeliveries);

        try
        {
            var consumer = NextConsumer();
            var result = await consumer.HandleAsync(message, cancellationToken).ConfigureAwait(false);
            return CompetingConsumerResult<TResult>.Success(result, consumer.Name, ActiveDeliveries);
        }
        finally
        {
            _deliveries.Release();
        }
    }

    private CompetingConsumer<TMessage, TResult> NextConsumer()
    {
        var index = Interlocked.Increment(ref _nextConsumer);
        return _consumers[(index - 1) % _consumers.Count];
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<CompetingConsumer<TMessage, TResult>> _consumers = [];
        private int _maxConcurrentDeliveries = 1;

        internal Builder(string name) => _name = name;

        public Builder WithMaxConcurrentDeliveries(int maxConcurrentDeliveries)
        {
            _maxConcurrentDeliveries = maxConcurrentDeliveries;
            return this;
        }

        public Builder AddConsumer(
            string name,
            Func<TMessage, CancellationToken, ValueTask<TResult>> handler)
        {
            _consumers.Add(new CompetingConsumer<TMessage, TResult>(name, handler));
            return this;
        }

        public CompetingConsumerGroup<TMessage, TResult> Build()
            => new(_name, _consumers.ToArray(), _maxConcurrentDeliveries);
    }
}

public sealed class CompetingConsumer<TMessage, TResult>
{
    public CompetingConsumer(string name, Func<TMessage, CancellationToken, ValueTask<TResult>> handler)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Competing consumer name is required.", nameof(name));

        Name = name;
        HandleAsync = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public string Name { get; }

    public Func<TMessage, CancellationToken, ValueTask<TResult>> HandleAsync { get; }
}
