namespace PatternKit.Generators.Composer;

/// <summary>
/// Marks a method as a pipeline step that will be composed into the pipeline.
/// Steps are ordered by the Order property and wrap each other according to the ComposerWrapOrder.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ComposeStepAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the order of this step in the pipeline.
    /// With OuterFirst (default): Lower Order values wrap higher Order values.
    ///   - Order=0 executes first, wrapping all other steps.
    /// With InnerFirst: Higher Order values wrap lower Order values.
    ///   - Order=0 executes last, closest to the terminal.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets an optional name for this step (for diagnostics and debugging).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComposeStepAttribute"/> class.
    /// </summary>
    /// <param name="order">The order of this step in the pipeline.</param>
    public ComposeStepAttribute(int order)
    {
        Order = order;
    }
}
