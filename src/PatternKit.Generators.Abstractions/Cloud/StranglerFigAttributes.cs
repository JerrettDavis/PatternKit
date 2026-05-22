namespace PatternKit.Generators.StranglerFig;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateStranglerFigAttribute(Type requestType, Type responseType) : Attribute
{
    public Type RequestType { get; } = requestType ?? throw new ArgumentNullException(nameof(requestType));

    public Type ResponseType { get; } = responseType ?? throw new ArgumentNullException(nameof(responseType));

    public string FactoryMethodName { get; set; } = "Create";

    public string MigrationName { get; set; } = "strangler-fig";
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StranglerFigRouteAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Route name is required.", nameof(name)) : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StranglerFigLegacyAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StranglerFigModernAttribute : Attribute
{
}
