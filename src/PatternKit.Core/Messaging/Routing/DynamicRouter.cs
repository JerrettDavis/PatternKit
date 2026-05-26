using PatternKit.Common;

namespace PatternKit.Messaging.Routing;

/// <summary>
/// Dynamic router that keeps a runtime-updatable route table and routes each message through the current snapshot.
/// </summary>
public sealed class DynamicRouter<TPayload, TResult>
{
    /// <summary>Predicate used to decide whether a dynamic route matches.</summary>
    public delegate bool RoutePredicate(Message<TPayload> message, MessageContext context);

    /// <summary>Handler executed for the first matching dynamic route.</summary>
    public delegate TResult RouteHandler(Message<TPayload> message, MessageContext context);

    private readonly object _gate = new();
    private readonly RouteHandler? _default;
    private RouteEntry[] _routes;

    private DynamicRouter(RouteEntry[] routes, RouteHandler? @default)
        => (_routes, _default) = (routes, @default);

    /// <summary>Current ordered route names.</summary>
    public IReadOnlyList<string> RouteNames => _routes.Select(static route => route.Name).ToArray();

    /// <summary>Registers or replaces a route in the runtime route table.</summary>
    public DynamicRouter<TPayload, TResult> Register(string name, int order, RoutePredicate predicate, RouteHandler handler)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dynamic route name cannot be null, empty, or whitespace.", nameof(name));

        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        if (handler is null)
            throw new ArgumentNullException(nameof(handler));

        var entry = new RouteEntry(name, order, predicate, handler);
        lock (_gate)
        {
            _routes = _routes
                .Where(route => !string.Equals(route.Name, name, StringComparison.Ordinal))
                .Append(entry)
                .OrderBy(static route => route.Order)
                .ThenBy(static route => route.Name, StringComparer.Ordinal)
                .ToArray();
        }

        return this;
    }

    /// <summary>Unregisters a route by name.</summary>
    public bool Unregister(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dynamic route name cannot be null, empty, or whitespace.", nameof(name));

        lock (_gate)
        {
            var next = _routes.Where(route => !string.Equals(route.Name, name, StringComparison.Ordinal)).ToArray();
            if (next.Length == _routes.Length)
                return false;

            _routes = next;
            return true;
        }
    }

    /// <summary>Routes <paramref name="message"/> through the current route snapshot.</summary>
    public TResult Route(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        var snapshot = _routes;
        foreach (var route in snapshot)
            if (route.Predicate(message, effectiveContext))
                return route.Handler(message, effectiveContext);

        return _default is not null
            ? _default(message, effectiveContext)
            : Throw.NoStrategyMatched<TResult>();
    }

    /// <summary>Creates a new dynamic router builder.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for <see cref="DynamicRouter{TPayload,TResult}"/>.</summary>
    public sealed class Builder
    {
        private readonly List<RouteEntry> _routes = new(8);
        private RouteHandler? _default;

        /// <summary>Adds an initial route to the dynamic route table.</summary>
        public WhenBuilder When(string name, int order, RoutePredicate predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Dynamic route name cannot be null, empty, or whitespace.", nameof(name));

            return new WhenBuilder(this, name, order, predicate);
        }

        /// <summary>Sets the default route handler.</summary>
        public Builder Default(RouteHandler handler)
        {
            _default = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <summary>Builds the dynamic router with the configured initial route table.</summary>
        public DynamicRouter<TPayload, TResult> Build()
            => new(
                _routes
                    .GroupBy(static route => route.Name, StringComparer.Ordinal)
                    .Select(static group => group.Last())
                    .OrderBy(static route => route.Order)
                    .ThenBy(static route => route.Name, StringComparer.Ordinal)
                    .ToArray(),
                _default);

        /// <summary>Fluent route continuation.</summary>
        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly string _name;
            private readonly int _order;
            private readonly RoutePredicate _predicate;

            internal WhenBuilder(Builder owner, string name, int order, RoutePredicate predicate)
                => (_owner, _name, _order, _predicate) = (owner, name, order, predicate);

            /// <summary>Adds the handler for the current route.</summary>
            public Builder Then(RouteHandler handler)
            {
                if (handler is null)
                    throw new ArgumentNullException(nameof(handler));

                _owner._routes.Add(new RouteEntry(_name, _order, _predicate, handler));
                return _owner;
            }
        }
    }

    private sealed class RouteEntry
    {
        public RouteEntry(string name, int order, RoutePredicate predicate, RouteHandler handler)
            => (Name, Order, Predicate, Handler) = (name, order, predicate, handler);

        public string Name { get; }

        public int Order { get; }

        public RoutePredicate Predicate { get; }

        public RouteHandler Handler { get; }
    }
}
