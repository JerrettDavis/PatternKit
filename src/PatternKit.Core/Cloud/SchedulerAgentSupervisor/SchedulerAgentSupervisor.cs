namespace PatternKit.Cloud.SchedulerAgentSupervisor;

public sealed class SchedulerAgentContext<TWork>
{
    internal SchedulerAgentContext(string supervisorName, string jobName, TWork work, int attempt)
        => (SupervisorName, JobName, Work, Attempt) = (supervisorName, jobName, work, attempt);

    public string SupervisorName { get; }

    public string JobName { get; }

    public TWork Work { get; }

    public int Attempt { get; }

    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();

    public IList<string> Events { get; } = new List<string>();
}

public sealed class SchedulerAgentResult<TResult>
{
    private SchedulerAgentResult(
        string supervisorName,
        string jobName,
        string agentName,
        int attempt,
        TResult? response,
        Exception? exception,
        IReadOnlyList<string> events,
        bool succeeded,
        bool retryScheduled,
        bool exhausted)
        => (SupervisorName, JobName, AgentName, Attempt, Response, Exception, Events, Succeeded, RetryScheduled, Exhausted) =
            (supervisorName, jobName, agentName, attempt, response, exception, events, succeeded, retryScheduled, exhausted);

    public string SupervisorName { get; }

    public string JobName { get; }

    public string AgentName { get; }

    public int Attempt { get; }

    public TResult? Response { get; }

    public Exception? Exception { get; }

    public IReadOnlyList<string> Events { get; }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    public bool RetryScheduled { get; }

    public bool Exhausted { get; }

    public static SchedulerAgentResult<TResult> Success(string supervisorName, string jobName, string agentName, int attempt, TResult response, IReadOnlyList<string> events)
    {
        if (response is null)
            throw new ArgumentNullException(nameof(response));
        if (events is null)
            throw new ArgumentNullException(nameof(events));
        return new(supervisorName, jobName, agentName, attempt, response, null, events, succeeded: true, retryScheduled: false, exhausted: false);
    }

    public static SchedulerAgentResult<TResult> Failure(string supervisorName, string jobName, string agentName, int attempt, Exception exception, IReadOnlyList<string> events, bool retryScheduled, bool exhausted)
    {
        if (exception is null)
            throw new ArgumentNullException(nameof(exception));
        if (events is null)
            throw new ArgumentNullException(nameof(events));
        return new(supervisorName, jobName, agentName, attempt, default, exception, events, succeeded: false, retryScheduled, exhausted);
    }
}

public sealed class SchedulerSupervisionPolicy<TWork>
{
    private SchedulerSupervisionPolicy(int maxAttempts, TimeSpan retryDelay, Func<Exception, SchedulerAgentContext<TWork>, bool>? shouldRetry)
        => (MaxAttempts, RetryDelay, ShouldRetry) = (maxAttempts, retryDelay, shouldRetry ?? ((_, _) => true));

    public int MaxAttempts { get; }

    public TimeSpan RetryDelay { get; }

    public Func<Exception, SchedulerAgentContext<TWork>, bool> ShouldRetry { get; }

    public static Builder Create() => new();

    public sealed class Builder
    {
        private int _maxAttempts = 3;
        private TimeSpan _retryDelay = TimeSpan.FromSeconds(1);
        private Func<Exception, SchedulerAgentContext<TWork>, bool>? _shouldRetry;

        public Builder MaxAttempts(int maxAttempts)
        {
            if (maxAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            _maxAttempts = maxAttempts;
            return this;
        }

        public Builder RetryDelay(TimeSpan retryDelay)
        {
            if (retryDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(retryDelay));
            _retryDelay = retryDelay;
            return this;
        }

        public Builder RetryWhen(Func<Exception, SchedulerAgentContext<TWork>, bool> shouldRetry)
        {
            _shouldRetry = shouldRetry ?? throw new ArgumentNullException(nameof(shouldRetry));
            return this;
        }

        public SchedulerSupervisionPolicy<TWork> Build() => new(_maxAttempts, _retryDelay, _shouldRetry);
    }
}

public sealed class SchedulerAgentSupervisor<TWork, TResult>
{
    private readonly List<ScheduledJob> _jobs = [];
    private readonly List<Agent> _agents = [];
    private readonly Func<DateTimeOffset> _clock;
    private readonly SchedulerSupervisionPolicy<TWork> _policy;

    private SchedulerAgentSupervisor(string name, Func<DateTimeOffset>? clock, SchedulerSupervisionPolicy<TWork>? policy, IReadOnlyList<Agent> agents)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supervisor name is required.", nameof(name));
        if (agents.Count == 0)
            throw new InvalidOperationException("Scheduler Agent Supervisor requires at least one agent.");

        Name = name;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
        _policy = policy ?? SchedulerSupervisionPolicy<TWork>.Create().Build();
        _agents.AddRange(agents);
    }

    public string Name { get; }

    public IReadOnlyList<string> PendingJobs => _jobs.Select(static job => job.Name).ToArray();

    public static Builder Create(string name = "scheduler-agent-supervisor") => new(name);

    public SchedulerAgentSupervisor<TWork, TResult> Schedule(string jobName, TWork work, DateTimeOffset dueAt)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentException("Job name is required.", nameof(jobName));
        if (work is null)
            throw new ArgumentNullException(nameof(work));
        _jobs.Add(new ScheduledJob(jobName, work, dueAt, attempt: 0));
        return this;
    }

    public IReadOnlyList<SchedulerAgentResult<TResult>> RunDue() => RunDue(_clock());

    public IReadOnlyList<SchedulerAgentResult<TResult>> RunDue(DateTimeOffset now)
    {
        var due = _jobs.Where(job => job.DueAt <= now).OrderBy(static job => job.DueAt).ToArray();
        if (due.Length == 0)
            return Array.Empty<SchedulerAgentResult<TResult>>();

        foreach (var job in due)
            _jobs.Remove(job);

        var results = new List<SchedulerAgentResult<TResult>>(due.Length);
        foreach (var job in due)
            results.Add(Dispatch(job, now));

        return results;
    }

    private SchedulerAgentResult<TResult> Dispatch(ScheduledJob job, DateTimeOffset now)
    {
        var agent = _agents[0];
        var attempt = job.Attempt + 1;
        var context = new SchedulerAgentContext<TWork>(Name, job.Name, job.Work, attempt);
        context.Events.Add($"dispatch:{agent.Name}:{attempt}");
        try
        {
            var response = agent.Execute(context);
            return SchedulerAgentResult<TResult>.Success(Name, job.Name, agent.Name, attempt, response, context.Events.ToArray());
        }
        catch (Exception exception)
        {
            var canRetry = attempt < _policy.MaxAttempts && _policy.ShouldRetry(exception, context);
            if (canRetry)
                _jobs.Add(new ScheduledJob(job.Name, job.Work, now.Add(_policy.RetryDelay), attempt));
            return SchedulerAgentResult<TResult>.Failure(Name, job.Name, agent.Name, attempt, exception, context.Events.ToArray(), canRetry, exhausted: !canRetry);
        }
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Agent> _agents = [];
        private Func<DateTimeOffset>? _clock;
        private SchedulerSupervisionPolicy<TWork>? _policy;

        internal Builder(string name) => _name = name;

        public Builder Clock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public Builder Supervision(SchedulerSupervisionPolicy<TWork> policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            return this;
        }

        public Builder Agent(string name, Func<SchedulerAgentContext<TWork>, TResult> execute)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Agent name is required.", nameof(name));
            if (execute is null)
                throw new ArgumentNullException(nameof(execute));
            if (_agents.Any(agent => agent.Name == name))
                throw new InvalidOperationException($"Scheduler agent '{name}' is already registered.");
            _agents.Add(new Agent(name, execute));
            return this;
        }

        public SchedulerAgentSupervisor<TWork, TResult> Build() => new(_name, _clock, _policy, _agents);
    }

    private sealed class Agent
    {
        public Agent(string name, Func<SchedulerAgentContext<TWork>, TResult> execute)
            => (Name, Execute) = (name, execute);

        public string Name { get; }

        public Func<SchedulerAgentContext<TWork>, TResult> Execute { get; }
    }

    private sealed class ScheduledJob
    {
        public ScheduledJob(string name, TWork work, DateTimeOffset dueAt, int attempt)
            => (Name, Work, DueAt, Attempt) = (name, work, dueAt, attempt);

        public string Name { get; }

        public TWork Work { get; }

        public DateTimeOffset DueAt { get; }

        public int Attempt { get; }
    }
}
