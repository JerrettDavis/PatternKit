namespace PatternKit.Generators.Template;

/// <summary>
/// Marks a method as a hook in the template method workflow.
/// Hooks execute at specific lifecycle points (BeforeAll, AfterAll, OnError).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class TemplateHookAttribute : Attribute
{
    /// <summary>
    /// The hook point where this method should be invoked.
    /// </summary>
    public HookPoint HookPoint { get; }

    /// <summary>
    /// Optional step order for future BeforeStep/AfterStep targeting (reserved for v2).
    /// </summary>
    public int? StepOrder { get; set; }

    /// <summary>
    /// Initializes a new template hook with the specified hook point.
    /// </summary>
    /// <param name="hookPoint">When this hook should be invoked.</param>
    public TemplateHookAttribute(HookPoint hookPoint)
    {
        HookPoint = hookPoint;
    }
}
