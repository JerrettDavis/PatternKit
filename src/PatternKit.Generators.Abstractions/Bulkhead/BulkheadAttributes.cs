namespace PatternKit.Generators.Bulkhead;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateBulkheadPolicyAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));
    public string FactoryMethodName { get; set; } = "Create";
    public string PolicyName { get; set; } = "bulkhead";
    public int MaxConcurrency { get; set; } = 8;
    public int MaxQueueLength { get; set; }
    public int QueueTimeoutMilliseconds { get; set; }
}
