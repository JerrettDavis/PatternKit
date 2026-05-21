namespace PatternKit.Generators.TableDataGateway;

/// <summary>Generates an in-memory Table Data Gateway factory from a key selector.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateTableDataGatewayAttribute : Attribute
{
    public GenerateTableDataGatewayAttribute(Type rowType, Type keyType)
    {
        RowType = rowType ?? throw new ArgumentNullException(nameof(rowType));
        KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));
    }

    public Type RowType { get; }

    public Type KeyType { get; }

    public string FactoryName { get; set; } = "Create";

    public string TableName { get; set; } = "";
}

/// <summary>Marks the key selector method for a generated Table Data Gateway.</summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class TableGatewayKeySelectorAttribute : Attribute;
