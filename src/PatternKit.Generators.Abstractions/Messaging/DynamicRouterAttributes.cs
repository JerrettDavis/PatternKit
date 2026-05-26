using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed dynamic-router factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateDynamicRouterAttribute : Attribute
{
    /// <summary>Creates a dynamic-router generator attribute.</summary>
    public GenerateDynamicRouterAttribute(Type payloadType, Type resultType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
    }

    /// <summary>Message payload type routed by the generated dynamic router.</summary>
    public Type PayloadType { get; }

    /// <summary>Route handler result type returned by the generated dynamic router.</summary>
    public Type ResultType { get; }

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";
}

/// <summary>
/// Marks a static method as an initial generated dynamic-router route handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DynamicRouteAttribute : Attribute
{
    /// <summary>Creates a dynamic-router route attribute.</summary>
    public DynamicRouteAttribute(string name, int order, string predicateMethodName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dynamic route name cannot be null, empty, or whitespace.", nameof(name));

        if (string.IsNullOrWhiteSpace(predicateMethodName))
            throw new ArgumentException("Dynamic route predicate method name cannot be null, empty, or whitespace.", nameof(predicateMethodName));

        Name = name;
        Order = order;
        PredicateMethodName = predicateMethodName;
    }

    /// <summary>Route name used for replacement and diagnostics.</summary>
    public string Name { get; }

    /// <summary>Initial route order in the generated dynamic router.</summary>
    public int Order { get; }

    /// <summary>Name of the static predicate method used by this route.</summary>
    public string PredicateMethodName { get; }
}

/// <summary>
/// Marks a static method as the generated dynamic-router default handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DynamicRouteDefaultAttribute : Attribute;
