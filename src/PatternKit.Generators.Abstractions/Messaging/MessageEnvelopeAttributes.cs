using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates typed factories for a message envelope contract.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMessageEnvelopeAttribute : Attribute
{
    /// <summary>Creates a message-envelope generator attribute.</summary>
    public GenerateMessageEnvelopeAttribute(Type payloadType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
    }

    /// <summary>Payload type carried by the generated message factory.</summary>
    public Type PayloadType { get; }

    /// <summary>Name of the generated message factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name of the generated context factory method.</summary>
    public string ContextFactoryName { get; set; } = "CreateContext";
}

/// <summary>
/// Declares a required header for a generated message envelope contract.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class MessageEnvelopeHeaderAttribute : Attribute
{
    /// <summary>Creates a message-envelope header declaration.</summary>
    public MessageEnvelopeHeaderAttribute(string name, Type valueType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Header name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
        ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
    }

    /// <summary>Header name written into <c>MessageHeaders</c>.</summary>
    public string Name { get; }

    /// <summary>Type of the generated factory parameter for this header.</summary>
    public Type ValueType { get; }

    /// <summary>Optional generated factory parameter name. When omitted, the header name is converted to a camel-case identifier.</summary>
    public string? ParameterName { get; set; }
}
