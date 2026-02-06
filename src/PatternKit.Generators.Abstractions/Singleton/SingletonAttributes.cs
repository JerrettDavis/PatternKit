namespace PatternKit.Generators.Singleton;

/// <summary>
/// Marks a partial class or record class for Singleton pattern code generation.
/// Generates a thread-safe singleton instance accessor with configurable initialization mode.
/// </summary>
/// <remarks>
/// The generator supports two initialization modes:
/// <list type="bullet">
/// <item><description>Eager: Instance is created when the type is first accessed (static field initializer)</description></item>
/// <item><description>Lazy: Instance is created on first access to the Instance property</description></item>
/// </list>
/// For Lazy mode, thread-safety is configurable via the <see cref="Threading"/> property.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SingletonAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the singleton initialization mode.
    /// Default is <see cref="SingletonMode.Eager"/>.
    /// </summary>
    public SingletonMode Mode { get; set; } = SingletonMode.Eager;

    /// <summary>
    /// Gets or sets the threading model for singleton access.
    /// Default is <see cref="SingletonThreading.ThreadSafe"/>.
    /// Only applies when <see cref="Mode"/> is <see cref="SingletonMode.Lazy"/>.
    /// </summary>
    public SingletonThreading Threading { get; set; } = SingletonThreading.ThreadSafe;

    /// <summary>
    /// Gets or sets the name of the generated singleton instance property.
    /// Default is "Instance".
    /// </summary>
    public string InstancePropertyName { get; set; } = "Instance";
}

/// <summary>
/// Specifies when the singleton instance is created.
/// </summary>
public enum SingletonMode
{
    /// <summary>
    /// Instance is created when the type is first accessed.
    /// Uses a static field initializer for simple, thread-safe initialization.
    /// </summary>
    Eager = 0,

    /// <summary>
    /// Instance is created on first access to the Instance property.
    /// Uses <see cref="System.Lazy{T}"/> for thread-safe lazy initialization.
    /// </summary>
    Lazy = 1
}

/// <summary>
/// Specifies the threading model for singleton instance access.
/// </summary>
public enum SingletonThreading
{
    /// <summary>
    /// Thread-safe singleton access using locks or Lazy&lt;T&gt;.
    /// Recommended for most scenarios.
    /// </summary>
    ThreadSafe = 0,

    /// <summary>
    /// No thread synchronization. Faster but only safe in single-threaded scenarios.
    /// WARNING: May result in multiple instance creation if accessed from multiple threads.
    /// </summary>
    SingleThreadedFast = 1
}

/// <summary>
/// Marks a static method as the factory for creating the singleton instance.
/// The method must be static, parameterless, and return the containing type.
/// </summary>
/// <remarks>
/// Use this when the singleton requires custom initialization logic
/// beyond what a parameterless constructor can provide.
/// Only one method in a type may be marked with this attribute.
/// </remarks>
/// <example>
/// <code>
/// [Singleton(Mode = SingletonMode.Lazy)]
/// public partial class ConfigManager
/// {
///     private ConfigManager(string path) { }
///
///     [SingletonFactory]
///     private static ConfigManager Create() => new ConfigManager("config.json");
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SingletonFactoryAttribute : Attribute
{
}
