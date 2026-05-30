namespace PatternKit.Application.WorkflowOrchestration;

/// <summary>
/// Executes an explicit multi-step workflow with conditional steps, retries, compensation, and observable history.
/// </summary>
public sealed class WorkflowOrchestrator<TContext>
{
    private readonly IReadOnlyList<WorkflowStep<TContext>> _steps;

    private WorkflowOrchestrator(string name, IReadOnlyList<WorkflowStep<TContext>> steps)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow name cannot be null, empty, or whitespace.", nameof(name));
        if (steps is null)
            throw new ArgumentNullException(nameof(steps));
        if (steps.Count == 0)
            throw new ArgumentException("Workflow must contain at least one step.", nameof(steps));

        Name = name;
        _steps = steps;
    }

    public string Name { get; }

    public IReadOnlyList<WorkflowStep<TContext>> Steps => _steps;

    public async ValueTask<WorkflowExecution<TContext>> ExecuteAsync(TContext context, CancellationToken cancellationToken = default)
    {
        var history = new List<WorkflowExecutionRecord>();
        var completed = new Stack<WorkflowStep<TContext>>();

        foreach (var step in _steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!step.ShouldRun(context))
            {
                history.Add(WorkflowExecutionRecord.Skipped(step.Name));
                continue;
            }

            var outcome = await ExecuteStepAsync(step, context, history, cancellationToken).ConfigureAwait(false);
            if (!outcome.Succeeded)
            {
                await CompensateAsync(completed, context, history, cancellationToken).ConfigureAwait(false);
                return new WorkflowExecution<TContext>(Name, context, WorkflowExecutionStatus.Failed, history);
            }

            completed.Push(step);
        }

        return new WorkflowExecution<TContext>(Name, context, WorkflowExecutionStatus.Completed, history);
    }

    public WorkflowExecution<TContext> Execute(TContext context)
        => ExecuteAsync(context).AsTask().GetAwaiter().GetResult();

    public static Builder Create(string name = "workflow-orchestration") => new(name);

    private static async ValueTask<WorkflowStepOutcome> ExecuteStepAsync(
        WorkflowStep<TContext> step,
        TContext context,
        List<WorkflowExecutionRecord> history,
        CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= step.MaxAttempts; attempt++)
        {
            try
            {
                await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
                history.Add(WorkflowExecutionRecord.Completed(step.Name, attempt));
                return WorkflowStepOutcome.Success();
            }
            catch (Exception exception) when (attempt < step.MaxAttempts)
            {
                lastException = exception;
                history.Add(WorkflowExecutionRecord.Retried(step.Name, attempt, exception));
            }
            catch (Exception exception)
            {
                lastException = exception;
                history.Add(WorkflowExecutionRecord.Failed(step.Name, attempt, exception));
            }
        }

        return WorkflowStepOutcome.Failure(lastException);
    }

    private static async ValueTask CompensateAsync(
        Stack<WorkflowStep<TContext>> completed,
        TContext context,
        List<WorkflowExecutionRecord> history,
        CancellationToken cancellationToken)
    {
        while (completed.Count > 0)
        {
            var step = completed.Pop();
            if (!step.HasCompensation)
                continue;

            try
            {
                await step.CompensateAsync(context, cancellationToken).ConfigureAwait(false);
                history.Add(WorkflowExecutionRecord.Compensated(step.Name));
            }
            catch (Exception exception)
            {
                history.Add(WorkflowExecutionRecord.CompensationFailed(step.Name, exception));
            }
        }
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<WorkflowStep<TContext>> _steps = [];

        internal Builder(string name) => _name = name;

        public Builder AddStep(
            string name,
            Func<TContext, CancellationToken, ValueTask> execute,
            Action<WorkflowStepBuilder<TContext>>? configure = null)
        {
            var builder = new WorkflowStepBuilder<TContext>(name, execute);
            configure?.Invoke(builder);
            _steps.Add(builder.Build());
            return this;
        }

        public WorkflowOrchestrator<TContext> Build()
        {
            var ordered = _steps
                .OrderBy(static step => step.Order)
                .ThenBy(static step => step.Name, StringComparer.Ordinal)
                .ToArray();

            if (ordered.Select(static step => step.Name).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
                throw new InvalidOperationException("Workflow step names must be unique.");

            return new WorkflowOrchestrator<TContext>(_name, ordered);
        }
    }
}

public sealed class WorkflowStepBuilder<TContext>
{
    private readonly string _name;
    private readonly Func<TContext, CancellationToken, ValueTask> _execute;
    private Func<TContext, bool> _condition = static _ => true;
    private Func<TContext, CancellationToken, ValueTask>? _compensation;
    private int _maxAttempts = 1;
    private int _order;

    internal WorkflowStepBuilder(string name, Func<TContext, CancellationToken, ValueTask> execute)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Workflow step name cannot be null, empty, or whitespace.", nameof(name));

        _name = name;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public WorkflowStepBuilder<TContext> At(int order)
    {
        _order = order;
        return this;
    }

    public WorkflowStepBuilder<TContext> When(Func<TContext, bool> condition)
    {
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));
        return this;
    }

    public WorkflowStepBuilder<TContext> WithMaxAttempts(int maxAttempts)
    {
        if (maxAttempts <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Max attempts must be positive.");

        _maxAttempts = maxAttempts;
        return this;
    }

    public WorkflowStepBuilder<TContext> Compensate(Func<TContext, CancellationToken, ValueTask> compensation)
    {
        _compensation = compensation ?? throw new ArgumentNullException(nameof(compensation));
        return this;
    }

    internal WorkflowStep<TContext> Build()
        => new(_name, _order, _maxAttempts, _condition, _execute, _compensation);
}

public sealed class WorkflowStep<TContext>
{
    internal WorkflowStep(
        string name,
        int order,
        int maxAttempts,
        Func<TContext, bool> shouldRun,
        Func<TContext, CancellationToken, ValueTask> execute,
        Func<TContext, CancellationToken, ValueTask>? compensate)
    {
        Name = name;
        Order = order;
        MaxAttempts = maxAttempts;
        ShouldRun = shouldRun;
        ExecuteAsync = execute;
        CompensateAsync = compensate is null ? NoCompensationAsync : compensate;
        HasCompensation = compensate is not null;
    }

    public string Name { get; }

    public int Order { get; }

    public int MaxAttempts { get; }

    public bool HasCompensation { get; }

    internal Func<TContext, bool> ShouldRun { get; }

    internal Func<TContext, CancellationToken, ValueTask> ExecuteAsync { get; }

    internal Func<TContext, CancellationToken, ValueTask> CompensateAsync { get; }

    private static ValueTask NoCompensationAsync(TContext context, CancellationToken cancellationToken)
        => default;
}

public sealed class WorkflowExecution<TContext>
{
    public WorkflowExecution(string workflowName, TContext context, WorkflowExecutionStatus status, IReadOnlyList<WorkflowExecutionRecord> history)
    {
        WorkflowName = workflowName;
        Context = context;
        Status = status;
        History = history ?? throw new ArgumentNullException(nameof(history));
    }

    public string WorkflowName { get; }

    public TContext Context { get; }

    public WorkflowExecutionStatus Status { get; }

    public IReadOnlyList<WorkflowExecutionRecord> History { get; }
}

public sealed class WorkflowExecutionRecord
{
    public WorkflowExecutionRecord(string stepName, WorkflowExecutionRecordKind kind, int attempt, string? errorMessage)
    {
        StepName = stepName;
        Kind = kind;
        Attempt = attempt;
        ErrorMessage = errorMessage;
    }

    public string StepName { get; }

    public WorkflowExecutionRecordKind Kind { get; }

    public int Attempt { get; }

    public string? ErrorMessage { get; }

    public static WorkflowExecutionRecord Completed(string stepName, int attempt) => new(stepName, WorkflowExecutionRecordKind.Completed, attempt, null);

    public static WorkflowExecutionRecord Skipped(string stepName) => new(stepName, WorkflowExecutionRecordKind.Skipped, 0, null);

    public static WorkflowExecutionRecord Retried(string stepName, int attempt, Exception exception) => new(stepName, WorkflowExecutionRecordKind.Retried, attempt, exception.Message);

    public static WorkflowExecutionRecord Failed(string stepName, int attempt, Exception exception) => new(stepName, WorkflowExecutionRecordKind.Failed, attempt, exception.Message);

    public static WorkflowExecutionRecord Compensated(string stepName) => new(stepName, WorkflowExecutionRecordKind.Compensated, 0, null);

    public static WorkflowExecutionRecord CompensationFailed(string stepName, Exception exception) => new(stepName, WorkflowExecutionRecordKind.CompensationFailed, 0, exception.Message);
}

public enum WorkflowExecutionStatus
{
    Completed,
    Failed
}

public enum WorkflowExecutionRecordKind
{
    Completed,
    Skipped,
    Retried,
    Failed,
    Compensated,
    CompensationFailed
}

internal sealed class WorkflowStepOutcome
{
    private WorkflowStepOutcome(bool succeeded, Exception? exception)
    {
        Succeeded = succeeded;
        Exception = exception;
    }

    public bool Succeeded { get; }

    public Exception? Exception { get; }

    public static WorkflowStepOutcome Success() => new(true, null);

    public static WorkflowStepOutcome Failure(Exception? exception) => new(false, exception);
}
