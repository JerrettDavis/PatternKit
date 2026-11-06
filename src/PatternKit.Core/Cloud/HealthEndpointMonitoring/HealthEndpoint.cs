namespace PatternKit.Cloud.HealthEndpointMonitoring;

public sealed class HealthEndpointCheckResult
{
    private HealthEndpointCheckResult(string name, bool healthy, string message)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Health check name is required.", nameof(name));

        Name = name;
        Healthy = healthy;
        Message = message ?? string.Empty;
    }

    public string Name { get; }

    public bool Healthy { get; }

    public string Message { get; }

    public static HealthEndpointCheckResult HealthyCheck(string name, string message = "")
        => new(name, true, message);

    public static HealthEndpointCheckResult UnhealthyCheck(string name, string message)
        => new(name, false, message);
}

public sealed class HealthEndpointReport
{
    public HealthEndpointReport(string endpointName, IReadOnlyList<HealthEndpointCheckResult> checks)
    {
        if (string.IsNullOrWhiteSpace(endpointName))
            throw new ArgumentException("Health endpoint name is required.", nameof(endpointName));

        EndpointName = endpointName;
        Checks = checks ?? throw new ArgumentNullException(nameof(checks));
        Healthy = checks.All(static check => check.Healthy);
        PassedCount = checks.Count(static check => check.Healthy);
        FailedCount = checks.Count - PassedCount;
    }

    public string EndpointName { get; }

    public bool Healthy { get; }

    public int PassedCount { get; }

    public int FailedCount { get; }

    public IReadOnlyList<HealthEndpointCheckResult> Checks { get; }
}

public sealed class HealthEndpoint<TContext>
{
    private readonly IReadOnlyList<ConfiguredCheck> _checks;

    private HealthEndpoint(string name, IReadOnlyList<ConfiguredCheck> checks)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Health endpoint name is required.", nameof(name));

        if (checks is null)
            throw new ArgumentNullException(nameof(checks));
        if (checks.Count == 0)
            throw new InvalidOperationException("Health endpoint requires at least one check.");

        Name = name;
        _checks = checks;
    }

    public string Name { get; }

    public HealthEndpointReport Evaluate(TContext context)
    {
        if (context is null)
            throw new ArgumentNullException(nameof(context));

        var results = new List<HealthEndpointCheckResult>(_checks.Count);
        foreach (var check in _checks)
        {
            var result = check.Evaluate(context) ?? throw new InvalidOperationException($"Health check '{check.Name}' returned null.");
            results.Add(result);
        }

        return new(Name, results);
    }

    public static Builder Create(string name = "health-endpoint") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<ConfiguredCheck> _checks = [];

        internal Builder(string name) => _name = name;

        public Builder WithCheck(string name, Func<TContext, HealthEndpointCheckResult> check)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Health check name is required.", nameof(name));
            if (check is null)
                throw new ArgumentNullException(nameof(check));

            _checks.Add(new(name, check));
            return this;
        }

        public HealthEndpoint<TContext> Build() => new(_name, _checks.ToArray());
    }

    private sealed class ConfiguredCheck
    {
        public ConfiguredCheck(string name, Func<TContext, HealthEndpointCheckResult> evaluate)
            => (Name, Evaluate) = (name, evaluate);

        public string Name { get; }

        public Func<TContext, HealthEndpointCheckResult> Evaluate { get; }
    }
}
