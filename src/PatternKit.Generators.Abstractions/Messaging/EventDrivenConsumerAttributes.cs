using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateEventDrivenConsumerAttribute : Attribute
{
    public GenerateEventDrivenConsumerAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    public Type PayloadType { get; }

    public string FactoryName { get; set; } = "Create";

    public string ConsumerName { get; set; } = "event-driven-consumer";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventDrivenConsumerHandlerAttribute : Attribute
{
    public EventDrivenConsumerHandlerAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Handler name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
    }

    public string Name { get; }
}
