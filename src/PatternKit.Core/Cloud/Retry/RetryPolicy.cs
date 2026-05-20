namespace PatternKit.Cloud.Retry;

/// <summary>
/// Outcome returned by a retry policy execution.
/// </summary>
public sealed class RetryResult<TResult>
{
    public RetryResult(TResult? value, bool succeeded, int attempts, Exception? exception)
    {
        Value = value;
        Succeeded = succeeded;
        Attempts = attempts;
        Exception = exception;
    }

    public TResult? Value { get; }
    public bool Succeeded { get; }
    public int Attempts { get; }
    public Exception? Exception { get; }

    public static RetryResult<TResult> Success(TResult value, int attempts)
        => new(value, true, attempts, null);

    public static RetryResult<TResult> Failure(TResult? value, int attempts, Exception? exception)
        => new(value, false, attempts, exception);
}

/// <summary>
/// Context passed to retry delay calculations.
/// </summary>
public sealed class RetryDelayContext
{
    public RetryDelayContext(int attempt, TimeSpan previousDelay)
    {
        Attempt = attempt;
        PreviousDelay = previousDelay;
    }

    public int Attempt { get; }
    public TimeSpan PreviousDelay { get; }
}

/// <summary>
/// Retry policy for transient-failure handling.
/// </summary>
public sealed class RetryPolicy<TResult>
{
    public delegate bool ResultPredicate(TResult result);
    public delegate bool ExceptionPredicate(Exception exception);
    public delegate TimeSpan DelayFactory(RetryDelayContext context);

    private readonly ResultPredicate _shouldRetryResult;
    private readonly ExceptionPredicate _shouldRetryException;
    private readonly DelayFactory _nextDelay;
    private readonly Func<TimeSpan, CancellationToken, ValueTask> _delay;

    private RetryPolicy(
        string name,
        int maxAttempts,
        TimeSpan initialDelay,
        ResultPredicate shouldRetryResult,
        ExceptionPredicate shouldRetryException,
        DelayFactory nextDelay,
        Func<TimeSpan, CancellationToken, ValueTask> delay)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Retry policy name is required.", nameof(name));
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Retry policy must allow at least one attempt.");
        if (initialDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(initialDelay), initialDelay, "Initial delay cannot be negative.");

        Name = name;
        MaxAttempts = maxAttempts;
        InitialDelay = initialDelay;
        _shouldRetryResult = shouldRetryResult ?? throw new ArgumentNullException(nameof(shouldRetryResult));
        _shouldRetryException = shouldRetryException ?? throw new ArgumentNullException(nameof(shouldRetryException));
        _nextDelay = nextDelay ?? throw new ArgumentNullException(nameof(nextDelay));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    public string Name { get; }
    public int MaxAttempts { get; }
    public TimeSpan InitialDelay { get; }

    public static Builder Create(string name = "retry") => new(name);

    public RetryResult<TResult> Execute(Func<TResult> operation)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        TResult? lastValue = default;
        Exception? lastException = null;
        var delay = InitialDelay;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var value = operation();
                lastValue = value;
                lastException = null;

                if (!_shouldRetryResult(value))
                    return RetryResult<TResult>.Success(value, attempt);
            }
            catch (Exception ex) when (_shouldRetryException(ex))
            {
                lastException = ex;
            }

            if (attempt == MaxAttempts)
                break;

            if (delay > TimeSpan.Zero)
                Thread.Sleep(delay);

            delay = _nextDelay(new RetryDelayContext(attempt, delay));
        }

        return RetryResult<TResult>.Failure(lastValue, MaxAttempts, lastException);
    }

    public async ValueTask<RetryResult<TResult>> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        TResult? lastValue = default;
        Exception? lastException = null;
        var delay = InitialDelay;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var value = await operation(cancellationToken).ConfigureAwait(false);
                lastValue = value;
                lastException = null;

                if (!_shouldRetryResult(value))
                    return RetryResult<TResult>.Success(value, attempt);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (_shouldRetryException(ex))
            {
                lastException = ex;
            }

            if (attempt == MaxAttempts)
                break;

            if (delay > TimeSpan.Zero)
                await _delay(delay, cancellationToken).ConfigureAwait(false);

            delay = _nextDelay(new RetryDelayContext(attempt, delay));
        }

        return RetryResult<TResult>.Failure(lastValue, MaxAttempts, lastException);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private int _maxAttempts = 3;
        private TimeSpan _initialDelay = TimeSpan.Zero;
        private double _backoffFactor = 1;
        private ResultPredicate _shouldRetryResult = static _ => false;
        private ExceptionPredicate _shouldRetryException = static _ => true;
        private Func<TimeSpan, CancellationToken, ValueTask> _delay = static (delay, ct) => new(Task.Delay(delay, ct));

        internal Builder(string name) => _name = name;

        public Builder WithMaxAttempts(int maxAttempts)
        {
            _maxAttempts = maxAttempts;
            return this;
        }

        public Builder WithInitialDelay(TimeSpan initialDelay)
        {
            _initialDelay = initialDelay;
            return this;
        }

        public Builder WithExponentialBackoff(double factor)
        {
            if (factor < 1)
                throw new ArgumentOutOfRangeException(nameof(factor), factor, "Backoff factor must be at least 1.");

            _backoffFactor = factor;
            return this;
        }

        public Builder HandleResult(ResultPredicate predicate)
        {
            _shouldRetryResult = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        public Builder HandleException(ExceptionPredicate predicate)
        {
            _shouldRetryException = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        public Builder WithDelayProvider(Func<TimeSpan, CancellationToken, ValueTask> delay)
        {
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
            return this;
        }

        public RetryPolicy<TResult> Build()
            => new(
                _name,
                _maxAttempts,
                _initialDelay,
                _shouldRetryResult,
                _shouldRetryException,
                context => TimeSpan.FromTicks((long)Math.Min(long.MaxValue, context.PreviousDelay.Ticks * _backoffFactor)),
                _delay);
    }
}
