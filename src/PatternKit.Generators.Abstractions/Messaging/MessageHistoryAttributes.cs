using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMessageHistoryAttribute : Attribute
{
    public GenerateMessageHistoryAttribute(Type payloadType, string component)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
        if (string.IsNullOrWhiteSpace(component))
            throw new ArgumentException("Message history component cannot be null, empty, or whitespace.", nameof(component));

        Component = component;
    }

    public Type PayloadType { get; }

    public string Component { get; }

    public string FactoryName { get; set; } = "Create";

    public string Action { get; set; } = "handled";

    public string HeaderName { get; set; } = "Message-History";
}
