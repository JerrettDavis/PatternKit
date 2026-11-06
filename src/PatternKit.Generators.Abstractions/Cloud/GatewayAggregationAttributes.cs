namespace PatternKit.Generators.GatewayAggregation;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateGatewayAggregationAttribute(Type requestType, Type responseType) : Attribute
{
    public Type RequestType { get; } = requestType ?? throw new ArgumentNullException(nameof(requestType));

    public Type ResponseType { get; } = responseType ?? throw new ArgumentNullException(nameof(responseType));

    public string FactoryMethodName { get; set; } = "Create";

    public string GatewayName { get; set; } = "gateway-aggregation";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GatewayAggregationFetchAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Fetch name is required.", nameof(name)) : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class GatewayAggregationComposerAttribute : Attribute
{
}
