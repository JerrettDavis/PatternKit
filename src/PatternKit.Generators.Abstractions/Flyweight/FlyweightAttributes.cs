namespace PatternKit.Generators.Flyweight;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class FlyweightAttribute : Attribute
{
    public FlyweightAttribute(Type keyType)
    {
        KeyType = keyType;
    }

    public Type KeyType { get; }

    public string? CacheTypeName { get; set; }

    public int Capacity { get; set; }

    public FlyweightEviction Eviction { get; set; } = FlyweightEviction.None;

    public FlyweightThreadingPolicy Threading { get; set; } = FlyweightThreadingPolicy.Locking;

    public bool GenerateTryGet { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class FlyweightFactoryAttribute : Attribute;

public enum FlyweightEviction
{
    None = 0,
    Lru = 1
}

public enum FlyweightThreadingPolicy
{
    SingleThreadedFast = 0,
    Locking = 1,
    Concurrent = 2
}
