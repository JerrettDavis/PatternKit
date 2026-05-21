namespace PatternKit.Application.ServiceLayer;

/// <summary>Application service operation exposed by a Service Layer boundary.</summary>
public interface IServiceOperation<TRequest, TResponse>
{
    string Name { get; }

    ValueTask<ServiceLayerResult<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Fluent Service Layer operation with preconditions and a typed handler.</summary>
public sealed class ServiceLayerOperation<TRequest, TResponse> : IServiceOperation<TRequest, TResponse>
{
    private readonly IReadOnlyList<ServiceLayerRule<TRequest>> _rules;
    private readonly Func<TRequest, CancellationToken, ValueTask<TResponse>> _handler;

    private ServiceLayerOperation(
        string name,
        IReadOnlyList<ServiceLayerRule<TRequest>> rules,
        Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)
    {
        Name = name;
        _rules = rules;
        _handler = handler;
    }

    public string Name { get; }

    public static Builder Create(string name)
        => new(name);

    public async ValueTask<ServiceLayerResult<TResponse>> ExecuteAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        cancellationToken.ThrowIfCancellationRequested();
        foreach (var rule in _rules)
        {
            if (!rule.Predicate(request))
                return ServiceLayerResult<TResponse>.Rejected(rule.Code, rule.Message);
        }

        try
        {
            return ServiceLayerResult<TResponse>.Completed(await _handler(request, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ServiceLayerResult<TResponse>.Failed(ex);
        }
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<ServiceLayerRule<TRequest>> _rules = new();
        private Func<TRequest, CancellationToken, ValueTask<TResponse>>? _handler;

        internal Builder(string name)
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Service Layer operation name is required.", nameof(name))
                : name;
        }

        public Builder Require(string code, string message, Func<TRequest, bool> predicate)
        {
            _rules.Add(new ServiceLayerRule<TRequest>(code, message, predicate));
            return this;
        }

        public Builder Handle(Func<TRequest, CancellationToken, ValueTask<TResponse>> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            return this;
        }

        public ServiceLayerOperation<TRequest, TResponse> Build()
            => new(_name, _rules.ToArray(), _handler ?? throw new InvalidOperationException("Service Layer operation handler is required."));
    }
}

/// <summary>Precondition rule used by a Service Layer operation.</summary>
public sealed class ServiceLayerRule<TRequest>
{
    public ServiceLayerRule(string code, string message, Func<TRequest, bool> predicate)
    {
        Code = string.IsNullOrWhiteSpace(code)
            ? throw new ArgumentException("Service Layer rule code is required.", nameof(code))
            : code;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Service Layer rule message is required.", nameof(message))
            : message;
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public string Code { get; }

    public string Message { get; }

    public Func<TRequest, bool> Predicate { get; }
}

/// <summary>Result returned by a Service Layer operation.</summary>
public sealed class ServiceLayerResult<TResponse>
{
    private ServiceLayerResult(TResponse? response, ServiceLayerStatus status, string? code, string? message, Exception? exception)
    {
        Response = response;
        Status = status;
        Code = code;
        Message = message;
        Exception = exception;
    }

    public TResponse? Response { get; }

    public ServiceLayerStatus Status { get; }

    public string? Code { get; }

    public string? Message { get; }

    public Exception? Exception { get; }

    public bool Succeeded => Status == ServiceLayerStatus.Completed;

    public static ServiceLayerResult<TResponse> Completed(TResponse response)
        => new(response, ServiceLayerStatus.Completed, null, null, null);

    public static ServiceLayerResult<TResponse> Rejected(string code, string message)
        => new(default, ServiceLayerStatus.Rejected, Validate(code, nameof(code)), Validate(message, nameof(message)), null);

    public static ServiceLayerResult<TResponse> Failed(Exception exception)
        => new(default, ServiceLayerStatus.Failed, null, null, exception ?? throw new ArgumentNullException(nameof(exception)));

    private static string Validate(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Service Layer result values are required.", parameterName)
            : value;
}

/// <summary>Execution status for a Service Layer operation.</summary>
public enum ServiceLayerStatus
{
    Completed,
    Rejected,
    Failed
}
