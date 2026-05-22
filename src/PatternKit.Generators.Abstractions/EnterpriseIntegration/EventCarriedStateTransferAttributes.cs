namespace PatternKit.Generators.EventCarriedStateTransfer;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateEventCarriedStateTransferAttribute(Type eventType, Type keyType, Type stateType) : Attribute
{
    public Type EventType { get; } = eventType ?? throw new ArgumentNullException(nameof(eventType));

    public Type KeyType { get; } = keyType ?? throw new ArgumentNullException(nameof(keyType));

    public Type StateType { get; } = stateType ?? throw new ArgumentNullException(nameof(stateType));

    public string FactoryMethodName { get; set; } = "Create";

    public string TransferName { get; set; } = "event-carried-state-transfer";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventCarriedStateKeyAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventCarriedStateVersionAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class EventCarriedStateMapperAttribute : Attribute
{
}
