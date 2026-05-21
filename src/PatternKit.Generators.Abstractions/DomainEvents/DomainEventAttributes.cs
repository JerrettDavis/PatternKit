namespace PatternKit.Generators.DomainEvents;

/// <summary>Generates a Domain Event dispatcher factory from attributed handler methods.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateDomainEventDispatcherAttribute : Attribute
{
    public GenerateDomainEventDispatcherAttribute(Type eventBaseType)
    {
        EventBaseType = eventBaseType ?? throw new ArgumentNullException(nameof(eventBaseType));
    }

    public Type EventBaseType { get; }

    public string FactoryName { get; set; } = "Create";

    public string DispatcherName { get; set; } = "";
}

/// <summary>Marks a handler method for a generated Domain Event dispatcher.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class DomainEventHandlerAttribute : Attribute
{
    public DomainEventHandlerAttribute(Type eventType, int order)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        Order = order;
    }

    public Type EventType { get; }

    public int Order { get; }
}
