using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateMessagingBridgeAttribute : Attribute
{
    public GenerateMessagingBridgeAttribute(Type inboundType, Type outboundType)
    {
        InboundType = inboundType ?? throw new ArgumentNullException(nameof(inboundType));
        OutboundType = outboundType ?? throw new ArgumentNullException(nameof(outboundType));
    }

    public Type InboundType { get; }

    public Type OutboundType { get; }

    public string FactoryName { get; set; } = "Create";

    public string BridgeName { get; set; } = "messaging-bridge";
}
