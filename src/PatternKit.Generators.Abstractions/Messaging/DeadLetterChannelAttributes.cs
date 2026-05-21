using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a dead-letter channel factory for failed or undeliverable messages.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateDeadLetterChannelAttribute(Type payloadType) : Attribute
{
    /// <summary>Payload type captured by the generated dead-letter channel.</summary>
    public Type PayloadType { get; } = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    /// <summary>Name of the generated factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Operational channel name recorded in dead-letter headers.</summary>
    public string ChannelName { get; set; } = "dead-letter-channel";

    /// <summary>Source endpoint, pipeline, or transport name recorded in dead-letter metadata.</summary>
    public string Source { get; set; } = "application";

    /// <summary>Prefix used by generated dead-letter identifiers.</summary>
    public string IdPrefix { get; set; } = "dead";

    /// <summary>Controls whether exception type and message are persisted.</summary>
    public bool IncludeExceptionDetails { get; set; } = true;
}

/// <summary>
/// Marks the store factory used by a generated dead-letter channel.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class DeadLetterStoreFactoryAttribute : Attribute;
