namespace PatternKit.Generators.AntiCorruption;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateAntiCorruptionLayerAttribute(Type externalType, Type domainType) : Attribute
{
    public Type ExternalType { get; } = externalType ?? throw new ArgumentNullException(nameof(externalType));
    public Type DomainType { get; } = domainType ?? throw new ArgumentNullException(nameof(domainType));
    public string FactoryMethodName { get; set; } = "Create";
    public string LayerName { get; set; } = "anti-corruption-layer";
    public string SourceSystem { get; set; } = "external";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class AntiCorruptionTranslatorAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AntiCorruptionExternalRuleAttribute(string rejectionReason) : Attribute
{
    public string RejectionReason { get; } = string.IsNullOrWhiteSpace(rejectionReason)
        ? throw new ArgumentException("Rejection reason is required.", nameof(rejectionReason))
        : rejectionReason;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class AntiCorruptionDomainRuleAttribute(string rejectionReason) : Attribute
{
    public string RejectionReason { get; } = string.IsNullOrWhiteSpace(rejectionReason)
        ? throw new ArgumentException("Rejection reason is required.", nameof(rejectionReason))
        : rejectionReason;
}
