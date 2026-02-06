using System;

namespace PatternKit.Generators.Adapter;

/// <summary>
/// Specifies how the generator handles target interface members without explicit mapping methods.
/// </summary>
public enum AdapterMissingMapPolicy
{
    /// <summary>
    /// Emit a diagnostic error for any unmapped target member.
    /// This is the default and safest setting.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Generate a stub method that throws <see cref="NotImplementedException"/>.
    /// Useful during incremental development.
    /// </summary>
    ThrowingStub = 1
}

/// <summary>
/// Marks a static partial class as the host for adapter generation.
/// The generator creates an adapter class that implements <see cref="Target"/>
/// by delegating to an <see cref="Adaptee"/> instance via mapping methods defined in the host.
/// </summary>
/// <remarks>
/// <para>
/// Place <see cref="AdapterMapAttribute"/> on static methods in the host class
/// to define how each target member delegates to the adaptee.
/// </para>
/// <para>
/// Generated file: <c>{AdapterTypeName}.Adapter.g.cs</c>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class GenerateAdapterAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the target interface or abstract class that the adapter will implement.
    /// </summary>
    public Type Target { get; set; } = null!;

    /// <summary>
    /// Gets or sets the adaptee type whose functionality is being adapted.
    /// </summary>
    public Type Adaptee { get; set; } = null!;

    /// <summary>
    /// Gets or sets the name of the generated adapter class.
    /// Default: <c>"{AdapteeName}To{TargetName}Adapter"</c>.
    /// </summary>
    public string? AdapterTypeName { get; set; }

    /// <summary>
    /// Gets or sets how unmapped target members are handled.
    /// Default: <see cref="AdapterMissingMapPolicy.Error"/>.
    /// </summary>
    public AdapterMissingMapPolicy MissingMap { get; set; } = AdapterMissingMapPolicy.Error;
}

/// <summary>
/// Marks a static method as a mapping from a target interface member to adaptee logic.
/// The first parameter of the decorated method must be the adaptee type.
/// </summary>
/// <remarks>
/// If <see cref="TargetMember"/> is not specified, the generator matches by method name.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AdapterMapAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the target interface member this method maps to.
    /// If null, the generator matches by method name.
    /// </summary>
    public string? TargetMember { get; set; }
}
