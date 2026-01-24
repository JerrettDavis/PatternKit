namespace PatternKit.Generators.Facade;

/// <summary>
/// Specifies how the generator handles facade contract members without mappings.
/// </summary>
public enum FacadeMissingMapPolicy
{
    /// <summary>
    /// Emit a diagnostic error if any contract member lacks a mapping.
    /// This is the default and recommended behavior.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Generate a stub method that throws NotImplementedException.
    /// Useful for incremental development but not recommended for production.
    /// </summary>
    Stub = 1,

    /// <summary>
    /// Silently ignore unmapped members.
    /// Not recommended as it can lead to runtime errors.
    /// </summary>
    Ignore = 2
}
