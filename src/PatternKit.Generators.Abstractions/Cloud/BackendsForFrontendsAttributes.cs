namespace PatternKit.Generators.BackendsForFrontends;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateBackendsForFrontendsAttribute(Type requestType, Type responseType) : Attribute
{
    public Type RequestType { get; } = requestType ?? throw new ArgumentNullException(nameof(requestType));

    public Type ResponseType { get; } = responseType ?? throw new ArgumentNullException(nameof(responseType));

    public string FactoryMethodName { get; set; } = "Create";

    public string GatewayName { get; set; } = "backends-for-frontends";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FrontendSelectorAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Frontend name is required.", nameof(name)) : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FrontendHandlerAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Frontend name is required.", nameof(name)) : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class FrontendFallbackAttribute : Attribute
{
}
