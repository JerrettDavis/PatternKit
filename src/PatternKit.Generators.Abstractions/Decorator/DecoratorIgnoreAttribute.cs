namespace PatternKit.Generators.Decorator;

/// <summary>
/// Marks a member that should not participate in decoration.
/// The generator will still emit a forwarding member in the decorator. For concrete
/// members it strips <c>virtual</c>/<c>override</c> so the member is not decorated,
/// but when required to satisfy an abstract or virtual contract it emits a
/// <c>sealed override</c> instead.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DecoratorIgnoreAttribute : Attribute
{
}
