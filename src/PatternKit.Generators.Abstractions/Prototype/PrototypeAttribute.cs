namespace PatternKit.Generators.Prototype;

/// <summary>
/// Marks a type (class/struct/record class/record struct) for Prototype pattern code generation.
/// Generates a Clone() method with configurable cloning strategies for safe, deterministic object duplication.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class PrototypeAttribute : Attribute
{
    /// <summary>
    /// The cloning mode that determines default behavior for reference types.
    /// Default is ShallowWithWarnings (safe-by-default with diagnostics for mutable references).
    /// </summary>
    public PrototypeMode Mode { get; set; } = PrototypeMode.ShallowWithWarnings;

    /// <summary>
    /// The name of the generated clone method.
    /// Default is "Clone".
    /// </summary>
    public string CloneMethodName { get; set; } = "Clone";

    /// <summary>
    /// When true, only members explicitly marked with [PrototypeInclude] will be cloned.
    /// When false (default), all eligible members are cloned unless marked with [PrototypeIgnore].
    /// </summary>
    public bool IncludeExplicit { get; set; }
}

/// <summary>
/// Determines the default cloning behavior for reference types.
/// </summary>
public enum PrototypeMode
{
    /// <summary>
    /// Shallow clone with warnings for mutable reference types.
    /// This is the safe-by-default mode that alerts developers to potential aliasing issues.
    /// </summary>
    ShallowWithWarnings = 0,

    /// <summary>
    /// Shallow clone without warnings.
    /// Use when you explicitly want shallow cloning and understand the implications.
    /// </summary>
    Shallow = 1,

    /// <summary>
    /// Deep clone when deterministically possible.
    /// Attempts to deep clone collections and types with known clone mechanisms.
    /// Falls back to shallow for types without clone support.
    /// </summary>
    DeepWhenPossible = 2
}

/// <summary>
/// Marks a member to be excluded from the clone operation.
/// Only applies when IncludeExplicit is false (default mode).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class PrototypeIgnoreAttribute : Attribute
{
}

/// <summary>
/// Marks a member to be explicitly included in the clone operation.
/// Only applies when IncludeExplicit is true.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class PrototypeIncludeAttribute : Attribute
{
}

/// <summary>
/// Specifies the cloning strategy for a specific member.
/// Overrides the default strategy determined by the PrototypeMode.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class PrototypeStrategyAttribute : Attribute
{
    public PrototypeCloneStrategy Strategy { get; }

    public PrototypeStrategyAttribute(PrototypeCloneStrategy strategy)
    {
        Strategy = strategy;
    }
}

/// <summary>
/// Defines how a member's value is cloned.
/// </summary>
public enum PrototypeCloneStrategy
{
    /// <summary>
    /// Copy the reference as-is (shallow copy).
    /// Safe for immutable types and value types.
    /// WARNING: For mutable reference types, mutations will affect both the original and clone.
    /// </summary>
    ByReference = 0,

    /// <summary>
    /// Perform a shallow copy of the member.
    /// For collections, creates a new collection with the same element references.
    /// </summary>
    ShallowCopy = 1,

    /// <summary>
    /// Clone the value using a known mechanism:
    /// - ICloneable.Clone()
    /// - Clone() method returning same type
    /// - Copy constructor T(T other)
    /// - For collections like List&lt;T&gt;, creates new collection: new List&lt;T&gt;(original)
    /// Generator emits an error if no suitable clone mechanism is available.
    /// </summary>
    Clone = 2,

    /// <summary>
    /// Perform a deep copy of the member value.
    /// Only available when the generator can safely emit deep copy logic.
    /// For complex types, requires Clone strategy on nested members.
    /// </summary>
    DeepCopy = 3,

    /// <summary>
    /// Use a custom clone mechanism provided by the user.
    /// Requires a partial method: private static partial TMember Clone{MemberName}(TMember value);
    /// </summary>
    Custom = 4
}
