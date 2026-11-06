namespace PatternKit.Generators.Messaging;

/// <summary>
/// Generates a factory for the Correlation Identifier enterprise integration pattern.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateCorrelationIdentifierAttribute : Attribute
{
    /// <summary>
    /// Creates a correlation identifier generator attribute.
    /// </summary>
    /// <param name="payloadType">The message payload type.</param>
    public GenerateCorrelationIdentifierAttribute(Type payloadType)
    {
        PayloadType = payloadType ?? throw new ArgumentNullException(nameof(payloadType));
    }

    /// <summary>The message payload type.</summary>
    public Type PayloadType { get; }

    /// <summary>The generated factory method name.</summary>
    public string FactoryName { get; set; } = "Create";

    /// <summary>The header used to carry the correlation identifier.</summary>
    public string HeaderName { get; set; } = "correlation-id";

    /// <summary>Whether generated configuration preserves an existing identifier.</summary>
    public bool PreserveExisting { get; set; } = true;
}
