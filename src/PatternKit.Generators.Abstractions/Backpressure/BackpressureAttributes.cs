namespace PatternKit.Generators.Backpressure;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateBackpressurePolicyAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));
    public string FactoryMethodName { get; set; } = "Create";
    public string PolicyName { get; set; } = "backpressure";
    public int Capacity { get; set; } = 8;
    public string Mode { get; set; } = "Reject";
    public int WaitTimeoutMilliseconds { get; set; }
}
