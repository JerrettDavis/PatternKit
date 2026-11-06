using System;

namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateClaimCheckAttribute(Type payloadType) : Attribute
{
    public Type PayloadType { get; } = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
    public string FactoryName { get; set; } = "Create";
    public string ClaimCheckName { get; set; } = "claim-check";
    public string StoreName { get; set; } = "claim-store";
    public string ClaimIdPrefix { get; set; } = "claim";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ClaimCheckStoreFactoryAttribute : Attribute;
