namespace PatternKit.Generators.FeatureToggles;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateFeatureToggleSetAttribute : Attribute
{
    public GenerateFeatureToggleSetAttribute(Type contextType)
    {
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
    }

    public Type ContextType { get; }

    public string FactoryName { get; set; } = "Create";

    public string SetName { get; set; } = "feature-toggles";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class FeatureToggleRuleAttribute : Attribute
{
    public FeatureToggleRuleAttribute(string name)
    {
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Feature toggle rule name is required.", nameof(name))
            : name;
    }

    public string Name { get; }

    public bool DefaultEnabled { get; set; }
}
