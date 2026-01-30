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
    /// After invoking OnError hook (if present), continue execution with remaining steps.
    /// Only allowed when all remaining steps are optional.
    /// </summary>
    HandleAndContinue = 1
}
