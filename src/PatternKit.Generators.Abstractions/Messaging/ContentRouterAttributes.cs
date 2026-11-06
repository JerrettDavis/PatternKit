using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed content-router factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateContentRouterAttribute : Attribute
{
    /// <summary>Creates a content-router generator attribute.</summary>
    public GenerateContentRouterAttribute(Type payloadType, Type resultType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
    }

    /// <summary>Message payload type routed by the generated content router.</summary>
    public Type PayloadType { get; }

    /// <summary>Route handler result type returned by the generated content router.</summary>
    public Type ResultType { get; }

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";
}

/// <summary>
/// Marks a static method as a generated content-router route handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ContentRouteAttribute : Attribute
{
    /// <summary>Creates a content-router route attribute.</summary>
    public ContentRouteAttribute(string name, int order, string predicateMethodName)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Content route name cannot be null, empty, or whitespace.", nameof(name));

        if (string.IsNullOrWhiteSpace(predicateMethodName))
            throw new ArgumentException("Content route predicate method name cannot be null, empty, or whitespace.", nameof(predicateMethodName));

        Name = name;
        Order = order;
        PredicateMethodName = predicateMethodName;
    }

    /// <summary>Route name used for diagnostics and duplicate validation.</summary>
    public string Name { get; }

    /// <summary>Route order in the generated content router.</summary>
    public int Order { get; }

    /// <summary>Name of the static predicate method used by this route.</summary>
    public string PredicateMethodName { get; }
}

/// <summary>
/// Marks a static method as the generated content-router default handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ContentRouteDefaultAttribute : Attribute;
