namespace PatternKit.Generators.HealthEndpointMonitoring;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateHealthEndpointAttribute(Type contextType) : Attribute
{
    public Type ContextType { get; } = contextType ?? throw new ArgumentNullException(nameof(contextType));

    public string FactoryMethodName { get; set; } = "Create";

    public string EndpointName { get; set; } = "health-endpoint";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class HealthEndpointCheckAttribute(string? name = null) : Attribute
{
    public string? Name { get; } = name;

    public int Order { get; set; }
}
