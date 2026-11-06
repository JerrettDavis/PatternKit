using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateScatterGatherAttribute : Attribute
{
    public GenerateScatterGatherAttribute(Type requestType, Type responseType, Type resultType)
    {
        RequestType = requestType ?? throw new ArgumentNullException(nameof(requestType));
        ResponseType = responseType ?? throw new ArgumentNullException(nameof(responseType));
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
    }

    public Type RequestType { get; }

    public Type ResponseType { get; }

    public Type ResultType { get; }

    public string FactoryName { get; set; } = "Create";

    public string Name { get; set; } = "scatter-gather";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ScatterGatherRecipientAttribute : Attribute
{
    public ScatterGatherRecipientAttribute(string name, int order = 0, string? predicateMethodName = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Recipient name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
        Order = order;
        PredicateMethodName = predicateMethodName;
    }

    public string Name { get; }

    public int Order { get; }

    public string? PredicateMethodName { get; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ScatterGatherAggregatorAttribute : Attribute
{
}
