namespace PatternKit.Generators.LazyLoading;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateLazyLoadAttribute(Type valueType) : Attribute
{
    public Type ValueType { get; } = valueType ?? throw new ArgumentNullException(nameof(valueType));
    public string FactoryMethodName { get; set; } = "Create";
    public string LoaderMethodName { get; set; } = "LoadAsync";
    public string LazyLoadName { get; set; } = "lazy-load";
    public bool CacheEnabled { get; set; } = true;
    public int TimeToLiveMilliseconds { get; set; }
}
