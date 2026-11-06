namespace PatternKit.Cloud.GatewayAggregation;

public sealed class GatewayAggregationPart
{
    private GatewayAggregationPart(string name, object? value, Exception? exception, bool succeeded)
        => (Name, Value, Exception, Succeeded) = (name, value, exception, succeeded);

    public string Name { get; }

    public object? Value { get; }

    public Exception? Exception { get; }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    public static GatewayAggregationPart Success(string name, object value)
        => new(name, value ?? throw new ArgumentNullException(nameof(value)), null, true);

    public static GatewayAggregationPart Failure(string name, Exception exception)
        => new(name, null, exception ?? throw new ArgumentNullException(nameof(exception)), false);
}

public sealed class GatewayAggregationContext<TRequest>
{
    internal GatewayAggregationContext(TRequest request, IReadOnlyDictionary<string, GatewayAggregationPart> parts)
        => (Request, Parts) = (request, parts);

    public TRequest Request { get; }

    public IReadOnlyDictionary<string, GatewayAggregationPart> Parts { get; }

    public TPart Require<TPart>(string name)
    {
        if (!Parts.TryGetValue(name, out var part))
            throw new InvalidOperationException($"Gateway aggregation part '{name}' is not registered.");
        if (part.Failed)
            throw new InvalidOperationException($"Gateway aggregation part '{name}' failed.", part.Exception);
        if (part.Value is not TPart value)
            throw new InvalidOperationException($"Gateway aggregation part '{name}' is not '{typeof(TPart).FullName}'.");

        return value;
    }
}

public sealed class GatewayAggregationResult<TResponse>
{
    private GatewayAggregationResult(string gatewayName, TResponse? response, IReadOnlyDictionary<string, GatewayAggregationPart> parts, Exception? exception, bool aggregated)
        => (GatewayName, Response, Parts, Exception, Aggregated) = (gatewayName, response, parts, exception, aggregated);

    public string GatewayName { get; }

    public TResponse? Response { get; }

    public IReadOnlyDictionary<string, GatewayAggregationPart> Parts { get; }

    public Exception? Exception { get; }

    public bool Aggregated { get; }

    public bool Failed => !Aggregated;

    public static GatewayAggregationResult<TResponse> Success(string gatewayName, TResponse response, IReadOnlyDictionary<string, GatewayAggregationPart> parts)
        => new(gatewayName, response, parts, null, true);

    public static GatewayAggregationResult<TResponse> Failure(string gatewayName, IReadOnlyDictionary<string, GatewayAggregationPart> parts, Exception exception)
        => new(gatewayName, default, parts, exception ?? throw new ArgumentNullException(nameof(exception)), false);
}

public sealed class GatewayAggregation<TRequest, TResponse>
{
    private readonly IReadOnlyList<Fetch> _fetches;
    private readonly Func<GatewayAggregationContext<TRequest>, TResponse> _compose;

    private GatewayAggregation(string name, IReadOnlyList<Fetch> fetches, Func<GatewayAggregationContext<TRequest>, TResponse>? compose)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Gateway aggregation name is required.", nameof(name));
        if (fetches is null)
            throw new ArgumentNullException(nameof(fetches));
        if (fetches.Count == 0)
            throw new InvalidOperationException("Gateway aggregation requires at least one downstream fetch.");

        Name = name;
        _fetches = fetches;
        _compose = compose ?? throw new InvalidOperationException("Gateway aggregation requires a response composer.");
    }

    public string Name { get; }

    public GatewayAggregationResult<TResponse> Aggregate(TRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var parts = new Dictionary<string, GatewayAggregationPart>(StringComparer.OrdinalIgnoreCase);
        foreach (var fetch in _fetches)
        {
            try
            {
                var value = fetch.Execute(request);
                if (value is null)
                    parts[fetch.Name] = GatewayAggregationPart.Failure(fetch.Name, new InvalidOperationException($"Gateway aggregation part '{fetch.Name}' returned null."));
                else
                    parts[fetch.Name] = GatewayAggregationPart.Success(fetch.Name, value);
            }
            catch (Exception ex)
            {
                parts[fetch.Name] = GatewayAggregationPart.Failure(fetch.Name, ex);
            }
        }

        try
        {
            var response = _compose(new(request, parts));
            if (response is null)
                return GatewayAggregationResult<TResponse>.Failure(Name, parts, new InvalidOperationException("Gateway aggregation composer returned null."));

            return GatewayAggregationResult<TResponse>.Success(Name, response, parts);
        }
        catch (Exception ex)
        {
            return GatewayAggregationResult<TResponse>.Failure(Name, parts, ex);
        }
    }

    public static Builder Create(string name = "gateway-aggregation") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Fetch> _fetches = [];
        private Func<GatewayAggregationContext<TRequest>, TResponse>? _compose;

        internal Builder(string name) => _name = name;

        public Builder Fetch<TPart>(string name, Func<TRequest, TPart> fetch)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Gateway aggregation fetch name is required.", nameof(name));
            if (fetch is null)
                throw new ArgumentNullException(nameof(fetch));
            if (_fetches.Any(part => string.Equals(part.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Gateway aggregation fetch '{name}' is already registered.");

            _fetches.Add(new(name, request => fetch(request)!));
            return this;
        }

        public Builder Compose(Func<GatewayAggregationContext<TRequest>, TResponse> compose)
        {
            _compose = compose ?? throw new ArgumentNullException(nameof(compose));
            return this;
        }

        public GatewayAggregation<TRequest, TResponse> Build()
            => new(_name, _fetches.ToArray(), _compose);
    }

    private sealed class Fetch
    {
        public Fetch(string name, Func<TRequest, object?> execute)
            => (Name, Execute) = (name, execute);

        public string Name { get; }

        public Func<TRequest, object?> Execute { get; }
    }
}
