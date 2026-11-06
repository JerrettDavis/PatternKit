using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateResequencerAttribute : Attribute
{
    public GenerateResequencerAttribute(Type payloadType)
        => PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));

    public Type PayloadType { get; }

    public string FactoryName { get; set; } = "Create";

    public string Name { get; set; } = "resequencer";

    public long StartsAt { get; set; } = 1;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ResequencerSequenceAttribute : Attribute
{
}
