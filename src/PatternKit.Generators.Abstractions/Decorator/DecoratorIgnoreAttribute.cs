namespace PatternKit.Generators.Decorator;

/// <summary>
/// Marks a member that should not participate in decoration.
/// The generator will still emit a forwarding member in the decorator, but it will
/// strip <c>virtual</c>/<c>override</c> so the member is not decorated or overridden.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DecoratorIgnoreAttribute : Attribute
{
}
