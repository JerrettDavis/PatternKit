namespace PatternKit.Generators.CanonicalDataModel;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateCanonicalDataModelAttribute(Type sourceType, Type canonicalType) : Attribute
{
    public Type SourceType { get; } = sourceType ?? throw new ArgumentNullException(nameof(sourceType));

    public Type CanonicalType { get; } = canonicalType ?? throw new ArgumentNullException(nameof(canonicalType));

    public string FactoryMethodName { get; set; } = "Create";

    public string ModelName { get; set; } = "canonical-data-model";

    public string AdapterName { get; set; } = "source-adapter";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class CanonicalDataModelMapperAttribute : Attribute
{
}
