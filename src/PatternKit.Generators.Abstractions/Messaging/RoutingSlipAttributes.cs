using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates typed factory methods for a routing slip class.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateRoutingSlipAttribute : Attribute
{
    /// <summary>Creates a routing slip generator attribute.</summary>
    public GenerateRoutingSlipAttribute(Type payloadType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
    }

    /// <summary>Message payload type processed by generated routing slips.</summary>
    public Type PayloadType { get; }

    /// <summary>Name of the generated sync factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name of the generated async factory method.</summary>
    public string AsyncFactoryName { get; set; } = "CreateAsync";
}

/// <summary>
/// Marks a static method as a generated routing slip step.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class RoutingSlipStepAttribute : Attribute
{
    /// <summary>Creates a routing slip step attribute.</summary>
    public RoutingSlipStepAttribute(string name, int order)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Routing slip step name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
        Order = order;
    }

    /// <summary>Step name written into the routing slip itinerary.</summary>
    public string Name { get; }

    /// <summary>Step order in the generated routing slip itinerary.</summary>
    public int Order { get; }
}
