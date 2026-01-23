namespace PatternKit.Generators.Decorator;

/// <summary>
/// Marks an interface or abstract class for Decorator pattern code generation.
/// Generates a base decorator class that forwards all members to an inner instance,
/// with optional composition helpers for building decorator chains.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateDecoratorAttribute : Attribute
{
    /// <summary>
    /// Name of the generated base decorator class.
    /// Default is {ContractName}DecoratorBase (e.g., IStorage -> StorageDecoratorBase).
    /// </summary>
    public string? BaseTypeName { get; set; }

    /// <summary>
    /// Name of the generated helpers/composition class.
    /// Default is {ContractName}Decorators (e.g., IStorage -> StorageDecorators).
    /// </summary>
    public string? HelpersTypeName { get; set; }

    /// <summary>
    /// Determines what composition helpers are generated.
    /// Default is HelpersOnly (generates a Compose method for chaining decorators).
    /// </summary>
    public DecoratorCompositionMode Composition { get; set; } = DecoratorCompositionMode.HelpersOnly;

    /// <summary>
    /// Reserved for future use.
    /// Currently ignored by the Decorator generator and has no effect on generated code.
    /// Default is false.
    /// </summary>
    public bool GenerateAsync { get; set; }

    /// <summary>
    /// Reserved for future use.
    /// Currently ignored by the Decorator generator and has no effect on generated code.
    /// Default is false.
    /// </summary>
    public bool ForceAsync { get; set; }
}

/// <summary>
/// Determines what composition utilities are generated with the decorator.
/// </summary>
public enum DecoratorCompositionMode
{
    /// <summary>
    /// Do not generate any composition helpers.
    /// </summary>
    None = 0,

    /// <summary>
    /// Generate a static Compose method for chaining decorators in order.
    /// Decorators are applied in array order (first element is outermost).
    /// </summary>
    HelpersOnly = 1,

    /// <summary>
    /// Generate pipeline-style composition with explicit "next" parameter.
    /// (Reserved for future use - v2 feature)
    /// </summary>
    PipelineNextStyle = 2
}
