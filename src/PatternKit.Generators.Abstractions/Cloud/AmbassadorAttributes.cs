namespace PatternKit.Generators.Ambassador;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateAmbassadorAttribute(Type requestType, Type responseType) : Attribute
{
    public Type RequestType { get; } = requestType ?? throw new ArgumentNullException(nameof(requestType));

    public Type ResponseType { get; } = responseType ?? throw new ArgumentNullException(nameof(responseType));

    public string FactoryMethodName { get; set; } = "Create";

    public string AmbassadorName { get; set; } = "ambassador";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AmbassadorTransformAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AmbassadorConnectionPolicyAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AmbassadorTelemetryAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Telemetry name is required.", nameof(name)) : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AmbassadorCallAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class AmbassadorFallbackAttribute : Attribute
{
}
