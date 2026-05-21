namespace PatternKit.Generators.MaterializedViews;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateMaterializedViewAttribute : Attribute
{
    public GenerateMaterializedViewAttribute(Type stateType, Type eventType)
    {
        StateType = stateType ?? throw new ArgumentNullException(nameof(stateType));
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
    }

    public Type StateType { get; }

    public Type EventType { get; }

    public string FactoryName { get; set; } = "Create";

    public string ViewName { get; set; } = "";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class MaterializedViewHandlerAttribute : Attribute
{
    public MaterializedViewHandlerAttribute(Type eventType)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
    }

    public Type EventType { get; }

    public int Order { get; set; }
}
