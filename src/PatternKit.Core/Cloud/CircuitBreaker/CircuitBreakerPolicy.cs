namespace PatternKit.Cloud.CircuitBreaker;

/// <summary>
/// Current state of a circuit breaker policy.
/// </summary>
public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

/// <summary>
/// Snapshot of circuit breaker state at a point in time.
/// </summary>
public sealed class CircuitBreakerSnapshot
{
    public CircuitBreakerSnapshot(CircuitBreakerState state, int failureCount, DateTimeOffset? openedAt, TimeSpan breakDuration)
    {
        State = state;
        FailureCount = failureCount;
        OpenedAt = openedAt;
        BreakDuration = breakDuration;
    }

    public CircuitBreakerState State { get; }
    public int FailureCount { get; }
    public DateTimeOffset? OpenedAt { get; }
    public TimeSpan BreakDuration { get; }
}

/// <summary>
/// Outcome returned by a circuit breaker policy execution.
/// </summary>
public sealed class CircuitBreakerResult<TResult>
{
    public CircuitBreakerResult(TResult? value, bool succeeded, CircuitBreakerState state, int failureCount, bool rejected, Exception? exception)
    {
        Value = value;
        Succeeded = succeeded;
        State = state;
        FailureCount = failureCount;
        Rejected = rejected;
        Exception = exception;
    }

    public TResult? Value { get; }
    public bool Succeeded { get; }
    public CircuitBreakerState State { get; }
    public int FailureCount { get; }
    public bool Rejected { get; }
    public Exception? Exception { get; }

    public static CircuitBreakerResult<TResult> Success(TResult value, CircuitBreakerState state, int failureCount)
        => new(value, true, state, failureCount, false, null);

    public static CircuitBreakerResult<TResult> Failure(TResult? value, CircuitBreakerState state, int failureCount, Exception? exception)
        => new(value, false, state, failureCount, false, exception);

    public static CircuitBreakerResult<TResult> Rejection(CircuitBreakerState state, int failureCount)
        => new(default, false, state, failureCount, true, null);
}

/// <summary>
/// Circuit breaker policy for isolating unstable dependencies.
/// </summary>
public sealed class CircuitBreakerPolicy<TResult>
{
    public delegate bool ResultPredicate(TResult result);
    public delegate bool ExceptionPredicate(Exception exception);

    private readonly object _gate = new();
    private readonly ResultPredicate _shouldHandleResult;
    private readonly ExceptionPredicate _shouldHandleException;
    private readonly Func<DateTimeOffset> _clock;
    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _failureCount;
    private DateTimeOffset? _openedAt;
    private bool _halfOpenProbeInFlight;

    private CircuitBreakerPolicy(
        string name,
        int failureThreshold,
        TimeSpan breakDuration,
        ResultPredicate shouldHandleResult,
        ExceptionPredicate shouldHandleException,
        Func<DateTimeOffset> clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Circuit breaker policy name is required.", nameof(name));
        if (failureThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), failureThreshold, "Circuit breaker failure threshold must be at least one.");
        if (breakDuration < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(breakDuration), breakDuration, "Circuit breaker break duration cannot be negative.");

        Name = name;
        FailureThreshold = failureThreshold;
        BreakDuration = breakDuration;
        _shouldHandleResult = shouldHandleResult ?? throw new ArgumentNullException(nameof(shouldHandleResult));
        _shouldHandleException = shouldHandleException ?? throw new ArgumentNullException(nameof(shouldHandleException));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string Name { get; }
    public int FailureThreshold { get; }
    public TimeSpan BreakDuration { get; }

    public static Builder Create(string name = "circuit-breaker") => new(name);

    public CircuitBreakerSnapshot Snapshot
    {
        get
        {
            lock (_gate)
                return new CircuitBreakerSnapshot(_state, _failureCount, _openedAt, BreakDuration);
        }
    }

    public CircuitBreakerResult<TResult> Execute(Func<TResult> operation)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        var lease = TryEnter();
        if (!lease.Allowed)
            return CircuitBreakerResult<TResult>.Rejection(lease.State, lease.FailureCount);

        try
        {
            var value = operation();
            if (_shouldHandleResult(value))
                return RecordFailure(value, null);

            return RecordSuccess(value);
        }
        catch (Exception ex) when (_shouldHandleException(ex))
        {
            return RecordFailure(default, ex);
        }
        finally
        {
            if (lease.State == CircuitBreakerState.HalfOpen)
                ReleaseHalfOpenProbe();
        }
    }

    public async ValueTask<CircuitBreakerResult<TResult>> ExecuteAsync(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation is null)
            throw new ArgumentNullException(nameof(operation));

        cancellationToken.ThrowIfCancellationRequested();
        var lease = TryEnter();
        if (!lease.Allowed)
            return CircuitBreakerResult<TResult>.Rejection(lease.State, lease.FailureCount);

        try
        {
            var value = await operation(cancellationToken).ConfigureAwait(false);
            if (_shouldHandleResult(value))
                return RecordFailure(value, null);

            return RecordSuccess(value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (_shouldHandleException(ex))
        {
            return RecordFailure(default, ex);
        }
        finally
        {
            if (lease.State == CircuitBreakerState.HalfOpen)
                ReleaseHalfOpenProbe();
        }
    }

    private CircuitBreakerLease TryEnter()
    {
        lock (_gate)
        {
            if (_state == CircuitBreakerState.Open && IsBreakExpired())
            {
                _state = CircuitBreakerState.HalfOpen;
                _halfOpenProbeInFlight = false;
            }

            if (_state == CircuitBreakerState.Open)
                return CircuitBreakerLease.Rejected(_state, _failureCount);

            if (_state == CircuitBreakerState.HalfOpen)
            {
                if (_halfOpenProbeInFlight)
                    return CircuitBreakerLease.Rejected(_state, _failureCount);

                _halfOpenProbeInFlight = true;
            }

            return CircuitBreakerLease.CreateAllowed(_state, _failureCount);
        }
    }

    private bool IsBreakExpired()
        => _openedAt is null || _clock() - _openedAt.Value >= BreakDuration;

    private CircuitBreakerResult<TResult> RecordSuccess(TResult value)
    {
        lock (_gate)
        {
            _state = CircuitBreakerState.Closed;
            _failureCount = 0;
            _openedAt = null;
            return CircuitBreakerResult<TResult>.Success(value, _state, _failureCount);
        }
    }

    private CircuitBreakerResult<TResult> RecordFailure(TResult? value, Exception? exception)
    {
        lock (_gate)
        {
            if (_state == CircuitBreakerState.HalfOpen)
                OpenCircuit();
            else
            {
                _failureCount++;
                if (_failureCount >= FailureThreshold)
                    OpenCircuit();
            }

            return CircuitBreakerResult<TResult>.Failure(value, _state, _failureCount, exception);
        }
    }

    private void OpenCircuit()
    {
        _state = CircuitBreakerState.Open;
        _openedAt = _clock();
        _halfOpenProbeInFlight = false;
    }

    private void ReleaseHalfOpenProbe()
    {
        lock (_gate)
            _halfOpenProbeInFlight = false;
    }

    public sealed class Builder
    {
        private readonly string _name;
        private int _failureThreshold = 3;
        private TimeSpan _breakDuration = TimeSpan.FromSeconds(30);
        private ResultPredicate _shouldHandleResult = static _ => false;
        private ExceptionPredicate _shouldHandleException = static _ => true;
        private Func<DateTimeOffset> _clock = static () => DateTimeOffset.UtcNow;

        internal Builder(string name) => _name = name;

        public Builder WithFailureThreshold(int failureThreshold)
        {
            _failureThreshold = failureThreshold;
            return this;
        }

        public Builder WithBreakDuration(TimeSpan breakDuration)
        {
            _breakDuration = breakDuration;
            return this;
        }

        public Builder HandleResult(ResultPredicate predicate)
        {
            _shouldHandleResult = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        public Builder HandleException(ExceptionPredicate predicate)
        {
            _shouldHandleException = predicate ?? throw new ArgumentNullException(nameof(predicate));
            return this;
        }

        public Builder WithClock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public CircuitBreakerPolicy<TResult> Build()
            => new(_name, _failureThreshold, _breakDuration, _shouldHandleResult, _shouldHandleException, _clock);
    }

    private readonly struct CircuitBreakerLease
    {
        private CircuitBreakerLease(bool allowed, CircuitBreakerState state, int failureCount)
        {
            Allowed = allowed;
            State = state;
            FailureCount = failureCount;
        }

        public bool Allowed { get; }
        public CircuitBreakerState State { get; }
        public int FailureCount { get; }

        public static CircuitBreakerLease CreateAllowed(CircuitBreakerState state, int failureCount) => new(true, state, failureCount);
        public static CircuitBreakerLease Rejected(CircuitBreakerState state, int failureCount) => new(false, state, failureCount);
    }
}
