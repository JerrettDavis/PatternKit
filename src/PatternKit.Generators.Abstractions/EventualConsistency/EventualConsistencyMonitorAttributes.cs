namespace PatternKit.Generators.EventualConsistency;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateEventualConsistencyMonitorAttribute : Attribute
{
    public GenerateEventualConsistencyMonitorAttribute(Type keyType)
        => KeyType = keyType;

    public Type KeyType { get; }

    public string FactoryMethodName { get; set; } = "Create";

    public string MonitorName { get; set; } = "eventual-consistency-monitor";

    public long MaxAllowedLag { get; set; }
}
