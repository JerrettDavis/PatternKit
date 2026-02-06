using System;

namespace PatternKit.Generators.Bridge;

/// <summary>
/// Marks an interface or abstract class as a Bridge Implementor contract.
/// The generator will use this to discover the implementor's members that should be
/// forwarded through the abstraction.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BridgeImplementorAttribute : Attribute
{
    /// <summary>
    /// Optional custom name for the generated implementor type reference.
    /// If not specified, the original type name is used.
    /// </summary>
    public string? ImplementorTypeName { get; set; }
}

/// <summary>
/// Marks a partial class as a Bridge Abstraction host.
/// The generator will produce a protected constructor accepting the implementor,
/// a protected Implementor property, and protected forwarding methods for all
/// implementor members (unless marked with <see cref="BridgeIgnoreAttribute"/>).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class BridgeAbstractionAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance with the implementor contract type.
    /// </summary>
    /// <param name="implementorType">The interface or abstract class that defines the implementor contract.</param>
    public BridgeAbstractionAttribute(Type implementorType)
    {
        ImplementorType = implementorType;
    }

    /// <summary>
    /// Gets the implementor contract type.
    /// </summary>
    public Type ImplementorType { get; }

    /// <summary>
    /// Name of the generated protected property that exposes the implementor.
    /// Default: "Implementor".
    /// </summary>
    public string ImplementorPropertyName { get; set; } = "Implementor";

    /// <summary>
    /// Whether to generate a default concrete implementor class.
    /// Default: false.
    /// </summary>
    public bool GenerateDefault { get; set; }

    /// <summary>
    /// Fully qualified or simple type name for the generated default implementor.
    /// Only used when <see cref="GenerateDefault"/> is true.
    /// </summary>
    public string? DefaultTypeName { get; set; }
}

/// <summary>
/// Marks a member on the implementor contract that should be excluded from
/// forwarding in the generated bridge abstraction.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class BridgeIgnoreAttribute : Attribute
{
}
