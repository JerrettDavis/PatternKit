namespace PatternKit.Generators.PriorityQueue;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GeneratePriorityQueueAttribute(Type itemType, Type priorityType) : Attribute
{
    public Type ItemType { get; } = itemType ?? throw new ArgumentNullException(nameof(itemType));

    public Type PriorityType { get; } = priorityType ?? throw new ArgumentNullException(nameof(priorityType));

    public string FactoryMethodName { get; set; } = "Create";

    public string QueueName { get; set; } = "priority-queue";

    public bool DequeueHighestPriorityFirst { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class PriorityQueuePrioritySelectorAttribute : Attribute
{
}
