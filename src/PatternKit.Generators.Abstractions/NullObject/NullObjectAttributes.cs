namespace PatternKit.Generators.NullObject;

/// <summary>
/// Generates a Null Object implementation for an interface contract.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class GenerateNullObjectAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the generated implementation type name. Defaults to Null{ContractNameWithoutLeadingI}.
    /// </summary>
    public string? TypeName { get; set; }
}

/// <summary>
/// Overrides the generated default return value for a Null Object member.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class NullObjectDefaultAttribute : Attribute
{
    public NullObjectDefaultAttribute(string value) => Value = value;

    public NullObjectDefaultAttribute(bool value) => Value = value;

    public NullObjectDefaultAttribute(int value) => Value = value;

    public NullObjectDefaultAttribute(long value) => Value = value;

    public NullObjectDefaultAttribute(double value) => Value = value;

    /// <summary>
    /// Gets the configured constant default value.
    /// </summary>
    public object Value { get; }
}
