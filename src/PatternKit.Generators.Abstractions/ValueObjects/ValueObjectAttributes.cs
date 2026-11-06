namespace PatternKit.Generators.ValueObjects;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GenerateValueObjectAttribute : Attribute
{
    public string FactoryMethodName { get; set; } = "Create";
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ValueObjectComponentAttribute : Attribute;
