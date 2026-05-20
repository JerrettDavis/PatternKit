namespace PatternKit.Generators.Factories;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FactoryMethodAttribute(Type keyType) : Attribute
{
    public Type KeyType { get; } = keyType;
    public string CreateMethodName { get; set; } = "Create";
    public bool CaseInsensitiveStrings { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class FactoryCaseAttribute(object key) : Attribute
{
    public object Key { get; } = key;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class FactoryDefaultAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class, Inherited = false)]
public sealed class FactoryClassAttribute(Type keyType) : Attribute
{
    public Type KeyType { get; } = keyType;
    public string? FactoryTypeName { get; set; }
    public bool GenerateTryCreate { get; set; } = true;
    public bool GenerateEnumKeys { get; set; } = false;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FactoryClassKeyAttribute(object key) : Attribute
{
    public object Key { get; } = key;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateAbstractFactoryAttribute(Type keyType) : Attribute
{
    public Type KeyType { get; } = keyType;
    public string FactoryMethodName { get; set; } = "Create";
    public string? ServiceProviderFactoryMethodName { get; set; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class AbstractFactoryProductAttribute(object familyKey, Type contractType, Type implementationType) : Attribute
{
    public object FamilyKey { get; } = familyKey;
    public Type ContractType { get; } = contractType;
    public Type ImplementationType { get; } = implementationType;
    public bool IsDefaultFamily { get; set; }
}
