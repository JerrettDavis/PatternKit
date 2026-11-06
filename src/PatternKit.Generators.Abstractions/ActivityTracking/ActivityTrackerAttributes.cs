namespace PatternKit.Generators.ActivityTracking;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class GenerateActivityTrackerAttribute : Attribute
{
    public string FactoryMethodName { get; set; } = "Create";

    public string TrackerName { get; set; } = "activity-tracker";
}
