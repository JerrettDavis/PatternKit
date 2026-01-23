namespace PatternKit.Generators.Facade;

/// <summary>
/// Marks a type for Facade pattern code generation.
/// Can be applied to:
/// - Partial interface/class (contract-first): defines the facade surface to be implemented
/// - Static partial class (host-first): contains methods to expose as facade operations
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateFacadeAttribute : Attribute
{
    /// <summary>
    /// The name of the generated facade type. 
    /// For contract-first: implementation class name (default: {Name}Impl)
    /// For host-first: facade type name (default: {Name}Facade)
    /// </summary>
    public string? FacadeTypeName { get; set; }

    /// <summary>
    /// Whether to generate async variants for async methods.
    /// Default: inferred from method signatures (Task/ValueTask/CancellationToken)
    /// </summary>
    public bool GenerateAsync { get; set; } = true;

    /// <summary>
    /// Force all methods to be async even if they don't have async signatures.
    /// Default: false
    /// </summary>
    public bool ForceAsync { get; set; }

    /// <summary>
    /// How to handle contract members without mappings.
    /// Default: Error (emit diagnostic)
    /// </summary>
    public FacadeMissingMapPolicy MissingMap { get; set; } = FacadeMissingMapPolicy.Error;
}
