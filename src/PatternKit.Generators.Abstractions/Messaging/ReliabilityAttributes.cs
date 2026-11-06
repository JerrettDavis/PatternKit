using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates idempotent receiver, inbox processor, and outbox factories for a reliable messaging pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateReliabilityPipelineAttribute : Attribute
{
    /// <summary>Creates a reliability-pipeline generator attribute.</summary>
    public GenerateReliabilityPipelineAttribute(Type payloadType, Type resultType, Type outboxPayloadType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
        ResultType = resultType ?? throw new ArgumentNullException(nameof(resultType));
        OutboxPayloadType = outboxPayloadType ?? throw new ArgumentNullException(nameof(outboxPayloadType));
    }

    /// <summary>Input payload handled by the idempotent receiver and inbox.</summary>
    public Type PayloadType { get; }

    /// <summary>Result type returned by the reliable handler.</summary>
    public Type ResultType { get; }

    /// <summary>Payload type stored in the generated outbox.</summary>
    public Type OutboxPayloadType { get; }

    /// <summary>Name of the generated idempotent receiver factory method.</summary>
    public string ReceiverFactoryName { get; set; } = "CreateReceiver";

    /// <summary>Name of the generated inbox processor factory method.</summary>
    public string InboxFactoryName { get; set; } = "CreateInbox";

    /// <summary>Name of the generated outbox factory method.</summary>
    public string OutboxFactoryName { get; set; } = "CreateOutbox";

    /// <summary>Duplicate handling policy: Suppress or ReplayCompleted.</summary>
    public string DuplicatePolicy { get; set; } = "Suppress";

    /// <summary>Missing idempotency-key policy: Reject or Process.</summary>
    public string MissingKeyPolicy { get; set; } = "Reject";
}

/// <summary>
/// Marks the handler used by a generated reliability pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ReliabilityHandlerAttribute : Attribute;

/// <summary>
/// Marks an optional idempotency key selector used by a generated reliability pipeline.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ReliabilityKeySelectorAttribute : Attribute;
