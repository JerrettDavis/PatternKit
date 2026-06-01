namespace PatternKit.Application.CompensatingTransactions;

/// <summary>
/// Executes a reversible business transaction and compensates completed steps in reverse order when a later step fails.
/// </summary>
public sealed class CompensatingTransaction<TContext>
{
    private readonly IReadOnlyList<CompensatingTransactionStep<TContext>> _steps;

    private CompensatingTransaction(string name, IReadOnlyList<CompensatingTransactionStep<TContext>> steps)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Compensating transaction name cannot be null, empty, or whitespace.", nameof(name));
        if (steps is null)
            throw new ArgumentNullException(nameof(steps));
        if (steps.Count == 0)
            throw new ArgumentException("Compensating transaction must contain at least one step.", nameof(steps));

        Name = name;
        _steps = steps;
    }

    public string Name { get; }

    public IReadOnlyList<CompensatingTransactionStep<TContext>> Steps => _steps;

    public static Builder Create(string name = "compensating-transaction") => new(name);

    public async ValueTask<CompensatingTransactionExecution<TContext>> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        var history = new List<CompensatingTransactionRecord>();
        var completed = new Stack<CompensatingTransactionStep<TContext>>();

        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!step.ShouldRun(context))
            {
                history.Add(CompensatingTransactionRecord.Skipped(step.Name));
                continue;
            }

            try
            {
                await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                completed.Push(step);
                history.Add(CompensatingTransactionRecord.Completed(step.Name));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                history.Add(CompensatingTransactionRecord.Failed(step.Name, exception));
                var compensation = await CompensateAsync(completed, context, history, cancellationToken).ConfigureAwait(false);
                return new CompensatingTransactionExecution<TContext>(
                    Name,
                    context,
                    compensation.Succeeded ? CompensatingTransactionStatus.Compensated : CompensatingTransactionStatus.CompensationFailed,
                    history);
            }
        }

        return new CompensatingTransactionExecution<TContext>(Name, context, CompensatingTransactionStatus.Completed, history);
    }

    public CompensatingTransactionExecution<TContext> Execute(TContext context)
        => ExecuteAsync(context).AsTask().GetAwaiter().GetResult();

    private static async ValueTask<CompensationOutcome> CompensateAsync(
        Stack<CompensatingTransactionStep<TContext>> completed,
        TContext context,
        List<CompensatingTransactionRecord> history,
        CancellationToken cancellationToken)
    {
        var succeeded = true;
        while (completed.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var step = completed.Pop();

            try
            {
                await step.CompensateAsync(context, cancellationToken).ConfigureAwait(false);
                history.Add(CompensatingTransactionRecord.Compensated(step.Name));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                succeeded = false;
                history.Add(CompensatingTransactionRecord.CompensationFailed(step.Name, exception));
            }
        }

        return new CompensationOutcome(succeeded);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<CompensatingTransactionStep<TContext>> _steps = [];

        internal Builder(string name) => _name = name;

        public Builder AddStep(
            string name,
            Func<TContext, CancellationToken, ValueTask> execute,
            Func<TContext, CancellationToken, ValueTask> compensate,
            Action<CompensatingTransactionStepBuilder<TContext>>? configure = null)
        {
            var builder = new CompensatingTransactionStepBuilder<TContext>(name, execute, compensate);
            configure?.Invoke(builder);
            _steps.Add(builder.Build());
            return this;
        }

        public CompensatingTransaction<TContext> Build()
        {
            var ordered = _steps
                .OrderBy(static step => step.Order)
                .ThenBy(static step => step.Name, StringComparer.Ordinal)
                .ToArray();

            if (ordered.Select(static step => step.Name).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
                throw new InvalidOperationException("Compensating transaction step names must be unique.");

            return new CompensatingTransaction<TContext>(_name, ordered);
        }
    }

    private readonly struct CompensationOutcome
    {
        public CompensationOutcome(bool succeeded) => Succeeded = succeeded;

        public bool Succeeded { get; }
    }
}

public sealed class CompensatingTransactionStepBuilder<TContext>
{
    private readonly string _name;
    private readonly Func<TContext, CancellationToken, ValueTask> _execute;
    private readonly Func<TContext, CancellationToken, ValueTask> _compensate;
    private Func<TContext, bool> _condition = static _ => true;
    private int _order;

    internal CompensatingTransactionStepBuilder(
        string name,
        Func<TContext, CancellationToken, ValueTask> execute,
        Func<TContext, CancellationToken, ValueTask> compensate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Compensating transaction step name cannot be null, empty, or whitespace.", nameof(name));

        _name = name;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _compensate = compensate ?? throw new ArgumentNullException(nameof(compensate));
    }

    public CompensatingTransactionStepBuilder<TContext> At(int order)
    {
        _order = order;
        return this;
    }

    public CompensatingTransactionStepBuilder<TContext> When(Func<TContext, bool> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    internal CompensatingTransactionStep<TContext> Build()
        => new(_name, _order, _condition, _execute, _compensate);
}

public sealed class CompensatingTransactionStep<TContext>
{
    internal CompensatingTransactionStep(
        string name,
        int order,
        Func<TContext, bool> shouldRun,
        Func<TContext, CancellationToken, ValueTask> execute,
        Func<TContext, CancellationToken, ValueTask> compensate)
    {
        Name = name;
        Order = order;
        ShouldRun = shouldRun;
        ExecuteAsync = execute;
        CompensateAsync = compensate;
    }

    public string Name { get; }

    public int Order { get; }

    internal Func<TContext, bool> ShouldRun { get; }

    internal Func<TContext, CancellationToken, ValueTask> ExecuteAsync { get; }

    internal Func<TContext, CancellationToken, ValueTask> CompensateAsync { get; }
}

public sealed class CompensatingTransactionExecution<TContext>
{
    public CompensatingTransactionExecution(
        string transactionName,
        TContext context,
        CompensatingTransactionStatus status,
        IReadOnlyList<CompensatingTransactionRecord> history)
    {
        TransactionName = transactionName;
        Context = context;
        Status = status;
        History = history ?? throw new ArgumentNullException(nameof(history));
    }

    public string TransactionName { get; }

    public TContext Context { get; }

    public CompensatingTransactionStatus Status { get; }

    public IReadOnlyList<CompensatingTransactionRecord> History { get; }
}

public sealed class CompensatingTransactionRecord
{
    public CompensatingTransactionRecord(string stepName, CompensatingTransactionRecordKind kind, string? errorMessage)
    {
        StepName = stepName;
        Kind = kind;
        ErrorMessage = errorMessage;
    }

    public string StepName { get; }

    public CompensatingTransactionRecordKind Kind { get; }

    public string? ErrorMessage { get; }

    public static CompensatingTransactionRecord Completed(string stepName) => new(stepName, CompensatingTransactionRecordKind.Completed, null);

    public static CompensatingTransactionRecord Skipped(string stepName) => new(stepName, CompensatingTransactionRecordKind.Skipped, null);

    public static CompensatingTransactionRecord Failed(string stepName, Exception exception) => new(stepName, CompensatingTransactionRecordKind.Failed, exception.Message);

    public static CompensatingTransactionRecord Compensated(string stepName) => new(stepName, CompensatingTransactionRecordKind.Compensated, null);

    public static CompensatingTransactionRecord CompensationFailed(string stepName, Exception exception) => new(stepName, CompensatingTransactionRecordKind.CompensationFailed, exception.Message);
}

public enum CompensatingTransactionStatus
{
    Completed,
    Compensated,
    CompensationFailed
}

public enum CompensatingTransactionRecordKind
{
    Completed,
    Skipped,
    Failed,
    Compensated,
    CompensationFailed
}
