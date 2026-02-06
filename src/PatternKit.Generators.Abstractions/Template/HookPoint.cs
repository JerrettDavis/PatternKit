namespace PatternKit.Generators.Template;

/// <summary>
/// Defines the lifecycle points where hooks can be invoked in a template method workflow.
/// </summary>
public enum HookPoint
{
    /// <summary>
    /// Invoked before any steps execute.
    /// </summary>
    BeforeAll = 0,

    /// <summary>
    /// Invoked after all steps complete successfully.
    /// </summary>
    AfterAll = 1,

    /// <summary>
    /// Invoked when any step throws an exception.
    /// Receives the exception as a parameter.
    /// </summary>
    OnError = 2
}
