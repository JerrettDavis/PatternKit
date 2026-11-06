using System;

namespace PatternKit.Generators.Messaging;

/// <summary>Generates a typed Guaranteed Delivery queue factory for a partial class or struct.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateGuaranteedDeliveryAttribute : Attribute
{
    public GenerateGuaranteedDeliveryAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    public Type PayloadType { get; }
    public string FactoryName { get; set; } = "Create";
    public string QueueName { get; set; } = "guaranteed-delivery";
    public int LeaseMilliseconds { get; set; } = 300000;
    public int MaxDeliveryAttempts { get; set; } = 5;
}
