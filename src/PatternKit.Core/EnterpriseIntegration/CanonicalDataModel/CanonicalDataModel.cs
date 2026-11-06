namespace PatternKit.EnterpriseIntegration.CanonicalDataModel;

public sealed class CanonicalDataModelResult<TCanonical>
{
    private CanonicalDataModelResult(string modelName, string adapterName, TCanonical? value, Exception? exception, bool normalized)
        => (ModelName, AdapterName, Value, Exception, Normalized) = (modelName, adapterName, value, exception, normalized);

    public string ModelName { get; }

    public string AdapterName { get; }

    public TCanonical? Value { get; }

    public Exception? Exception { get; }

    public bool Normalized { get; }

    public bool Failed => !Normalized;

    public static CanonicalDataModelResult<TCanonical> Success(string modelName, string adapterName, TCanonical value)
        => new(modelName, adapterName, value, null, true);

    public static CanonicalDataModelResult<TCanonical> Failure(string modelName, string adapterName, Exception exception)
        => new(modelName, adapterName, default, exception ?? throw new ArgumentNullException(nameof(exception)), false);
}

public sealed class CanonicalDataModel<TCanonical>
{
    private readonly Dictionary<Type, Adapter> _adapters;

    private CanonicalDataModel(string name, IReadOnlyList<Adapter> adapters)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Canonical data model name is required.", nameof(name));
        if (adapters is null)
            throw new ArgumentNullException(nameof(adapters));
        if (adapters.Count == 0)
            throw new InvalidOperationException("Canonical data model requires at least one source adapter.");

        Name = name;
        _adapters = adapters.ToDictionary(static adapter => adapter.SourceType);
    }

    public string Name { get; }

    public IReadOnlyCollection<Type> SourceTypes => _adapters.Keys;

    public CanonicalDataModelResult<TCanonical> Normalize<TSource>(TSource source)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        if (!_adapters.TryGetValue(typeof(TSource), out var adapter))
            return CanonicalDataModelResult<TCanonical>.Failure(Name, typeof(TSource).Name, new InvalidOperationException($"No canonical data model adapter is registered for '{typeof(TSource).FullName}'."));

        try
        {
            var value = adapter.Normalize(source);
            if (value is null)
                return CanonicalDataModelResult<TCanonical>.Failure(Name, adapter.Name, new InvalidOperationException($"Canonical data model adapter '{adapter.Name}' returned null."));

            return CanonicalDataModelResult<TCanonical>.Success(Name, adapter.Name, value);
        }
        catch (Exception ex)
        {
            return CanonicalDataModelResult<TCanonical>.Failure(Name, adapter.Name, ex);
        }
    }

    public static Builder Create(string name = "canonical-data-model") => new(name);

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<Adapter> _adapters = [];

        internal Builder(string name) => _name = name;

        public Builder From<TSource>(string adapterName, Func<TSource, TCanonical> normalize)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
                throw new ArgumentException("Canonical adapter name is required.", nameof(adapterName));
            if (normalize is null)
                throw new ArgumentNullException(nameof(normalize));
            if (_adapters.Any(adapter => adapter.SourceType == typeof(TSource)))
                throw new InvalidOperationException($"Canonical adapter for '{typeof(TSource).FullName}' is already registered.");

            _adapters.Add(new(adapterName, typeof(TSource), source => normalize((TSource)source)));
            return this;
        }

        public CanonicalDataModel<TCanonical> Build() => new(_name, _adapters.ToArray());
    }

    private sealed class Adapter
    {
        public Adapter(string name, Type sourceType, Func<object, TCanonical> normalize)
            => (Name, SourceType, Normalize) = (name, sourceType, normalize);

        public string Name { get; }

        public Type SourceType { get; }

        public Func<object, TCanonical> Normalize { get; }
    }
}
