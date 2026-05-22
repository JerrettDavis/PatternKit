namespace PatternKit.Cloud.GatewayRouting;

public sealed class GatewayRoutingResult<TResponse>
{
    private GatewayRoutingResult(string gatewayName, string routeName, TResponse response, bool fallback)
        => (GatewayName, RouteName, Response, Fallback) = (gatewayName, routeName, response, fallback);

    public string GatewayName { get; }

    public string RouteName { get; }

    public TResponse Response { get; }

    public bool Fallback { get; }

    public bool MatchedRoute => !Fallback;

    public static GatewayRoutingResult<TResponse> Matched(string gatewayName, string routeName, TResponse response)
        => new(gatewayName, routeName, response ?? throw new ArgumentNullException(nameof(response)), false);

    public static GatewayRoutingResult<TResponse> FromFallback(string gatewayName, string routeName, TResponse response)
        => new(gatewayName, routeName, response ?? throw new ArgumentNullException(nameof(response)), true);
}

public sealed class GatewayRouting<TRequest, TResponse>
{
    private readonly IReadOnlyList<RouteEntry> _routes;
    private readonly RouteEntry _fallback;

    private GatewayRouting(string name, IReadOnlyList<RouteEntry> routes, RouteEntry? fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Gateway routing name is required.", nameof(name));
        if (routes is null)
            throw new ArgumentNullException(nameof(routes));
        if (routes.Count == 0)
            throw new InvalidOperationException("Gateway routing requires at least one route.");

        Name = name;
        _routes = routes;
        _fallback = fallback ?? throw new InvalidOperationException("Gateway routing requires a fallback route.");
    }

    public string Name { get; }

    public GatewayRoutingResult<TResponse> Route(TRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        foreach (var route in _routes)
        {
            if (route.Matches(request))
                return GatewayRoutingResult<TResponse>.Matched(Name, route.Name, route.Handle(request));
        }

        return GatewayRoutingResult<TResponse>.FromFallback(Name, _fallback.Name, _fallback.Handle(request));
    }

    public static Builder Create(string name = "gateway-routing") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<RouteEntry> _routes = [];
        private RouteEntry? _fallback;

        internal Builder(string name) => _name = name;

        public Builder Route(string name, Func<TRequest, bool> predicate, Func<TRequest, TResponse> handler)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Gateway route name is required.", nameof(name));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));
            if (_routes.Any(route => string.Equals(route.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Gateway route '{name}' is already registered.");

            _routes.Add(new(name, predicate, handler));
            return this;
        }

        public Builder Fallback(string name, Func<TRequest, TResponse> handler)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Gateway fallback route name is required.", nameof(name));
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            _fallback = new(name, static _ => true, handler);
            return this;
        }

        public GatewayRouting<TRequest, TResponse> Build() => new(_name, _routes.ToArray(), _fallback);
    }

    private sealed class RouteEntry
    {
        public RouteEntry(string name, Func<TRequest, bool> matches, Func<TRequest, TResponse> handle)
            => (Name, Matches, Handle) = (name, matches, handle);

        public string Name { get; }

        public Func<TRequest, bool> Matches { get; }

        public Func<TRequest, TResponse> Handle { get; }
    }
}
