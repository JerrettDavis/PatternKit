namespace PatternKit.Generators.Facade;

/// <summary>
/// Marks a method in a host class to be exposed as a facade operation.
/// Used in host-first approach where methods define the facade surface,
/// and the generator creates a facade type with instance methods.
/// </summary>
/// <remarks>
/// Host methods should typically:
/// - Be static (for static hosts)
/// - Accept subsystem dependencies as first parameters
/// - Return the operation result
/// 
/// Generated facade methods will be instance methods on the facade type,
/// with dependencies injected via constructor.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FacadeExposeAttribute : Attribute
{
    /// <summary>
    /// Optional: custom name for the exposed method in the facade.
    /// If null, uses the host method name.
    /// </summary>
    public string? MethodName { get; set; }
}
