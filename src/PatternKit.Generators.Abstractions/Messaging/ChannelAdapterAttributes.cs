using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateChannelAdapterAttribute : Attribute
{
    public GenerateChannelAdapterAttribute(Type externalType, Type payloadType)
    {
        ExternalType = externalType ?? throw new ArgumentNullException(nameof(externalType));
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
    }

    public Type ExternalType { get; }

    public Type PayloadType { get; }

    public string FactoryName { get; set; } = "Create";

    public string AdapterName { get; set; } = "channel-adapter";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ChannelAdapterInboundAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ChannelAdapterOutboundAttribute : Attribute
{
}
