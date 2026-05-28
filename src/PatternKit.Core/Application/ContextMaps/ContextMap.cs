namespace PatternKit.Application.ContextMaps;

/// <summary>
/// Relationship style between bounded contexts.
/// </summary>
public enum ContextRelationshipKind
{
    Partnership,
    SharedKernel,
    CustomerSupplier,
    Conformist,
    AntiCorruptionLayer,
    OpenHostService,
    PublishedLanguage,
    SeparateWays
}

/// <summary>
/// Directed relationship between two bounded contexts.
/// </summary>
public sealed record ContextMapRelationship
{
    public ContextMapRelationship(string upstreamContext, string downstreamContext, ContextRelationshipKind kind, string contractName)
    {
        UpstreamContext = string.IsNullOrWhiteSpace(upstreamContext)
            ? throw new ArgumentException("Upstream context is required.", nameof(upstreamContext))
            : upstreamContext;
        DownstreamContext = string.IsNullOrWhiteSpace(downstreamContext)
            ? throw new ArgumentException("Downstream context is required.", nameof(downstreamContext))
            : downstreamContext;
        ContractName = string.IsNullOrWhiteSpace(contractName)
            ? throw new ArgumentException("Contract name is required.", nameof(contractName))
            : contractName;
        Kind = kind;
    }

    public string UpstreamContext { get; }

    public string DownstreamContext { get; }

    public ContextRelationshipKind Kind { get; }

    public string ContractName { get; }
}

/// <summary>
/// Explicit map of bounded-context relationships for integration and ownership reviews.
/// </summary>
public sealed class ContextMapDescriptor
{
    private ContextMapDescriptor(string name, IReadOnlyList<ContextMapRelationship> relationships)
    {
        Name = name;
        Relationships = relationships;
    }

    public string Name { get; }

    public IReadOnlyList<ContextMapRelationship> Relationships { get; }

    public static Builder Create(string name) => new(name);

    public sealed class Builder
    {
        private readonly List<ContextMapRelationship> _relationships = [];

        internal Builder(string name)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("Context map name is required.", nameof(name))
                : name;
        }

        public string Name { get; }

        public Builder AddRelationship(string upstreamContext, string downstreamContext, ContextRelationshipKind kind, string contractName)
            => AddRelationship(new ContextMapRelationship(upstreamContext, downstreamContext, kind, contractName));

        public Builder AddRelationship(ContextMapRelationship relationship)
        {
            if (relationship is null)
                throw new ArgumentNullException(nameof(relationship));

            if (_relationships.Any(existing =>
                string.Equals(existing.UpstreamContext, relationship.UpstreamContext, StringComparison.Ordinal)
                && string.Equals(existing.DownstreamContext, relationship.DownstreamContext, StringComparison.Ordinal)
                && string.Equals(existing.ContractName, relationship.ContractName, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Relationship '{relationship.UpstreamContext}->{relationship.DownstreamContext}' for '{relationship.ContractName}' is already registered.");
            }

            _relationships.Add(relationship);
            return this;
        }

        public ContextMapDescriptor Build()
            => new(
                Name,
                _relationships
                    .OrderBy(static relationship => relationship.UpstreamContext, StringComparer.Ordinal)
                    .ThenBy(static relationship => relationship.DownstreamContext, StringComparer.Ordinal)
                    .ThenBy(static relationship => relationship.ContractName, StringComparer.Ordinal)
                    .ToArray());
    }
}
