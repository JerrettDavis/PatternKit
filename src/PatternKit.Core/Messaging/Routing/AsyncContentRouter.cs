using PatternKit.Common;

namespace PatternKit.Messaging.Routing;

/// <summary>
/// Async content-based router that selects the first matching route for a message.
/// </summary>
public sealed class AsyncContentRouter<TPayload, TResult>
{
    /// <summary>Async predicate used to decide whether a route matches.</summary>
    public delegate ValueTask<bool> RoutePredicate(Message<TPayload> message, MessageContext context, CancellationToken cancellationToken);

    /// <summary>Async handler executed for the first matching route.</summary>
    public delegate ValueTask<TResult> RouteHandler(Message<TPayload> message, MessageContext context, CancellationToken cancellationToken);

    private readonly RoutePredicate[] _predicates;
    private readonly RouteHandler[] _handlers;
    private readonly RouteHandler? _default;

    private AsyncContentRouter(RoutePredicate[] predicates, RouteHandler[] handlers, RouteHandler? @default)
        => (_predicates, _handlers, _default) = (predicates, handlers, @default);

    /// <summary>
    /// Routes <paramref name="message"/> to the first matching async handler.
    /// </summary>
    public async ValueTask<TResult> RouteAsync(
        Message<TPayload> message,
        MessageContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new ArgumentNullException(nameof(message));

        var effectiveContext = CreateContext(message, context, cancellationToken);
        for (var i = 0; i < _predicates.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _predicates[i](message, effectiveContext, cancellationToken).ConfigureAwait(false))
                return await _handlers[i](message, effectiveContext, cancellationToken).ConfigureAwait(false);
        }

        return _default is not null
            ? await _default(message, effectiveContext, cancellationToken).ConfigureAwait(false)
            : Throw.NoStrategyMatched<TResult>();
    }

    /// <summary>Creates a new async content router builder.</summary>
    public static Builder Create() => new();

    private static MessageContext CreateContext(
        Message<TPayload> message,
        MessageContext? context,
        CancellationToken cancellationToken)
    {
        if (context is null)
            return MessageContext.From(message, cancellationToken);

        return cancellationToken.CanBeCanceled
            ? context.WithCancellation(cancellationToken)
            : context;
    }

    /// <summary>Fluent builder for <see cref="AsyncContentRouter{TPayload,TResult}"/>.</summary>
    public sealed class Builder
    {
        private readonly List<RoutePredicate> _predicates = new(8);
        private readonly List<RouteHandler> _handlers = new(8);
        private RouteHandler? _default;

        /// <summary>Adds an async route predicate.</summary>
        public WhenBuilder When(RoutePredicate predicate)
        {
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            return new WhenBuilder(this, predicate);
        }

        /// <summary>Sets the default async route handler.</summary>
        public Builder Default(RouteHandler handler)
        {
            _default = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        /// <summary>Builds an immutable router.</summary>
        public AsyncContentRouter<TPayload, TResult> Build()
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
