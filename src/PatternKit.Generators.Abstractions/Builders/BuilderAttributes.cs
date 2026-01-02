namespace PatternKit.Generators.Builders;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class GenerateBuilderAttribute : Attribute
{
    public string? BuilderTypeName { get; set; }
    public string NewMethodName { get; set; } = "New";
    public string BuildMethodName { get; set; } = "Build";
    public BuilderModel Model { get; set; } = BuilderModel.MutableInstance;
    public bool GenerateBuilderMethods { get; set; } = false;
    public bool ForceAsync { get; set; } = false;
    public bool IncludeFields { get; set; } = false;
}

public enum BuilderModel
{
    MutableInstance,
    StateProjection
}

[AttributeUsage(AttributeTargets.Constructor, Inherited = false, AllowMultiple = false)]
public sealed class BuilderConstructorAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class BuilderIgnoreAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class BuilderRequiredAttribute : Attribute
{
    public string? Message { get; set; }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class BuilderProjectorAttribute : Attribute
{
}
