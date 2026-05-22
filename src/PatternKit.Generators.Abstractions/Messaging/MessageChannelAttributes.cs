using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMessageChannelAttribute : Attribute
{
    public GenerateMessageChannelAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    public Type PayloadType { get; }

    public string FactoryName { get; set; } = "Create";

    public string ChannelName { get; set; } = "message-channel";

    public int Capacity { get; set; } = -1;

    public string BackpressurePolicy { get; set; } = "Reject";
}
