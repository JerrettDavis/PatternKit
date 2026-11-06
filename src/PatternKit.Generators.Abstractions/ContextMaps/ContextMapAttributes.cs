namespace PatternKit.Generators.ContextMaps;

public enum ContextMapRelationshipKind
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

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateContextMapDescriptorAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Context map name is required.", nameof(name))
        : name;

    public string FactoryMethodName { get; set; } = "Create";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class ContextMapRelationshipAttribute(
    string upstreamContext,
    string downstreamContext,
    ContextMapRelationshipKind kind,
    string contractName) : Attribute
{
    public string UpstreamContext { get; } = string.IsNullOrWhiteSpace(upstreamContext)
        ? throw new ArgumentException("Upstream context is required.", nameof(upstreamContext))
        : upstreamContext;

    public string DownstreamContext { get; } = string.IsNullOrWhiteSpace(downstreamContext)
        ? throw new ArgumentException("Downstream context is required.", nameof(downstreamContext))
        : downstreamContext;

    public ContextMapRelationshipKind Kind { get; } = kind;

    public string ContractName { get; } = string.IsNullOrWhiteSpace(contractName)
        ? throw new ArgumentException("Contract name is required.", nameof(contractName))
        : contractName;
}
