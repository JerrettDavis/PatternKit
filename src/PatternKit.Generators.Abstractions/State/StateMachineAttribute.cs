namespace PatternKit.Generators.State;

/// <summary>
/// Marks a partial type as a state machine host that will generate Fire/FireAsync/CanFire methods
/// for deterministic state transitions based on annotated transition, guard, and hook methods.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class StateMachineAttribute : Attribute
{
    /// <summary>
    /// The state enum type (must be an enum in v1).
    /// </summary>
    public Type StateType { get; }

    /// <summary>
    /// The trigger enum type (must be an enum in v1).
    /// </summary>
    public Type TriggerType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateMachineAttribute"/> class.
    /// </summary>
    /// <param name="stateType">The state enum type.</param>
    /// <param name="triggerType">The trigger enum type.</param>
    public StateMachineAttribute(Type stateType, Type triggerType)
    {
        StateType = stateType;
        TriggerType = triggerType;
    }

    /// <summary>
    /// Gets or sets the name of the generated Fire method. Default: "Fire".
    /// </summary>
    public string FireMethodName { get; set; } = "Fire";

    /// <summary>
    /// Gets or sets the name of the generated FireAsync method. Default: "FireAsync".
    /// </summary>
    public string FireAsyncMethodName { get; set; } = "FireAsync";

    /// <summary>
    /// Gets or sets the name of the generated CanFire method. Default: "CanFire".
    /// </summary>
    public string CanFireMethodName { get; set; } = "CanFire";

    /// <summary>
    /// Gets or sets whether to generate async methods.
    /// When null (default), async generation is inferred from the presence of async transitions/hooks.
    /// </summary>
    public bool? GenerateAsync { get; set; }

    /// <summary>
    /// Gets or sets whether to force async generation even if all transitions/hooks are synchronous.
    /// Default is false.
    /// </summary>
    public bool ForceAsync { get; set; }

    /// <summary>
    /// Gets or sets the policy for handling invalid triggers.
    /// Default is Throw.
    /// </summary>
    public StateMachineInvalidTriggerPolicy InvalidTrigger { get; set; } = StateMachineInvalidTriggerPolicy.Throw;

    /// <summary>
    /// Gets or sets the policy for handling guard failures.
    /// Default is Throw.
    /// </summary>
    public StateMachineGuardFailurePolicy GuardFailure { get; set; } = StateMachineGuardFailurePolicy.Throw;
}

/// <summary>
/// Defines the policy for handling invalid triggers.
/// </summary>
public enum StateMachineInvalidTriggerPolicy
{
    /// <summary>
    /// Throw an InvalidOperationException when an invalid trigger is fired.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Ignore invalid triggers (no-op).
    /// </summary>
    Ignore = 1,

    /// <summary>
    /// Return false from CanFire and no-op in Fire for invalid triggers.
    /// </summary>
    ReturnFalse = 2
}

/// <summary>
/// Defines the policy for handling guard failures.
/// </summary>
public enum StateMachineGuardFailurePolicy
{
    /// <summary>
    /// Throw an InvalidOperationException when a guard returns false.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Ignore guard failures (no-op, state does not transition).
    /// </summary>
    Ignore = 1,

    /// <summary>
    /// Return false from CanFire and no-op in Fire when guard fails.
    /// </summary>
    ReturnFalse = 2
}
