using PatternKit.Common;

namespace PatternKit.Messaging.Routing;

/// <summary>
/// Content-based router that selects the first matching route for a message.
/// </summary>
public sealed class ContentRouter<TPayload, TResult>
{
    /// <summary>Predicate used to decide whether a route matches.</summary>
    public delegate bool RoutePredicate(Message<TPayload> message, MessageContext context);

    /// <summary>Handler executed for the first matching route.</summary>
    public delegate TResult RouteHandler(Message<TPayload> message, MessageContext context);

    private readonly RoutePredicate[] _predicates;
    private readonly RouteHandler[] _handlers;
    private readonly RouteHandler? _default;

    private ContentRouter(RoutePredicate[] predicates, RouteHandler[] handlers, RouteHandler? @default)
        => (_predicates, _handlers, _default) = (predicates, handlers, @default);

    /// <summary>
    /// Routes <paramref name="message"/> to the first matching handler.
    /// </summary>
    public TResult Route(Message<TPayload> message, MessageContext? context = null)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = context ?? MessageContext.From(message);
        for (var i = 0; i < _predicates.Length; i++)
            if (_predicates[i](message, effectiveContext))
                return _handlers[i](message, effectiveContext);

        return _default is not null
            ? _default(message, effectiveContext)
            : Throw.NoStrategyMatched<TResult>();
    }

    /// <summary>Creates a new content router builder.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for <see cref="ContentRouter{TPayload,TResult}"/>.</summary>
    public sealed class Builder
    {
        private readonly List<RoutePredicate> _predicates = new(8);
        private readonly List<RouteHandler> _handlers = new(8);
        private RouteHandler? _default;

        /// <summary>Adds a route predicate.</summary>
        public WhenBuilder When(RoutePredicate predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return new WhenBuilder(this, predicate);
        }

        /// <summary>Sets the default route handler.</summary>
        public Builder Default(RouteHandler handler)
        {
            _default = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <summary>Builds an immutable router.</summary>
        public ContentRouter<TPayload, TResult> Build()
            => new(_predicates.ToArray(), _handlers.ToArray(), _default);

        /// <summary>Fluent route continuation.</summary>
        public sealed class WhenBuilder
        {
            private readonly Builder _owner;
            private readonly RoutePredicate _predicate;

            internal WhenBuilder(Builder owner, RoutePredicate predicate)
                => (_owner, _predicate) = (owner, predicate);

            /// <summary>Adds the handler for the current predicate.</summary>
            public Builder Then(RouteHandler handler)
            {
                if (handler is null)
                    throw new ArgumentNullException(nameof(handler));

                _owner._predicates.Add(_predicate);
                _owner._handlers.Add(handler);
                return _owner;
            }
        }
    }
}
