namespace PatternKit.Messaging.PipesAndFilters;

public sealed class PipelineFilterResult
{
    public PipelineFilterResult(string name, bool succeeded)
    {
        Name = name;
        Succeeded = succeeded;
    }

    public string Name { get; }

    public bool Succeeded { get; }
}

public sealed class PipesAndFiltersResult<TContext>
{
    public PipesAndFiltersResult(TContext value, bool succeeded, IReadOnlyList<PipelineFilterResult> filters)
    {
        Value = value;
        Succeeded = succeeded;
        Filters = filters;
    }

    public TContext Value { get; }

    public bool Succeeded { get; }

    public IReadOnlyList<PipelineFilterResult> Filters { get; }
}

public sealed class PipesAndFiltersPipeline<TContext>
{
    private readonly IReadOnlyList<PipelineFilter<TContext>> _filters;

    private PipesAndFiltersPipeline(string name, IReadOnlyList<PipelineFilter<TContext>> filters)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Pipes and Filters pipeline name is required.", nameof(name));
        if (filters.Count == 0)
            throw new ArgumentException("At least one pipeline filter is required.", nameof(filters));

        Name = name;
        _filters = filters;
    }

    public string Name { get; }

    public int FilterCount => _filters.Count;

    public static Builder Create(string name = "pipes-and-filters") => new(name);

    public async ValueTask<PipesAndFiltersResult<TContext>> ExecuteAsync(TContext input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = input;
        var executed = new List<PipelineFilterResult>(_filters.Count);

        foreach (var filter in _filters)
        {
            current = await filter.ApplyAsync(current, cancellationToken).ConfigureAwait(false);
            executed.Add(new PipelineFilterResult(filter.Name, true));
        }

        return new PipesAndFiltersResult<TContext>(current, true, executed);
    }

    public sealed class Builder
    {
        private readonly string _name;
        private readonly List<PipelineFilter<TContext>> _filters = [];

        internal Builder(string name) => _name = name;

        public Builder AddFilter(string name, Func<TContext, CancellationToken, ValueTask<TContext>> filter)
        {
            _filters.Add(new PipelineFilter<TContext>(name, filter));
            return this;
        }

        public PipesAndFiltersPipeline<TContext> Build()
            => new(_name, _filters.ToArray());
    }
}

public sealed class PipelineFilter<TContext>
{
    public PipelineFilter(string name, Func<TContext, CancellationToken, ValueTask<TContext>> applyAsync)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Pipeline filter name is required.", nameof(name));

        Name = name;
        ApplyAsync = applyAsync ?? throw new ArgumentNullException(nameof(applyAsync));
    }

    public string Name { get; }

    public Func<TContext, CancellationToken, ValueTask<TContext>> ApplyAsync { get; }
}
