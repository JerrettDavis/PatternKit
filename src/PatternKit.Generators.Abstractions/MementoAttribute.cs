namespace PatternKit.Generators;

/// <summary>
/// Marks a type (class/struct/record class/record struct) for Memento pattern code generation.
/// Generates an immutable memento struct for capturing and restoring state snapshots,
/// with optional undo/redo caretaker history management.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class MementoAttribute : Attribute
{
    /// <summary>
    /// When true, generates a caretaker class for undo/redo history management.
    /// Default is false (generates only the memento struct).
    /// </summary>
    public bool GenerateCaretaker { get; set; }

    /// <summary>
    /// Maximum number of snapshots to retain in the caretaker history.
    /// When the limit is exceeded, the oldest snapshot is evicted (FIFO).
    /// Default is 0 (unbounded). Only applies when GenerateCaretaker is true.
    /// </summary>
    public int Capacity { get; set; }

    /// <summary>
    /// Member selection mode for the memento.
    /// Default is IncludeAll (all public instance properties/fields with getters).
    /// </summary>
    public MementoInclusionMode InclusionMode { get; set; } = MementoInclusionMode.IncludeAll;

    /// <summary>
    /// When true, the generated caretaker will skip capturing duplicate states
    /// (states that are equal according to value equality).
    /// Default is true. Only applies when GenerateCaretaker is true.
    /// </summary>
    public bool SkipDuplicates { get; set; } = true;
}

/// <summary>
/// Determines how members are selected for inclusion in the memento.
/// </summary>
public enum MementoInclusionMode
{
    /// <summary>
    /// Include all eligible public instance properties and fields with getters,
    /// except those marked with [MementoIgnore].
    /// </summary>
    IncludeAll = 0,

    /// <summary>
    /// Include only members explicitly marked with [MementoInclude].
    /// </summary>
    ExplicitOnly = 1
}

/// <summary>
/// Marks a member to be excluded from the generated memento.
/// Only applies when InclusionMode is IncludeAll.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class MementoIgnoreAttribute : Attribute
{
}

/// <summary>
/// Marks a member to be explicitly included in the generated memento.
/// Only applies when InclusionMode is ExplicitOnly.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class MementoIncludeAttribute : Attribute
{
}

/// <summary>
/// Specifies the capture strategy for a member in the memento.
/// Determines how the member value is copied when creating a snapshot.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class MementoStrategyAttribute : Attribute
{
    public MementoCaptureStrategy Strategy { get; }

    public MementoStrategyAttribute(MementoCaptureStrategy strategy)
    {
        Strategy = strategy;
    }
}

/// <summary>
/// Defines how a member's value is captured in the memento snapshot.
/// </summary>
public enum MementoCaptureStrategy
{
    /// <summary>
    /// Capture the reference as-is (shallow copy).
    /// Safe for immutable types and value types.
    /// WARNING: For mutable reference types, mutations will affect all snapshots.
    /// </summary>
    ByReference = 0,

    /// <summary>
    /// Clone the value using a known mechanism (ICloneable, record with-expression, etc.).
    /// Generator emits an error if no suitable clone mechanism is available.
    /// </summary>
    Clone = 1,

    /// <summary>
    /// Perform a deep copy of the member value.
    /// Only available when the generator can safely emit deep copy logic.
    /// </summary>
    DeepCopy = 2,

    /// <summary>
    /// Use a custom capture mechanism provided by the user.
    /// Requires the user to implement a partial method for custom capture logic.
    /// </summary>
    Custom = 3
}
