namespace PatternKit.Generators.Composer;

/// <summary>
/// Marks a partial type as a composer pipeline host that will generate deterministic
/// composition of ordered components into a single executable pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class ComposerAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the generated synchronous invoke method.
    /// Default is "Invoke".
    /// </summary>
    public string InvokeMethodName { get; set; } = "Invoke";

    /// <summary>
    /// Gets or sets the name of the generated asynchronous invoke method.
    /// Default is "InvokeAsync".
    /// </summary>
    public string InvokeAsyncMethodName { get; set; } = "InvokeAsync";

    /// <summary>
    /// Gets or sets whether to generate async methods.
    /// When null (default), async generation is inferred from the presence of async steps or terminal.
    /// </summary>
    public bool? GenerateAsync { get; set; }

    /// <summary>
    /// Gets or sets whether to force async generation even if all steps are synchronous.
    /// Default is false.
    /// </summary>
    public bool ForceAsync { get; set; }

    /// <summary>
    /// Gets or sets the wrapping order for pipeline steps.
    /// Default is OuterFirst (step with Order=0 is outermost).
    /// </summary>
    public ComposerWrapOrder WrapOrder { get; set; } = ComposerWrapOrder.OuterFirst;
}

/// <summary>
/// Defines the order in which pipeline steps wrap each other.
/// </summary>
public enum ComposerWrapOrder
{
    /// <summary>
    /// Steps with lower Order values wrap steps with higher Order values.
    /// Order=0 is the outermost wrapper (executes first).
    /// </summary>
    OuterFirst = 0,

    /// <summary>
    /// Steps with higher Order values wrap steps with lower Order values.
    /// Order=0 is the innermost wrapper (executes last, closest to terminal).
    /// </summary>
    InnerFirst = 1
}
