namespace PatternKit.Generators.CacheAside;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateCacheAsidePolicyAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));
    public string FactoryMethodName { get; set; } = "Create";
    public string PolicyName { get; set; } = "cache-aside";
    public int TimeToLiveMilliseconds { get; set; }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CacheAsidePredicateAttribute : Attribute;
