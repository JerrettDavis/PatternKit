namespace PatternKit.Generators.BoundedContexts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateBoundedContextDescriptorAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Context name is required.", nameof(name))
        : name;

    public string FactoryMethodName { get; set; } = "Create";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class BoundedContextCapabilityAttribute(string name, Type serviceType) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Capability name is required.", nameof(name))
        : name;

    public Type ServiceType { get; } = serviceType ?? throw new ArgumentNullException(nameof(serviceType));
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class BoundedContextAdapterAttribute(
    string upstreamContext,
    string downstreamContext,
    Type sourceType,
    Type targetType) : Attribute
{
    public string UpstreamContext { get; } = string.IsNullOrWhiteSpace(upstreamContext)
        ? throw new ArgumentException("Upstream context is required.", nameof(upstreamContext))
        : upstreamContext;

    public string DownstreamContext { get; } = string.IsNullOrWhiteSpace(downstreamContext)
        ? throw new ArgumentException("Downstream context is required.", nameof(downstreamContext))
        : downstreamContext;

    public Type SourceType { get; } = sourceType ?? throw new ArgumentNullException(nameof(sourceType));

    public Type TargetType { get; } = targetType ?? throw new ArgumentNullException(nameof(targetType));
}
