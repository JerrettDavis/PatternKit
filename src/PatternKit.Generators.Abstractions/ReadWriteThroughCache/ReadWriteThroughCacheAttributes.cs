namespace PatternKit.Generators.ReadWriteThroughCache;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateReadWriteThroughCachePolicyAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));
    public string FactoryMethodName { get; set; } = "Create";
    public string PolicyName { get; set; } = "read-write-through-cache";
    public int TimeToLiveMilliseconds { get; set; }
}
