namespace PatternKit.Generators;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class GenerateStrategyAttribute : Attribute
{
    public string Name { get; }
    public Type InType { get; }
    public Type? OutType { get; }
    public StrategyKind Kind { get; }

    public GenerateStrategyAttribute(string name, Type inType, StrategyKind kind)
    {
        Name = name;
        InType = inType;
        Kind = kind;
    }

    public GenerateStrategyAttribute(string name, Type inType, Type outType, StrategyKind kind)
    {
        Name = name;
        InType = inType;
        OutType = outType;
        Kind = kind;
    }
}

public enum StrategyKind
{
    Action,
    Result,
    Try
}