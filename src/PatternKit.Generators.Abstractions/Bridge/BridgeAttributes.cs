namespace PatternKit.Generators.Bridge;

/// <summary>
/// Marks an interface or abstract class as a Bridge implementor contract.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
public sealed class BridgeImplementorAttribute : Attribute
{
    public string? ImplementorTypeName { get; set; }
}

/// <summary>
/// Marks a partial class as a Bridge abstraction that forwards protected members to an implementor.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BridgeAbstractionAttribute : Attribute
{
    public BridgeAbstractionAttribute(Type implementorType)
    {
        ImplementorType = implementorType;
    }

    public Type ImplementorType { get; }

    public string ImplementorPropertyName { get; set; } = "Implementor";

    public bool GenerateDefault { get; set; }

    public string? DefaultTypeName { get; set; }
}

/// <summary>
/// Excludes an implementor member from generated Bridge forwarding.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false)]
public sealed class BridgeIgnoreAttribute : Attribute;
