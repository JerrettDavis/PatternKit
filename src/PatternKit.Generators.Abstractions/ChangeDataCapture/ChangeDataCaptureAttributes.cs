namespace PatternKit.Generators.ChangeDataCapture;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateChangeDataCaptureAttribute(Type mutationType, Type eventType) : Attribute
{
    public Type MutationType { get; } = mutationType;
    public Type EventType { get; } = eventType;
    public string FactoryMethodName { get; set; } = "Create";
    public string MapperMethodName { get; set; } = "Map";
    public string PipelineName { get; set; } = "change-data-capture";
}
