namespace PatternKit.Generators.Command;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CommandAttribute : Attribute
{
    public string? CommandTypeName { get; set; }
    public bool GenerateAsync { get; set; } = true;
    public bool ForceAsync { get; set; }
    public bool GenerateUndo { get; set; }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CommandHandlerAttribute : Attribute
{
    public Type? CommandType { get; set; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandHostAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CommandCaseAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CommandUndoAttribute : Attribute;
