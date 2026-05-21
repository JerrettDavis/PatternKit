namespace PatternKit.Generators.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GeneratePipesAndFiltersPipelineAttribute(Type contextType) : Attribute
{
    public Type ContextType { get; } = contextType ?? throw new ArgumentNullException(nameof(contextType));

    public string FactoryMethodName { get; set; } = "Create";

    public string PipelineName { get; set; } = "pipes-and-filters";
}
