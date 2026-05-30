namespace PatternKit.Generators.CacheStampedeProtection;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateCacheStampedeProtectionAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));

    public string FactoryMethodName { get; set; } = "Create";

    public string PolicyName { get; set; } = "cache-stampede-protection";
}
