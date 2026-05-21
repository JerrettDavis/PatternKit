namespace PatternKit.Generators.EventSourcing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateEventStoreAttribute : Attribute
{
    public GenerateEventStoreAttribute(Type eventType, Type streamIdType)
    {
        EventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        StreamIdType = streamIdType ?? throw new ArgumentNullException(nameof(streamIdType));
    }

    public Type EventType { get; }

    public Type StreamIdType { get; }

    public string FactoryName { get; set; } = "Create";

    public string StoreName { get; set; } = "";
}
