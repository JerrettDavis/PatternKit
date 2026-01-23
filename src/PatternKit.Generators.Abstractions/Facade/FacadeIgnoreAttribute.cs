namespace PatternKit.Generators.Facade;

/// <summary>
/// Excludes a contract member from facade generation.
/// Useful for inherited members or optional operations that don't need implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class FacadeIgnoreAttribute : Attribute
{
}
