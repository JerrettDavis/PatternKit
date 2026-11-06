namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateCompetingConsumerGroupAttribute(Type messageType, Type resultType) : Attribute
{
    public Type MessageType { get; } = messageType ?? throw new ArgumentNullException(nameof(messageType));

    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));

    public string FactoryMethodName { get; set; } = "Create";

    public string GroupName { get; set; } = "competing-consumers";

    public int MaxConcurrentDeliveries { get; set; } = 1;
}
