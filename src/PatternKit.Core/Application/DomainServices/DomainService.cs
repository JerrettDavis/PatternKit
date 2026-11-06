namespace PatternKit.Application.DomainServices;

/// <summary>
/// Stateless domain operation for behavior that does not naturally belong to a single entity or value object.
/// </summary>
public sealed class DomainServiceOperation<TRequest, TResponse>
{
    private readonly Func<TRequest, TResponse> _execute;

    private DomainServiceOperation(string name, Func<TRequest, TResponse> execute)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Operation name is required.", nameof(name))
            : name;
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public string Name { get; }

    public static DomainServiceOperation<TRequest, TResponse> Create(string name, Func<TRequest, TResponse> execute)
        => new(name, execute);

    public TResponse Execute(TRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        return _execute(request);
    }
}

/// <summary>
/// Named set of stateless domain service operations.
/// </summary>
public sealed class DomainServiceRegistry<TRequest, TResponse>
{
    private readonly IReadOnlyDictionary<string, DomainServiceOperation<TRequest, TResponse>> _operations;

    private DomainServiceRegistry(IReadOnlyDictionary<string, DomainServiceOperation<TRequest, TResponse>> operations)
    {
        _operations = operations;
    }

    public IReadOnlyList<string> Names => _operations.Keys.OrderBy(static name => name, StringComparer.Ordinal).ToArray();

    public static Builder Create() => new();

    public DomainServiceOperation<TRequest, TResponse> Get(string name)
    {
        if (!_operations.TryGetValue(name, out var operation))
            throw new KeyNotFoundException($"Domain service operation '{name}' was not registered.");

        return operation;
    }

    public TResponse Execute(string name, TRequest request)
        => Get(name).Execute(request);

    public sealed class Builder
    {
        private readonly Dictionary<string, DomainServiceOperation<TRequest, TResponse>> _operations = new(StringComparer.Ordinal);

        public Builder Add(string name, Func<TRequest, TResponse> execute)
            => Add(DomainServiceOperation<TRequest, TResponse>.Create(name, execute));

        public Builder Add(DomainServiceOperation<TRequest, TResponse> operation)
        {
            if (operation is null)
                throw new ArgumentNullException(nameof(operation));

            if (_operations.ContainsKey(operation.Name))
                throw new InvalidOperationException($"Domain service operation '{operation.Name}' is already registered.");

            _operations.Add(operation.Name, operation);
            return this;
        }

        public DomainServiceRegistry<TRequest, TResponse> Build()
            => new(new Dictionary<string, DomainServiceOperation<TRequest, TResponse>>(_operations, StringComparer.Ordinal));
    }
}
