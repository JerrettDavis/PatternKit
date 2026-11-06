using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a durable subscriber factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateDurableSubscriberAttribute : Attribute
{
    public GenerateDurableSubscriberAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    public Type PayloadType { get; }

    public string FactoryName { get; set; } = "Create";

    public string SubscriberName { get; set; } = "durable-subscriber";
}

/// <summary>
/// Marks a static method as a generated durable subscriber handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DurableSubscriberHandlerAttribute : Attribute
{
    public DurableSubscriberHandlerAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Handler name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
    }

    public string Name { get; }
}
