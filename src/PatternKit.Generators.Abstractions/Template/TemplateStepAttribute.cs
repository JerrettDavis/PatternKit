namespace PatternKit.Generators.Template;

/// <summary>
/// Marks a method as a step in the template method workflow.
/// Steps execute in ascending order.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TemplateStepAttribute : Attribute
{
    /// <summary>
    /// Execution order of this step. Required. Steps execute in ascending order.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Optional name for diagnostics and documentation.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether this step is optional. Optional steps may be skipped in error scenarios.
    /// </summary>
    public bool Optional { get; set; }

    /// <summary>
    /// Initializes a new template step with the specified execution order.
    /// </summary>
    /// <param name="order">The execution order (lower values execute first).</param>
    public TemplateStepAttribute(int order)
    {
        Order = order;
    }
}
