namespace PatternKit.Generators.Decorator;

/// <summary>
/// Marks a member that should not participate in decoration.
/// The generator will still emit a forwarding member in the decorator. For concrete
/// members it strips <c>virtual</c>/<c>override</c> so the member is not decorated.
/// When required to satisfy an abstract or virtual member on an abstract base class,
/// it emits a <c>sealed override</c> instead; for interface contracts it emits a
/// non-virtual implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class DecoratorIgnoreAttribute : Attribute
{
}
