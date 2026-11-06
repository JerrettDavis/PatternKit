using System;

namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a typed mailbox factory for a partial class or struct.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMailboxAttribute : Attribute
{
    /// <summary>Creates a mailbox generator attribute.</summary>
    public GenerateMailboxAttribute(Type payloadType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
    }

    /// <summary>Message payload type accepted by the generated mailbox.</summary>
    public Type PayloadType { get; }

    /// <summary>Name of the generated mailbox factory method.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>Bounded capacity. Use 0 for an unbounded mailbox.</summary>
    public int Capacity { get; set; }

    /// <summary>Backpressure policy emitted when <see cref="Capacity"/> is greater than zero.</summary>
    public string BackpressurePolicy { get; set; } = "Wait";

    /// <summary>Error policy emitted into the generated mailbox.</summary>
    public string ErrorPolicy { get; set; } = "Stop";
}

/// <summary>
/// Marks the static method used by a generated mailbox handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MailboxHandlerAttribute : Attribute;

/// <summary>
/// Marks the static method used by a generated mailbox error handler.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MailboxErrorHandlerAttribute : Attribute;

/// <summary>
/// Marks the static method used by a generated mailbox event sink.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MailboxEventSinkAttribute : Attribute;
