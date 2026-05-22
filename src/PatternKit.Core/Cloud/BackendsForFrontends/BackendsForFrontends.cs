namespace PatternKit.Cloud.BackendsForFrontends;

public sealed class BackendsForFrontendsContext<TRequest>
{
    internal BackendsForFrontendsContext(string frontendName, TRequest request)
        => (FrontendName, Request) = (frontendName, request);

    public string FrontendName { get; }

    public TRequest Request { get; }

    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}

public sealed class BackendsForFrontendsResult<TResponse>
{
    private BackendsForFrontendsResult(string gatewayName, string? frontendName, TResponse? response, Exception? exception, bool handled)
        => (GatewayName, FrontendName, Response, Exception, Handled) = (gatewayName, frontendName, response, exception, handled);

    public string GatewayName { get; }

    public string? FrontendName { get; }

    public TResponse? Response { get; }

    public Exception? Exception { get; }

    public bool Handled { get; }

    public bool Failed => !Handled;

    public static BackendsForFrontendsResult<TResponse> Success(string gatewayName, string frontendName, TResponse response)
        => new(gatewayName, frontendName, response ?? throw new ArgumentNullException(nameof(response)), null, true);

    public static BackendsForFrontendsResult<TResponse> Failure(string gatewayName, string? frontendName, Exception exception)
        => new(gatewayName, frontendName, default, exception ?? throw new ArgumentNullException(nameof(exception)), false);
}

public sealed class BackendsForFrontends<TRequest, TResponse>
{
    private readonly IReadOnlyList<Frontend> _frontends;
    private readonly Func<BackendsForFrontendsContext<TRequest>, TResponse>? _fallback;

    private BackendsForFrontends(string name, IReadOnlyList<Frontend> frontends, Func<BackendsForFrontendsContext<TRequest>, TResponse>? fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Backends for Frontends name is required.", nameof(name));
        if (frontends is null)
            throw new ArgumentNullException(nameof(frontends));
        if (frontends.Count == 0)
            throw new InvalidOperationException("Backends for Frontends requires at least one frontend.");

        Name = name;
        _frontends = frontends;
        _fallback = fallback;
    }

    public string Name { get; }

    public BackendsForFrontendsResult<TResponse> Dispatch(TRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        foreach (var frontend in _frontends)
        {
            if (!frontend.Matches(request))
                continue;

            return Invoke(frontend.Name, request, frontend.Handle);
        }

        return _fallback is null
            ? BackendsForFrontendsResult<TResponse>.Failure(Name, null, new InvalidOperationException("No frontend matched the request."))
            : Invoke("fallback", request, _fallback);
    }

    public static Builder Create(string name = "backends-for-frontends") => new(name);

    private BackendsForFrontendsResult<TResponse> Invoke(string frontendName, TRequest request, Func<BackendsForFrontendsContext<TRequest>, TResponse> handler)
    {
        try
        {
            var response = handler(new(frontendName, request));
            if (response is null)
                return BackendsForFrontendsResult<TResponse>.Failure(Name, frontendName, new InvalidOperationException($"Frontend '{frontendName}' returned null."));

            return BackendsForFrontendsResult<TResponse>.Success(Name, frontendName, response);
        }
        catch (Exception ex)
        {
            return BackendsForFrontendsResult<TResponse>.Failure(Name, frontendName, ex);
        }
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Frontend> _frontends = [];
        private Func<BackendsForFrontendsContext<TRequest>, TResponse>? _fallback;

        internal Builder(string name) => _name = name;

        public Builder Frontend(string name, Func<TRequest, bool> match, Func<BackendsForFrontendsContext<TRequest>, TResponse> handle)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Frontend name is required.", nameof(name));
            if (match is null)
                throw new ArgumentNullException(nameof(match));
            if (handle is null)
                throw new ArgumentNullException(nameof(handle));
            if (_frontends.Any(frontend => string.Equals(frontend.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"Frontend '{name}' is already registered.");

            _frontends.Add(new(name, match, handle));
            return this;
        }

        public Builder Fallback(Func<BackendsForFrontendsContext<TRequest>, TResponse> handle)
        {
            _fallback = handle ?? throw new ArgumentNullException(nameof(handle));
            return this;
        }

        public BackendsForFrontends<TRequest, TResponse> Build()
            => new(_name, _frontends.ToArray(), _fallback);
    }

    private sealed class Frontend
    {
        public Frontend(string name, Func<TRequest, bool> matches, Func<BackendsForFrontendsContext<TRequest>, TResponse> handle)
            => (Name, Matches, Handle) = (name, matches, handle);

        public string Name { get; }

        public Func<TRequest, bool> Matches { get; }

        public Func<BackendsForFrontendsContext<TRequest>, TResponse> Handle { get; }
    }
}
