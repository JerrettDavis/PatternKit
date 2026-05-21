namespace PatternKit.Generators.RateLimiting;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateRateLimitPolicyAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));
    public string FactoryMethodName { get; set; } = "Create";
    public string PolicyName { get; set; } = "rate-limit";
    public int PermitLimit { get; set; } = 60;
    public int WindowMilliseconds { get; set; } = 60000;
}
