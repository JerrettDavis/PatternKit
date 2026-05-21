namespace PatternKit.Generators.QueueLoadLeveling;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateQueueLoadLevelingPolicyAttribute(Type resultType) : Attribute
{
    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));

    public string FactoryMethodName { get; set; } = "Create";

    public string PolicyName { get; set; } = "queue-load-leveling";

    public int MaxConcurrentWorkers { get; set; } = 1;

    public int MaxQueueLength { get; set; } = 100;

    public int QueueTimeoutMilliseconds { get; set; } = 30000;
}
