namespace PatternKit.Generators.ObjectPool;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateObjectPoolAttribute(Type itemType) : Attribute
{
    public Type ItemType { get; } = itemType ?? throw new ArgumentNullException(nameof(itemType));

    public string FactoryMethodName { get; set; } = "Create";

    public int MaxRetained { get; set; } = -1;

    public string? ResetMethodName { get; set; }
}
