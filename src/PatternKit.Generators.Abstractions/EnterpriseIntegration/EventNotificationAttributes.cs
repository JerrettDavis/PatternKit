namespace PatternKit.Generators.EventNotification;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateEventNotificationAttribute(Type eventType, Type keyType) : Attribute
{
    public Type EventType { get; } = eventType ?? throw new ArgumentNullException(nameof(eventType));

    public Type KeyType { get; } = keyType ?? throw new ArgumentNullException(nameof(keyType));

    public string FactoryMethodName { get; set; } = "Create";

    public string NotificationName { get; set; } = "event-notification";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventNotificationKeyAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventNotificationCorrelationAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventNotificationRuleAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventNotificationMetadataAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Metadata name is required.", nameof(name)) : name;
}
