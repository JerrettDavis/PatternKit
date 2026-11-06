namespace PatternKit.Cloud.Sidecar;

public sealed class SidecarContext<TRequest>
{
    internal SidecarContext(TRequest request)
        => Request = request;

    public TRequest Request { get; }

    public IDictionary<string, object> Items { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    public IList<string> Events { get; } = new List<string>();

    public void Record(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name is required.", nameof(eventName));

        Events.Add(eventName);
    }
}

public sealed class SidecarResult<TResponse>
{
    private SidecarResult(string sidecarName, TResponse? response, IReadOnlyList<string> events, Exception? exception, bool succeeded)
        => (SidecarName, Response, Events, Exception, Succeeded) = (sidecarName, response, events, exception, succeeded);

    public string SidecarName { get; }

    public TResponse? Response { get; }

    public IReadOnlyList<string> Events { get; }

    public Exception? Exception { get; }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    public static SidecarResult<TResponse> Success(string sidecarName, TResponse response, IReadOnlyList<string> events)
        => new(sidecarName, response ?? throw new ArgumentNullException(nameof(response)), events, null, true);

    public static SidecarResult<TResponse> Failure(string sidecarName, IReadOnlyList<string> events, Exception exception)
        => new(sidecarName, default, events, exception ?? throw new ArgumentNullException(nameof(exception)), false);
}

public sealed class Sidecar<TRequest, TResponse>
{
    private readonly IReadOnlyList<BeforeStep> _before;
    private readonly IReadOnlyList<AfterStep> _after;
    private readonly Func<SidecarContext<TRequest>, TResponse> _handler;

    private Sidecar(string name, IReadOnlyList<BeforeStep> before, IReadOnlyList<AfterStep> after, Func<SidecarContext<TRequest>, TResponse>? handler)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Sidecar name is required.", nameof(name));
        if (before is null)
            throw new ArgumentNullException(nameof(before));
        if (after is null)
            throw new ArgumentNullException(nameof(after));
        if (before.Count == 0 && after.Count == 0)
            throw new InvalidOperationException("Sidecar requires at least one companion step.");

        Name = name;
        _before = before;
        _after = after;
        _handler = handler ?? throw new InvalidOperationException("Sidecar requires a primary handler.");
    }

    public string Name { get; }

    public SidecarResult<TResponse> Invoke(TRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var context = new SidecarContext<TRequest>(request);
        try
        {
            foreach (var step in _before)
            {
                step.Execute(context);
                context.Record(step.Name);
            }

            var response = _handler(context);
            if (response is null)
                return SidecarResult<TResponse>.Failure(Name, context.Events.ToArray(), new InvalidOperationException("Sidecar handler returned null."));

            foreach (var step in _after)
            {
                step.Execute(context, response);
                context.Record(step.Name);
            }

            return SidecarResult<TResponse>.Success(Name, response, context.Events.ToArray());
        }
        catch (Exception ex)
        {
            return SidecarResult<TResponse>.Failure(Name, context.Events.ToArray(), ex);
        }
    }

    public static Builder Create(string name = "sidecar") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<BeforeStep> _before = [];
        private readonly List<AfterStep> _after = [];
        private Func<SidecarContext<TRequest>, TResponse>? _handler;

        internal Builder(string name) => _name = name;

        public Builder Before(string name, Action<SidecarContext<TRequest>> step)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Sidecar step name is required.", nameof(name));
            if (step is null)
                throw new ArgumentNullException(nameof(step));
            if (_before.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Sidecar before step '{name}' is already registered.");

            _before.Add(new(name, step));
            return this;
        }

        public Builder After(string name, Action<SidecarContext<TRequest>, TResponse> step)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Sidecar step name is required.", nameof(name));
            if (step is null)
                throw new ArgumentNullException(nameof(step));
            if (_after.Any(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Sidecar after step '{name}' is already registered.");

            _after.Add(new(name, step));
            return this;
        }

        public Builder Handle(Func<SidecarContext<TRequest>, TResponse> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public Sidecar<TRequest, TResponse> Build() => new(_name, _before.ToArray(), _after.ToArray(), _handler);
    }

    private sealed class BeforeStep
    {
        public BeforeStep(string name, Action<SidecarContext<TRequest>> execute)
            => (Name, Execute) = (name, execute);

        public string Name { get; }

        public Action<SidecarContext<TRequest>> Execute { get; }
    }

    private sealed class AfterStep
    {
        public AfterStep(string name, Action<SidecarContext<TRequest>, TResponse> execute)
            => (Name, Execute) = (name, execute);

        public string Name { get; }

        public Action<SidecarContext<TRequest>, TResponse> Execute { get; }
    }
}
