namespace PatternKit.Generators.Retry;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateRetryPolicyAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));
    public string FactoryMethodName { get; set; } = "Create";
    public string PolicyName { get; set; } = "retry";
    public int MaxAttempts { get; set; } = 3;
    public int InitialDelayMilliseconds { get; set; }
    public double BackoffFactor { get; set; } = 1;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class RetryResultPredicateAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class RetryExceptionPredicateAttribute : Attribute;
