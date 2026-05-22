namespace PatternKit.Cloud.StranglerFig;

public enum StranglerFigRoute
{
    Legacy,
    Modern
}

public sealed class StranglerFigDecision
{
    private StranglerFigDecision(StranglerFigRoute route, string ruleName)
        => (Route, RuleName) = (route, ruleName);

    public StranglerFigRoute Route { get; }

    public string RuleName { get; }

    public bool UsedLegacy => Route == StranglerFigRoute.Legacy;

    public bool UsedModern => Route == StranglerFigRoute.Modern;

    public static StranglerFigDecision Legacy(string ruleName = "fallback")
        => new(StranglerFigRoute.Legacy, string.IsNullOrWhiteSpace(ruleName) ? "fallback" : ruleName);

    public static StranglerFigDecision Modern(string ruleName)
        => new(StranglerFigRoute.Modern, string.IsNullOrWhiteSpace(ruleName) ? throw new ArgumentException("Rule name is required.", nameof(ruleName)) : ruleName);
}

public sealed class StranglerFigResult<TResponse>
{
    private StranglerFigResult(string migrationName, StranglerFigDecision decision, TResponse response)
        => (MigrationName, Decision, Response) = (migrationName, decision, response);

    public string MigrationName { get; }

    public StranglerFigDecision Decision { get; }

    public TResponse Response { get; }

    public bool UsedLegacy => Decision.UsedLegacy;

    public bool UsedModern => Decision.UsedModern;

    public static StranglerFigResult<TResponse> From(string migrationName, StranglerFigDecision decision, TResponse response)
        => new(migrationName, decision ?? throw new ArgumentNullException(nameof(decision)), response ?? throw new ArgumentNullException(nameof(response)));
}

public sealed class StranglerFig<TRequest, TResponse>
{
    private readonly IReadOnlyList<RouteRule> _routes;
    private readonly Func<TRequest, TResponse> _legacy;
    private readonly Func<TRequest, TResponse> _modern;

    private StranglerFig(string name, IReadOnlyList<RouteRule> routes, Func<TRequest, TResponse>? legacy, Func<TRequest, TResponse>? modern)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Strangler Fig migration name is required.", nameof(name));
        if (routes is null)
            throw new ArgumentNullException(nameof(routes));
        if (routes.Count == 0)
            throw new InvalidOperationException("Strangler Fig requires at least one modern route rule.");

        Name = name;
        _routes = routes;
        _legacy = legacy ?? throw new InvalidOperationException("Strangler Fig requires a legacy handler.");
        _modern = modern ?? throw new InvalidOperationException("Strangler Fig requires a modern handler.");
    }

    public string Name { get; }

    public StranglerFigResult<TResponse> Route(TRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        foreach (var route in _routes)
        {
            if (route.Matches(request))
                return StranglerFigResult<TResponse>.From(Name, StranglerFigDecision.Modern(route.Name), _modern(request));
        }

        return StranglerFigResult<TResponse>.From(Name, StranglerFigDecision.Legacy(), _legacy(request));
    }

    public static Builder Create(string name = "strangler-fig") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<RouteRule> _routes = [];
        private Func<TRequest, TResponse>? _legacy;
        private Func<TRequest, TResponse>? _modern;

        internal Builder(string name) => _name = name;

        public Builder RouteToModern(string name, Func<TRequest, bool> predicate)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Strangler Fig route name is required.", nameof(name));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));
            if (_routes.Any(route => string.Equals(route.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Strangler Fig route '{name}' is already registered.");

            _routes.Add(new(name, predicate));
            return this;
        }

        public Builder Legacy(Func<TRequest, TResponse> handler)
        {
            _legacy = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public Builder Modern(Func<TRequest, TResponse> handler)
        {
            _modern = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public StranglerFig<TRequest, TResponse> Build() => new(_name, _routes.ToArray(), _legacy, _modern);
    }

    private sealed class RouteRule
    {
        public RouteRule(string name, Func<TRequest, bool> matches)
            => (Name, Matches) = (name, matches);

        public string Name { get; }

        public Func<TRequest, bool> Matches { get; }
    }
}
