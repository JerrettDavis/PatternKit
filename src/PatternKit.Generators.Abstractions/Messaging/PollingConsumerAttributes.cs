using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GeneratePollingConsumerAttribute : Attribute
{
    public GeneratePollingConsumerAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    public Type PayloadType { get; }

    public string FactoryName { get; set; } = "Create";

    public string ConsumerName { get; set; } = "polling-consumer";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PollingConsumerSourceAttribute : Attribute
{
}
