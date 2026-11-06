namespace PatternKit.Generators.IdentityMap;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateIdentityMapAttribute : Attribute
{
    public GenerateIdentityMapAttribute(Type entityType, Type keyType)
    {
        EntityType = entityType ?? throw new ArgumentNullException(nameof(entityType));
        KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
    }

    public Type EntityType { get; }

    public Type KeyType { get; }

    public string FactoryName { get; set; } = "Create";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class IdentityMapKeySelectorAttribute : Attribute;
