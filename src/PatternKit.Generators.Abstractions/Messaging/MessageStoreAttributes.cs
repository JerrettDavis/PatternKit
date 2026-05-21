using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed message-store factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMessageStoreAttribute : Attribute
{
    /// <summary>Creates a message-store generator attribute.</summary>
    public GenerateMessageStoreAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    /// <summary>Payload type persisted by the generated message store.</summary>
    public Type PayloadType { get; }

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name assigned to the generated message store.</summary>
    public string StoreName { get; set; } = "message-store";
}

/// <summary>
/// Marks a static method as the generated message identity selector.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MessageStoreIdentityAttribute : Attribute;

/// <summary>
/// Marks a static predicate as the generated message-store retention policy.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MessageStoreRetentionAttribute : Attribute;
