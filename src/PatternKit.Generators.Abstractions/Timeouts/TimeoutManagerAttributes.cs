namespace PatternKit.Generators.Timeouts;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateTimeoutManagerAttribute : Attribute
{
    public GenerateTimeoutManagerAttribute(Type keyType)
        => KeyType = keyType;

    public Type KeyType { get; }

    public string FactoryMethodName { get; set; } = "Create";

    public string ManagerName { get; set; } = "timeout-manager";
}
