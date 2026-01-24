namespace PatternKit.Generators.Facade;

/// <summary>
/// Identifies the implementation method that backs a facade contract member.
/// Used in contract-first approach where a facade interface/class defines the surface
/// and separate methods provide the actual implementation.
/// </summary>
/// <remarks>
/// The method signature must match the contract member it implements:
/// - Same return type (or compatible async type)
/// - Same parameters (order, type, modifiers)
/// - Can have additional dependency parameters for subsystems
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FacadeMapAttribute : Attribute
{
    /// <summary>
    /// Optional: explicit contract member name this method maps to.
    /// If null, mapping is inferred by signature matching.
    /// </summary>
    public string? MemberName { get; set; }
}
