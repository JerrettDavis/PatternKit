namespace PatternKit.Generators.DataMapping;

/// <summary>Requests a generated Data Mapper factory for a partial host type.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateDataMapperAttribute : Attribute
{
    public GenerateDataMapperAttribute(Type domainType, Type dataType)
    {
        DomainType = domainType ?? throw new ArgumentNullException(nameof(domainType));
        DataType = dataType ?? throw new ArgumentNullException(nameof(dataType));
    }

    /// <summary>Domain model type isolated from persistence details.</summary>
    public Type DomainType { get; }

    /// <summary>Persistence or transport data model type.</summary>
    public Type DataType { get; }

    /// <summary>Generated factory method name.</summary>
    public string FactoryName { get; set; } = "Create";
}

/// <summary>Marks the domain-to-data projection method.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DataMapperToDataAttribute : Attribute;

/// <summary>Marks the data-to-domain projection method.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DataMapperToDomainAttribute : Attribute;
