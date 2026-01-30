namespace PatternKit.Generators.Composer;

/// <summary>
/// Marks a method to be excluded from pipeline composition.
/// Use this to explicitly exclude methods that might otherwise be considered for composition.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ComposeIgnoreAttribute : Attribute
{
}
