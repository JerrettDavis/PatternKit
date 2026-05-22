namespace PatternKit.Generators.Sidecar;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateSidecarAttribute(Type requestType, Type responseType) : Attribute
{
    public Type RequestType { get; } = requestType ?? throw new ArgumentNullException(nameof(requestType));

    public Type ResponseType { get; } = responseType ?? throw new ArgumentNullException(nameof(responseType));

    public string FactoryMethodName { get; set; } = "Create";

    public string SidecarName { get; set; } = "sidecar";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SidecarBeforeAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Before step name is required.", nameof(name)) : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SidecarAfterAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("After step name is required.", nameof(name)) : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SidecarHandlerAttribute : Attribute
{
}
