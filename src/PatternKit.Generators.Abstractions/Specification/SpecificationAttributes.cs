namespace PatternKit.Generators.Specification;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateSpecificationRegistryAttribute(Type candidateType) : Attribute
{
    public Type CandidateType { get; } = candidateType ?? throw new ArgumentNullException(nameof(candidateType));
    public string FactoryMethodName { get; set; } = "Create";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SpecificationRuleAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Specification name is required.", nameof(name))
        : name;
}
