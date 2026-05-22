namespace PatternKit.Generators.SchedulerAgentSupervisor;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateSchedulerAgentSupervisorAttribute(Type workType, Type resultType) : Attribute
{
    public Type WorkType { get; } = workType ?? throw new ArgumentNullException(nameof(workType));

    public Type ResultType { get; } = resultType ?? throw new ArgumentNullException(nameof(resultType));

    public string FactoryMethodName { get; set; } = "Create";

    public string SupervisorName { get; set; } = "scheduler-agent-supervisor";

    public int MaxAttempts { get; set; } = 3;

    public int RetryDelayMilliseconds { get; set; } = 1000;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SchedulerAgentAttribute(string name) : Attribute
{
    public string Name { get; } = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Agent name is required.", nameof(name)) : name;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class SchedulerRetryWhenAttribute : Attribute
{
}
