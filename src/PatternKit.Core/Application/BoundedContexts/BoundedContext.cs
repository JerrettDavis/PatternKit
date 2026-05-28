namespace PatternKit.Application.BoundedContexts;

/// <summary>
/// Capability owned by a bounded context.
/// </summary>
public sealed record BoundedContextCapability
{
    public BoundedContextCapability(string name, Type serviceType)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Capability name is required.", nameof(name)) : name;
        ServiceType = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
    }

    public string Name { get; }

    public Type ServiceType { get; }
}

/// <summary>
/// Translation boundary between two bounded contexts.
/// </summary>
public sealed record BoundedContextAdapter
{
    public BoundedContextAdapter(string upstreamContext, string downstreamContext, Type sourceType, Type targetType)
    {
        UpstreamContext = string.IsNullOrWhiteSpace(upstreamContext)
            ? throw new ArgumentException("Upstream context is required.", nameof(upstreamContext))
            : upstreamContext;
        DownstreamContext = string.IsNullOrWhiteSpace(downstreamContext)
            ? throw new ArgumentException("Downstream context is required.", nameof(downstreamContext))
            : downstreamContext;
        SourceType = sourceType ?? throw new ArgumentNullException(nameof(sourceType));
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }

    public string UpstreamContext { get; }

    public string DownstreamContext { get; }

    public Type SourceType { get; }

    public Type TargetType { get; }
}

/// <summary>
/// Explicit description of a domain boundary and the integration contracts it owns.
/// </summary>
public sealed class BoundedContextDescriptor
{
    private BoundedContextDescriptor(string name, IReadOnlyList<BoundedContextCapability> capabilities, IReadOnlyList<BoundedContextAdapter> adapters)
    {
        Name = name;
        Capabilities = capabilities;
        Adapters = adapters;
    }

    public string Name { get; }

    public IReadOnlyList<BoundedContextCapability> Capabilities { get; }

    public IReadOnlyList<BoundedContextAdapter> Adapters { get; }

    public static Builder Create(string name) => new(name);

    public sealed class Builder
    {
        private readonly List<BoundedContextCapability> _capabilities = [];
        private readonly List<BoundedContextAdapter> _adapters = [];

        internal Builder(string name)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Context name is required.", nameof(name))
                : name;
        }

        public string Name { get; }

        public Builder AddCapability(string name, Type serviceType)
            => AddCapability(new BoundedContextCapability(name, serviceType));

        public Builder AddCapability(BoundedContextCapability capability)
        {
            if (capability is null)
                throw new ArgumentNullException(nameof(capability));

            if (_capabilities.Any(existing => string.Equals(existing.Name, capability.Name, StringComparison.Ordinal)))
                throw new InvalidOperationException($"Capability '{capability.Name}' is already registered for bounded context '{Name}'.");

            _capabilities.Add(capability);
            return this;
        }

        public Builder AddAdapter(string upstreamContext, string downstreamContext, Type sourceType, Type targetType)
            => AddAdapter(new BoundedContextAdapter(upstreamContext, downstreamContext, sourceType, targetType));

        public Builder AddAdapter(BoundedContextAdapter adapter)
        {
            if (adapter is null)
                throw new ArgumentNullException(nameof(adapter));

            if (_adapters.Any(existing =>
                string.Equals(existing.UpstreamContext, adapter.UpstreamContext, StringComparison.Ordinal)
                && string.Equals(existing.DownstreamContext, adapter.DownstreamContext, StringComparison.Ordinal)
                && existing.SourceType == adapter.SourceType
                && existing.TargetType == adapter.TargetType))
            {
                throw new InvalidOperationException(
                    $"Adapter '{adapter.UpstreamContext}->{adapter.DownstreamContext}' for '{adapter.SourceType.Name}' is already registered.");
            }

            _adapters.Add(adapter);
            return this;
        }

        public BoundedContextDescriptor Build()
            => new(
                Name,
                _capabilities.OrderBy(static capability => capability.Name, StringComparer.Ordinal).ToArray(),
                _adapters
                    .OrderBy(static adapter => adapter.UpstreamContext, StringComparer.Ordinal)
                    .ThenBy(static adapter => adapter.DownstreamContext, StringComparer.Ordinal)
                    .ThenBy(static adapter => adapter.SourceType.FullName, StringComparer.Ordinal)
                    .ToArray());
    }
}
