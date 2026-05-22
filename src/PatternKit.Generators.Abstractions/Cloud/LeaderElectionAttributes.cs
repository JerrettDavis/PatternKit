namespace PatternKit.Generators.LeaderElection;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class GenerateLeaderElectionAttribute(Type contextType) : Attribute
{
    public Type ContextType { get; } = contextType ?? throw new ArgumentNullException(nameof(contextType));

    public string FactoryMethodName { get; set; } = "Create";

    public string ElectionName { get; set; } = "leader-election";

    public int LeaseDurationMilliseconds { get; set; } = 30000;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LeaderCandidateIdAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LeaderAcquiredAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LeaderRenewedAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class LeaderReleasedAttribute : Attribute
{
}
