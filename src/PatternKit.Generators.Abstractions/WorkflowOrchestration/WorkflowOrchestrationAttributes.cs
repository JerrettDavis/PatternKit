namespace PatternKit.Generators.WorkflowOrchestration;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class WorkflowOrchestrationAttribute : Attribute
{
    public string FactoryMethodName { get; set; } = "Create";

    public string WorkflowName { get; set; } = "workflow-orchestration";
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class WorkflowStepAttribute : Attribute
{
    public WorkflowStepAttribute(string name, int order)
    {
        Name = name;
        Order = order;
    }

    public string Name { get; }

    public int Order { get; }

    public int MaxAttempts { get; set; } = 1;

    public string? Condition { get; set; }

    public string? Compensation { get; set; }
}
