namespace PatternKit.Generators.CircuitBreaker;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateCircuitBreakerPolicyAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));
    public string FactoryMethodName { get; set; } = "Create";
    public string PolicyName { get; set; } = "circuit-breaker";
    public int FailureThreshold { get; set; } = 3;
    public int BreakDurationMilliseconds { get; set; } = 30000;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CircuitBreakerResultPredicateAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CircuitBreakerExceptionPredicateAttribute : Attribute;
