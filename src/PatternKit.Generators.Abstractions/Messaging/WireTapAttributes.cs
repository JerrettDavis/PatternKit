using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed wire-tap factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateWireTapAttribute : Attribute
{
    /// <summary>Creates a wire-tap generator attribute.</summary>
    public GenerateWireTapAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    /// <summary>Payload type observed by the generated wire tap.</summary>
    public Type PayloadType { get; }

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name assigned to the generated wire tap.</summary>
    public string TapName { get; set; } = "wire-tap";
}

/// <summary>
/// Marks a static method as a generated wire-tap handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class WireTapHandlerAttribute : Attribute
{
    /// <summary>Creates a wire-tap handler attribute.</summary>
    public WireTapHandlerAttribute(string name, int order)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Wire tap handler name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
        Order = order;
    }

    /// <summary>Tap handler name used in result metadata and diagnostics.</summary>
    public string Name { get; }

    /// <summary>Tap order in the generated wire tap.</summary>
    public int Order { get; }
}
