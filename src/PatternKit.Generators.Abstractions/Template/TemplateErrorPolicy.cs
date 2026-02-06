namespace PatternKit.Generators.Template;

/// <summary>
/// Defines how errors are handled during template method execution.
/// </summary>
public enum TemplateErrorPolicy
{
    /// <summary>
    /// After invoking OnError hook (if present), rethrow the exception.
    /// This is the default behavior.
    /// </summary>
    Rethrow = 0,

    /// <summary>
    /// After invoking OnError hook (if present), suppress the exception (do not rethrow) and terminate the workflow.
    /// Remaining steps are not executed; use this only when all remaining steps are optional.
    /// </summary>
    HandleAndContinue = 1
}
