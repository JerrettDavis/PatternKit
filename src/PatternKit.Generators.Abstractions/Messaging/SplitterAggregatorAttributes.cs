using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed splitter factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateSplitterAttribute : Attribute
{
    /// <summary>Creates a splitter generator attribute.</summary>
    public GenerateSplitterAttribute(Type payloadType, Type itemType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
        ItemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
    }

    /// <summary>Message payload type accepted by the generated splitter.</summary>
    public Type PayloadType { get; }

    /// <summary>Item payload type produced by the generated splitter.</summary>
    public Type ItemType { get; }

    /// <summary>Name of the generated splitter factory method.</summary>
    public string FactoryName { get; set; } = "Create";
}

/// <summary>
/// Marks the static method used by a generated splitter projection.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SplitterProjectionAttribute : Attribute;

/// <summary>
/// Generates a typed aggregator factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateAggregatorAttribute : Attribute
{
    /// <summary>Creates an aggregator generator attribute.</summary>
    public GenerateAggregatorAttribute(Type keyType, Type itemType, Type resultType)
    {
        KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
        ItemType = itemType ?? throw new ArgumentNullException(nameof(itemType));
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
    }

    /// <summary>Aggregation correlation key type.</summary>
    public Type KeyType { get; }

    /// <summary>Item payload type collected by the generated aggregator.</summary>
    public Type ItemType { get; }

    /// <summary>Result type projected when a group completes.</summary>
    public Type ResultType { get; }

    /// <summary>Name of the generated aggregator factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>
    /// Duplicate message-id policy emitted into the generated aggregator. Supported values are Ignore, Include, and Replace.
    /// </summary>
    public string DuplicatePolicy { get; set; } = "Ignore";
}

/// <summary>
/// Marks the static method used by a generated aggregator key selector.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AggregatorCorrelationAttribute : Attribute;

/// <summary>
/// Marks the static method used by a generated aggregator completion policy.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AggregatorCompletionAttribute : Attribute;

/// <summary>
/// Marks the static method used by a generated aggregator result projection.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AggregatorProjectionAttribute : Attribute;
