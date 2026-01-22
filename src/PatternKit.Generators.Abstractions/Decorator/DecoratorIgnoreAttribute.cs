namespace PatternKit.Generators.Decorator;

/// <summary>
/// Marks a member to be excluded from the generated decorator base class.
/// Use this to skip members that should not be forwarded by decorators.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DecoratorIgnoreAttribute : Attribute
{
}
