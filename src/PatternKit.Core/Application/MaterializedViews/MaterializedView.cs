namespace PatternKit.Application.MaterializedViews;

public interface IMaterializedView<TState, TEvent>
{
    string Name { get; }

    ValueTask<TState> ProjectAsync(TState initialState, IEnumerable<TEvent> events, CancellationToken cancellationToken = default);
}

public sealed class MaterializedView<TState, TEvent> : IMaterializedView<TState, TEvent>
{
    private readonly HandlerRegistration[] _handlers;

    private MaterializedView(string name, IEnumerable<HandlerRegistration> handlers)
    {
        Name = name;
        _handlers = handlers
            .OrderBy(static handler => handler.Order)
            .ThenBy(static handler => handler.Index)
            .ToArray();
    }

    public string Name { get; }

    public static Builder Create(string name) => new(name);

    public async ValueTask<TState> ProjectAsync(TState initialState, IEnumerable<TEvent> events, CancellationToken cancellationToken = default)
    {
        if (events is null)
            throw new ArgumentNullException(nameof(events));

        var state = initialState;
        foreach (var @event in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (@event is null)
                throw new ArgumentException("Materialized view event stream cannot contain null events.", nameof(events));

            var eventType = @event.GetType();
            foreach (var handler in _handlers)
            {
                if (!handler.EventType.IsAssignableFrom(eventType))
                    continue;

                state = await handler.ApplyAsync(state, @event, cancellationToken).ConfigureAwait(false);
            }
        }

        return state;
    }

    public sealed class Builder
    {
        private readonly List<HandlerRegistration> _handlers = new();
        private readonly string _name;

        internal Builder(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Materialized view name is required.", nameof(name))
                : name;
        }

        public Builder WithHandler<TSpecificEvent>(Func<TState, TSpecificEvent, TState> handler, int order = 0)
            where TSpecificEvent : TEvent
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            _handlers.Add(new HandlerRegistration(
                typeof(TSpecificEvent),
                order,
                _handlers.Count,
                (state, @event, _) => @event is TSpecificEvent specificEvent
                    ? new ValueTask<TState>(handler(state, specificEvent))
                    : throw new InvalidOperationException("Materialized view handler received an incompatible event.")));
            return this;
        }

        public Builder WithAsyncHandler<TSpecificEvent>(
            Func<TState, TSpecificEvent, CancellationToken, ValueTask<TState>> handler,
            int order = 0)
            where TSpecificEvent : TEvent
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            _handlers.Add(new HandlerRegistration(
                typeof(TSpecificEvent),
                order,
                _handlers.Count,
                (state, @event, cancellationToken) => @event is TSpecificEvent specificEvent
                    ? handler(state, specificEvent, cancellationToken)
                    : throw new InvalidOperationException("Materialized view handler received an incompatible event.")));
            return this;
        }

        public MaterializedView<TState, TEvent> Build()
        {
            if (_handlers.Count == 0)
                throw new InvalidOperationException("Materialized view requires at least one event handler.");

            return new MaterializedView<TState, TEvent>(_name, _handlers);
        }
    }

    private sealed class HandlerRegistration
    {
        public HandlerRegistration(
            Type eventType,
            int order,
            int index,
            Func<TState, TEvent, CancellationToken, ValueTask<TState>> applyAsync)
        {
            EventType = eventType;
            Order = order;
            Index = index;
            ApplyAsync = applyAsync;
        }

        public Type EventType { get; }

        public int Order { get; }

        public int Index { get; }

        public Func<TState, TEvent, CancellationToken, ValueTask<TState>> ApplyAsync { get; }
    }
}
