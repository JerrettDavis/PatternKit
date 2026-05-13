namespace PatternKit.Generators.Chain;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ChainAttribute : Attribute
{
    public ChainModel Model { get; set; } = ChainModel.Responsibility;
    public string HandleMethodName { get; set; } = "Handle";
    public string TryHandleMethodName { get; set; } = "TryHandle";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ChainHandlerAttribute : Attribute
{
    public int Order { get; set; }
    public string? Name { get; set; }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ChainDefaultAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ChainTerminalAttribute : Attribute;

public enum ChainModel
{
    Responsibility = 0,
    Pipeline = 1
}
