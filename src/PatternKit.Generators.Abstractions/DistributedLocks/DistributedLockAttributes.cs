namespace PatternKit.Generators.DistributedLocks;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateDistributedLockAttribute : Attribute
{
    public GenerateDistributedLockAttribute(Type keyType)
        => KeyType = keyType ?? throw new ArgumentNullException(nameof(keyType));

    public Type KeyType { get; }

    public string FactoryMethodName { get; set; } = "Create";

    public string LockName { get; set; } = "distributed-lock";

    public int LeaseDurationMilliseconds { get; set; } = 30000;
}
