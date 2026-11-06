namespace PatternKit.Generators.ManualTaskGates;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateManualTaskGateAttribute : Attribute
{
    public GenerateManualTaskGateAttribute(Type keyType)
        => KeyType = keyType;

    public Type KeyType { get; }

    public string FactoryMethodName { get; set; } = "Create";

    public string GateName { get; set; } = "manual-task-gate";
}
