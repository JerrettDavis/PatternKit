using System;

namespace PatternKit.Generators.Composite;

/// <summary>
/// Marks an interface or abstract class as a Composite Component contract.
/// The generator will produce a ComponentBase (leaf defaults) and a CompositeBase
/// (child management and delegation) from the component's members.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CompositeComponentAttribute : Attribute
{
    /// <summary>
    /// Custom name for the generated leaf base class.
    /// Default: "{ComponentName}Base" (strips leading "I" for interfaces).
    /// </summary>
    public string? ComponentBaseName { get; set; }

    /// <summary>
    /// Custom name for the generated composite base class.
    /// Default: "{ComponentName}Composite" (strips leading "I" for interfaces).
    /// </summary>
    public string? CompositeBaseName { get; set; }

    /// <summary>
    /// Name of the property exposing the children collection on the composite.
    /// Default: "Children".
    /// </summary>
    public string ChildrenPropertyName { get; set; } = "Children";

    /// <summary>
    /// Storage strategy for the children collection.
    /// Default: <see cref="CompositeChildrenStorage.List"/>.
    /// </summary>
    public CompositeChildrenStorage Storage { get; set; } = CompositeChildrenStorage.List;

    /// <summary>
    /// Whether to generate traversal helper methods (depth-first, breadth-first).
    /// Default: false.
    /// </summary>
    public bool GenerateTraversalHelpers { get; set; }
}

/// <summary>
/// Marks a member on the component contract that should be excluded from
/// forwarding in the generated composite and leaf base classes.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class CompositeIgnoreAttribute : Attribute
{
}

/// <summary>
/// Specifies the backing collection type for composite children.
/// </summary>
public enum CompositeChildrenStorage
{
    /// <summary>Uses a <see cref="System.Collections.Generic.List{T}"/> for mutable child management.</summary>
    List = 0,

    /// <summary>Uses <see cref="System.Collections.Immutable.ImmutableArray{T}"/> for immutable child management.</summary>
    ImmutableArray = 1
}
