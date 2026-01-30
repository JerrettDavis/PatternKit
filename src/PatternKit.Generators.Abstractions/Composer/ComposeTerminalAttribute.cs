namespace PatternKit.Generators.Composer;

/// <summary>
/// Marks a method as the terminal step of the pipeline.
/// The terminal is the final step that produces the output without calling a 'next' delegate.
/// A pipeline must have exactly one terminal.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ComposeTerminalAttribute : Attribute
{
}
