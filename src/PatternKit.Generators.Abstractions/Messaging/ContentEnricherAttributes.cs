using System;

namespace PatternKit.Generators.Messaging;

public enum ContentEnrichmentErrorPolicy
{
    Throw,
    Skip,
    UseDefault,
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateContentEnricherAttribute(Type payloadType) : Attribute
{
    public Type PayloadType { get; } = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
    public string FactoryName { get; set; } = "Create";
    public string EnricherName { get; set; } = "content-enricher";
    public ContentEnrichmentErrorPolicy DefaultPolicy { get; set; } = ContentEnrichmentErrorPolicy.Throw;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ContentEnrichmentStepAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Enrichment step name is required.", nameof(name))
        : name;

    public int Order { get; set; }
    public ContentEnrichmentErrorPolicy Policy { get; set; } = ContentEnrichmentErrorPolicy.Throw;
    public string DefaultFactoryName { get; set; } = string.Empty;
}
