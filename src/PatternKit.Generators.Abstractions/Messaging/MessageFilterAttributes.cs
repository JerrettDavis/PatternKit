using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed message-filter factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMessageFilterAttribute : Attribute
{
    /// <summary>Creates a message-filter generator attribute.</summary>
    public GenerateMessageFilterAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    /// <summary>Payload type filtered by the generated message filter.</summary>
    public Type PayloadType { get; }

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name assigned to the generated message filter.</summary>
    public string FilterName { get; set; } = "message-filter";

    /// <summary>Reason returned when no allow rule matches.</summary>
    public string RejectionReason { get; set; } = "Message did not match any allow rule.";
}

/// <summary>
/// Marks a static predicate as an allow rule in a generated message filter.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MessageFilterRuleAttribute : Attribute
{
    /// <summary>Creates a message-filter allow rule attribute.</summary>
    public MessageFilterRuleAttribute(string name, int order)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Message filter rule name cannot be null, empty, or whitespace.", nameof(name));

        Name = name;
        Order = order;
    }

    /// <summary>Rule name used for generated metadata and diagnostics.</summary>
    public string Name { get; }

    /// <summary>Rule order in the generated message filter.</summary>
    public int Order { get; }
}
