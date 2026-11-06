namespace PatternKit.Generators.Aggregates;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateAggregateCommandHandlerAttribute(
    Type aggregateType,
    Type commandType,
    Type eventType) : Attribute
{
    public Type AggregateType { get; } = aggregateType ?? throw new ArgumentNullException(nameof(aggregateType));

    public Type CommandType { get; } = commandType ?? throw new ArgumentNullException(nameof(commandType));

    public Type EventType { get; } = eventType ?? throw new ArgumentNullException(nameof(eventType));

    public string FactoryMethodName { get; set; } = "Create";

    public string HandlerName { get; set; } = "generated-aggregate-handler";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AggregateDecisionAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AggregateEventApplierAttribute : Attribute;
