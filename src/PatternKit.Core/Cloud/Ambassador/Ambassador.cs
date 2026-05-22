namespace PatternKit.Cloud.Ambassador;

public sealed class AmbassadorContext<TRequest>
{
    internal AmbassadorContext(string ambassadorName, TRequest request)
        => (AmbassadorName, Request) = (ambassadorName, request);

    public string AmbassadorName { get; }

    public TRequest Request { get; internal set; }

    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    public IList<string> Events { get; } = [];

    public void Record(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("Event name is required.", nameof(eventName));

        Events.Add(eventName);
    }
}

public sealed class AmbassadorResult<TResponse>
{
    private AmbassadorResult(string ambassadorName, TResponse? response, Exception? exception, IReadOnlyList<string> events, bool succeeded, bool usedFallback)
        => (AmbassadorName, Response, Exception, Events, Succeeded, UsedFallback) = (ambassadorName, response, exception, events, succeeded, usedFallback);

    public string AmbassadorName { get; }

    public TResponse? Response { get; }

    public Exception? Exception { get; }

    public IReadOnlyList<string> Events { get; }

    public bool Succeeded { get; }

    public bool Failed => !Succeeded;

    public bool UsedFallback { get; }

    public static AmbassadorResult<TResponse> Success(string ambassadorName, TResponse response, IReadOnlyList<string> events, bool usedFallback = false)
        => new(ambassadorName, response ?? throw new ArgumentNullException(nameof(response)), null, events ?? throw new ArgumentNullException(nameof(events)), true, usedFallback);

    public static AmbassadorResult<TResponse> Failure(string ambassadorName, Exception exception, IReadOnlyList<string> events)
        => new(ambassadorName, default, exception ?? throw new ArgumentNullException(nameof(exception)), events ?? throw new ArgumentNullException(nameof(events)), false, false);
}

public sealed class Ambassador<TRequest, TResponse>
{
    private readonly IReadOnlyList<Step> _telemetry;
    private readonly IReadOnlyList<Func<TRequest, TRequest>> _transforms;
    private readonly Func<TRequest, bool> _connectionPolicy;
    private readonly Func<AmbassadorContext<TRequest>, TResponse> _call;
    private readonly Func<AmbassadorContext<TRequest>, TResponse>? _fallback;

    private Ambassador(
        string name,
        IReadOnlyList<Step> telemetry,
        IReadOnlyList<Func<TRequest, TRequest>> transforms,
        Func<TRequest, bool>? connectionPolicy,
        Func<AmbassadorContext<TRequest>, TResponse>? call,
        Func<AmbassadorContext<TRequest>, TResponse>? fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Ambassador name is required.", nameof(name));
        if (call is null)
            throw new InvalidOperationException("Ambassador requires an outbound call handler.");

        Name = name;
        _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
        _transforms = transforms ?? throw new ArgumentNullException(nameof(transforms));
        _connectionPolicy = connectionPolicy ?? (_ => true);
        _call = call;
        _fallback = fallback;
    }

    public string Name { get; }

    public AmbassadorResult<TResponse> Invoke(TRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var context = new AmbassadorContext<TRequest>(Name, request);
        try
        {
            foreach (var transform in _transforms)
            {
                context.Request = transform(context.Request);
                if (context.Request is null)
                    return AmbassadorResult<TResponse>.Failure(Name, new InvalidOperationException("Ambassador transform returned null."), context.Events.ToArray());
                context.Record("transform");
            }

            foreach (var step in _telemetry)
            {
                step.Execute(context);
                context.Record(step.Name);
            }

            if (!_connectionPolicy(context.Request))
                return InvokeFallback(context, new InvalidOperationException("Ambassador connection policy rejected the request."));

            var response = _call(context);
            if (response is null)
                return InvokeFallback(context, new InvalidOperationException("Ambassador outbound call returned null."));

            return AmbassadorResult<TResponse>.Success(Name, response, context.Events.ToArray());
        }
        catch (Exception ex)
        {
            return InvokeFallback(context, ex);
        }
    }

    public static Builder Create(string name = "ambassador") => new(name);

    private AmbassadorResult<TResponse> InvokeFallback(AmbassadorContext<TRequest> context, Exception exception)
    {
        if (_fallback is null)
            return AmbassadorResult<TResponse>.Failure(Name, exception, context.Events.ToArray());

        try
        {
            var response = _fallback(context);
            if (response is null)
                return AmbassadorResult<TResponse>.Failure(Name, new InvalidOperationException("Ambassador fallback returned null.", exception), context.Events.ToArray());

            context.Record("fallback");
            return AmbassadorResult<TResponse>.Success(Name, response, context.Events.ToArray(), usedFallback: true);
        }
        catch (Exception fallbackException)
        {
            return AmbassadorResult<TResponse>.Failure(Name, fallbackException, context.Events.ToArray());
        }
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Step> _telemetry = [];
        private readonly List<Func<TRequest, TRequest>> _transforms = [];
        private Func<TRequest, bool>? _connectionPolicy;
        private Func<AmbassadorContext<TRequest>, TResponse>? _call;
        private Func<AmbassadorContext<TRequest>, TResponse>? _fallback;

        internal Builder(string name) => _name = name;

        public Builder Transform(Func<TRequest, TRequest> transform)
        {
            _transforms.Add(transform ?? throw new ArgumentNullException(nameof(transform)));
            return this;
        }

        public Builder ConnectionPolicy(Func<TRequest, bool> connectionPolicy)
        {
            _connectionPolicy = connectionPolicy ?? throw new ArgumentNullException(nameof(connectionPolicy));
            return this;
        }

        public Builder Telemetry(string name, Action<AmbassadorContext<TRequest>> telemetry)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Telemetry name is required.", nameof(name));
            if (telemetry is null)
                throw new ArgumentNullException(nameof(telemetry));
            if (_telemetry.Any(step => string.Equals(step.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Ambassador telemetry step '{name}' is already registered.");

            _telemetry.Add(new(name, telemetry));
            return this;
        }

        public Builder Call(Func<AmbassadorContext<TRequest>, TResponse> call)
        {
            _call = call ?? throw new ArgumentNullException(nameof(call));
            return this;
        }

        public Builder Fallback(Func<AmbassadorContext<TRequest>, TResponse> fallback)
        {
            _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            return this;
        }

        public Ambassador<TRequest, TResponse> Build()
            => new(_name, _telemetry.ToArray(), _transforms.ToArray(), _connectionPolicy, _call, _fallback);
    }

    private sealed class Step
    {
        public Step(string name, Action<AmbassadorContext<TRequest>> execute)
            => (Name, Execute) = (name, execute);

        public string Name { get; }

        public Action<AmbassadorContext<TRequest>> Execute { get; }
    }
}
