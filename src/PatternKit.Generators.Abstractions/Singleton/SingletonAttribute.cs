using System;

namespace PatternKit.Generators.Singleton;

/// <summary>
/// Specifies whether the singleton instance is created eagerly at class load time
/// or lazily on first access.
/// </summary>
public enum SingletonMode
{
    /// <summary>
    /// Instance is created when the type is first loaded (static field initializer).
    /// Simplest and fastest; no synchronization overhead on access.
    /// </summary>
    Eager = 0,

    /// <summary>
    /// Instance is created on first access via <see cref="System.Lazy{T}"/>.
    /// Useful when construction is expensive and the singleton may never be used.
    /// </summary>
    Lazy = 1
}

/// <summary>
/// Specifies the threading model for lazy singleton construction.
/// Only meaningful when <see cref="SingletonMode"/> is <see cref="SingletonMode.Lazy"/>.
/// </summary>
public enum SingletonThreading
{
    /// <summary>
    /// Uses <see cref="System.Threading.LazyThreadSafetyMode.ExecutionAndPublication"/>
    /// to ensure exactly one instance is created even under concurrent access.
    /// This is the default and recommended setting.
    /// </summary>
    ThreadSafe = 0,

    /// <summary>
    /// Uses <see cref="System.Threading.LazyThreadSafetyMode.None"/> for maximum performance
    /// when the caller guarantees single-threaded access during initialization.
    /// </summary>
    SingleThreadedFast = 1
}

/// <summary>
/// Marks a partial class for Singleton pattern code generation.
/// The generator produces a static <c>Instance</c> property (or custom name)
/// that provides a single shared instance of the decorated type.
/// </summary>
/// <remarks>
/// <para>
/// The decorated type must be a <c>partial class</c> with either a parameterless
/// constructor or a static method marked with <see cref="SingletonFactoryAttribute"/>.
/// </para>
/// <para>
/// Generated file: <c>{TypeName}.Singleton.g.cs</c>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class SingletonAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the singleton creation mode.
    /// Default: <see cref="SingletonMode.Eager"/>.
    /// </summary>
    public SingletonMode Mode { get; set; } = SingletonMode.Eager;

    /// <summary>
    /// Gets or sets the threading model for lazy initialization.
    /// Ignored when <see cref="Mode"/> is <see cref="SingletonMode.Eager"/>.
    /// Default: <see cref="SingletonThreading.ThreadSafe"/>.
    /// </summary>
    public SingletonThreading Threading { get; set; } = SingletonThreading.ThreadSafe;

    /// <summary>
    /// Gets or sets the name of the generated static property.
    /// Default: <c>"Instance"</c>.
    /// </summary>
    public string InstancePropertyName { get; set; } = "Instance";
}

/// <summary>
/// Marks a static method as the factory for singleton instance creation.
/// The method must be parameterless and return the declaring type.
/// When present, the generator uses this method instead of <c>new T()</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SingletonFactoryAttribute : Attribute
{
}
