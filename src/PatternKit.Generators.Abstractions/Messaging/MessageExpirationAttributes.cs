using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed message-expiration policy factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMessageExpirationAttribute : Attribute
{
    /// <summary>Creates a message-expiration generator attribute.</summary>
    public GenerateMessageExpirationAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    /// <summary>Payload type stamped and evaluated by the generated expiration policy.</summary>
    public Type PayloadType { get; }

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Name assigned to the generated expiration policy.</summary>
    public string PolicyName { get; set; } = "message-expiration";

    /// <summary>Header used to store expiration deadlines.</summary>
    public string HeaderName { get; set; } = "expires-at";

    /// <summary>Default TTL, in milliseconds, used by Stamp. Values less than or equal to zero omit the default TTL.</summary>
    public int DefaultTtlMilliseconds { get; set; }

    /// <summary>Whether stamping keeps an existing expiration deadline.</summary>
    public bool PreserveExisting { get; set; } = true;

    /// <summary>Reason returned when a message is expired.</summary>
    public string ExpiredReason { get; set; } = "Message expired before processing.";
}
