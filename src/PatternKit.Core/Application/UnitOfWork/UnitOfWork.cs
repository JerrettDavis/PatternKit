namespace PatternKit.Application.UnitOfWork;

/// <summary>
/// Coordinates ordered operations and compensating rollback actions as one logical unit.
/// </summary>
public sealed class UnitOfWork
{
    private readonly IReadOnlyList<UnitOfWorkStep> _steps;

    private UnitOfWork(IReadOnlyList<UnitOfWorkStep> steps)
    {
        _steps = steps;
    }

    /// <summary>Registered step names in commit order.</summary>
    public IReadOnlyList<string> StepNames => _steps.Select(static step => step.Name).ToArray();

    /// <summary>Creates a unit-of-work builder.</summary>
    public static Builder Create() => new();

    /// <summary>Commits all registered operations in order.</summary>
    public async ValueTask<UnitOfWorkResult> CommitAsync(CancellationToken cancellationToken = default)
    {
        var committed = new List<string>();
        for (var i = 0; i < _steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = _steps[i];
            try
            {
                await step.Commit(cancellationToken).ConfigureAwait(false);
                committed.Add(step.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var rollback = await RollbackCommittedAsync(committed.Count - 1, cancellationToken).ConfigureAwait(false);
                return UnitOfWorkResult.Failed(committed, step.Name, ex, rollback);
            }
        }

        return UnitOfWorkResult.Success(committed);
    }

    /// <summary>Runs all registered rollback actions in reverse order.</summary>
    public async ValueTask<UnitOfWorkRollbackResult> RollbackAsync(CancellationToken cancellationToken = default)
        => await RollbackCommittedAsync(_steps.Count - 1, cancellationToken).ConfigureAwait(false);

    private async ValueTask<UnitOfWorkRollbackResult> RollbackCommittedAsync(int index, CancellationToken cancellationToken)
    {
        var rolledBack = new List<string>();
        var failures = new List<Exception>();
        for (var i = index; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = _steps[i];
            try
            {
                await step.Rollback(cancellationToken).ConfigureAwait(false);
                rolledBack.Add(step.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(ex);
            }
        }

        return new UnitOfWorkRollbackResult(rolledBack, failures);
    }

    /// <summary>Fluent unit-of-work builder.</summary>
    public sealed class Builder
    {
        private readonly List<UnitOfWorkStep> _steps = new();

        /// <summary>Adds a named commit operation with an optional compensating rollback action.</summary>
        public Builder Enlist(
            string name,
            Func<CancellationToken, ValueTask> commit,
            Func<CancellationToken, ValueTask>? rollback = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Unit-of-work step name is required.", nameof(name));
            if (_steps.Any(step => string.Equals(step.Name, name, StringComparison.Ordinal)))
                throw new ArgumentException($"Unit-of-work step '{name}' is already registered.", nameof(name));

            _steps.Add(new UnitOfWorkStep(
                name,
                commit ?? throw new ArgumentNullException(nameof(commit)),
                rollback ?? (static _ => default)));
            return this;
        }

        /// <summary>Builds an immutable unit-of-work snapshot.</summary>
        public UnitOfWork Build()
            => new(_steps.ToArray());
    }
}

/// <summary>One named unit-of-work operation and compensation pair.</summary>
public sealed class UnitOfWorkStep
{
    public UnitOfWorkStep(
        string name,
        Func<CancellationToken, ValueTask> commit,
        Func<CancellationToken, ValueTask> rollback)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Unit-of-work step name is required.", nameof(name))
            : name;
        Commit = commit ?? throw new ArgumentNullException(nameof(commit));
        Rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
    }

    public string Name { get; }
    public Func<CancellationToken, ValueTask> Commit { get; }
    public Func<CancellationToken, ValueTask> Rollback { get; }
}

/// <summary>Commit result for a unit of work.</summary>
public sealed class UnitOfWorkResult
{
    private UnitOfWorkResult(
        bool committed,
        IReadOnlyList<string> committedSteps,
        string? failedStep,
        Exception? exception,
        UnitOfWorkRollbackResult? rollback)
    {
        Committed = committed;
        CommittedSteps = committedSteps;
        FailedStep = failedStep;
        Exception = exception;
        Rollback = rollback;
    }

    public bool Committed { get; }
    public IReadOnlyList<string> CommittedSteps { get; }
    public string? FailedStep { get; }
    public Exception? Exception { get; }
    public UnitOfWorkRollbackResult? Rollback { get; }

    public static UnitOfWorkResult Success(IReadOnlyList<string> committedSteps)
        => new(true, committedSteps, null, null, null);

    public static UnitOfWorkResult Failed(
        IReadOnlyList<string> committedSteps,
        string failedStep,
        Exception exception,
        UnitOfWorkRollbackResult rollback)
        => new(false, committedSteps, failedStep, exception, rollback);
}

/// <summary>Rollback result for a unit of work.</summary>
public sealed class UnitOfWorkRollbackResult
{
    public UnitOfWorkRollbackResult(IReadOnlyList<string> rolledBackSteps, IReadOnlyList<Exception> failures)
    {
        RolledBackSteps = rolledBackSteps;
        Failures = failures;
    }

    public IReadOnlyList<string> RolledBackSteps { get; }
    public IReadOnlyList<Exception> Failures { get; }
    public bool Succeeded => Failures.Count == 0;
}
