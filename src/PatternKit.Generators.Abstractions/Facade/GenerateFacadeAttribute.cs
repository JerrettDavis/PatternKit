namespace PatternKit.Generators.Facade;

/// <summary>
/// Marks a type for Facade pattern code generation.
/// Can be applied to:
/// - Partial interface/class (contract-first): defines the facade surface to be implemented
/// - Static partial class (host-first): contains methods to expose as facade operations
/// - Partial interface/class (auto-facade): auto-generate members from external type
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
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

    /// <summary>
    /// Fully qualified name of external type to facade (e.g., "Microsoft.Extensions.Logging.ILogger").
    /// When specified, enables Auto-Facade mode.
    /// </summary>
    public string? TargetTypeName { get; set; }

    /// <summary>
    /// Member names to include (null = include all). Mutually exclusive with Exclude.
    /// </summary>
    public string[]? Include { get; set; }

    /// <summary>
    /// Member names to exclude (null = exclude none). Mutually exclusive with Include.
    /// </summary>
    public string[]? Exclude { get; set; }

    /// <summary>
    /// Prefix for generated member names (default: none).
    /// Useful when applying multiple [GenerateFacade] attributes.
    /// </summary>
    public string? MemberPrefix { get; set; }

    /// <summary>
    /// Field name for the backing instance (default: "_target" or "_target{N}" for multiple attributes).
    /// </summary>
    public string? FieldName { get; set; }
}
