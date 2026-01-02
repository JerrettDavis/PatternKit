namespace PatternKit.Generators.Visitors;

/// <summary>
/// Marks a type hierarchy for visitor pattern generation. Apply this attribute to the base type
/// of a visitable hierarchy to generate visitor interfaces, Accept methods, and fluent builders.
/// </summary>
/// <remarks>
/// The generator produces:
/// <list type="bullet">
/// <item>Visitor interfaces for all four visitor types (sync/async, result/action)</item>
/// <item>Accept methods on all partial types in the hierarchy</item>
/// <item>Fluent builder APIs for composing visitors</item>
/// </list>
/// <example>
/// <code>
/// [GenerateVisitor]
/// public partial class AstNode { }
/// 
/// public partial class Expression : AstNode { }
/// public partial class Statement : AstNode { }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateVisitorAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the generated visitor interface. 
    /// Defaults to "I{BaseTypeName}Visitor" if not specified.
    /// </summary>
    public string? VisitorInterfaceName { get; set; }

    /// <summary>
    /// Gets or sets whether to generate async visitor variants.
    /// Default is true.
    /// </summary>
    public bool GenerateAsync { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to generate action visitor variants.
    /// Default is true.
    /// </summary>
    public bool GenerateActions { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically discover derived types in the same assembly.
    /// Default is true.
    /// </summary>
    public bool AutoDiscoverDerivedTypes { get; set; } = true;
}
