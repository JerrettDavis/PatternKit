namespace PatternKit.Generators.Factories;

[AttributeUsage(AttributeTargets.Class)]
public sealed class FactoryMethodAttribute(Type keyType) : Attribute
{
    public Type KeyType { get; } = keyType;
    public string CreateMethodName { get; set; } = "Create";
    public bool CaseInsensitiveStrings { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class FactoryCaseAttribute(object key) : Attribute
{
    public object Key { get; } = key;
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class FactoryDefaultAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
public sealed class FactoryClassAttribute(Type keyType) : Attribute
{
    public Type KeyType { get; } = keyType;
    public string? FactoryTypeName { get; set; }
    public bool GenerateTryCreate { get; set; } = true;
    public bool GenerateEnumKeys { get; set; } = false;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class FactoryClassKeyAttribute(object key) : Attribute
{
    public object Key { get; } = key;
}
