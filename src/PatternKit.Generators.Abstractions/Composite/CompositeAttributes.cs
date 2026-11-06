namespace PatternKit.Generators.Composite;

/// <summary>
/// Marks a component contract for Composite pattern base type generation.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
public sealed class CompositeComponentAttribute : Attribute
{
    public string? ComponentBaseName { get; set; }

    public string? CompositeBaseName { get; set; }

    public string ChildrenPropertyName { get; set; } = "Children";

    public CompositeChildrenStorage Storage { get; set; } = CompositeChildrenStorage.List;

    public bool GenerateTraversalHelpers { get; set; }
}

/// <summary>
/// Excludes a contract member from generated Composite base types.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, Inherited = false)]
public sealed class CompositeIgnoreAttribute : Attribute;

public enum CompositeChildrenStorage
{
    List = 0,
    ImmutableArray = 1
}
