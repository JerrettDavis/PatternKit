using System;

namespace PatternKit.Generators.State;

/// <summary>
/// Marks a partial type as a state machine host.
/// The generator produces a State property, Fire, FireAsync, and CanFire methods
/// based on declared transitions, guards, and hooks.
/// </summary>
/// <remarks>
/// <para>
/// The generated code includes:
/// <list type="bullet">
/// <item>A State property of the state enum type</item>
/// <item>Fire(trigger) for synchronous state transitions</item>
/// <item>FireAsync(trigger, CancellationToken) for asynchronous transitions</item>
/// <item>CanFire(trigger) to check if a transition is valid from the current state</item>
/// </list>
/// </para>
/// <para>
/// Execution order for transitions:
/// ExitHooks(fromState) -> TransitionAction() -> State = toState -> EntryHooks(toState)
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public enum DoorState { Open, Closed, Locked }
/// public enum DoorTrigger { Close, Open, Lock, Unlock }
///
/// [StateMachine(typeof(DoorState), typeof(DoorTrigger))]
/// public partial class Door
/// {
///     [StateTransition(From = DoorState.Open, Trigger = DoorTrigger.Close, To = DoorState.Closed)]
///     private void OnClose() { }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class StateMachineAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="StateMachineAttribute"/>.
    /// </summary>
    /// <param name="stateType">The enum type representing states. Must be an enum.</param>
    /// <param name="triggerType">The enum type representing triggers. Must be an enum.</param>
    public StateMachineAttribute(Type stateType, Type triggerType)
    {
        StateType = stateType;
        TriggerType = triggerType;
    }

    /// <summary>
    /// Gets the enum type representing states.
    /// </summary>
    public Type StateType { get; }

    /// <summary>
    /// Gets the enum type representing triggers.
    /// </summary>
    public Type TriggerType { get; }

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
    /// Gets or sets whether to generate async FireAsync method.
    /// If not explicitly set, inferred from async transition/hook methods.
    /// </summary>
    public bool GenerateAsync { get; set; }

    /// <summary>
    /// Forces generation of async methods even when not otherwise inferred.
    /// </summary>
    public bool ForceAsync { get; set; }

    /// <summary>
    /// Gets or sets the invalid trigger policy. Default: <see cref="StateMachineInvalidTriggerPolicy.Throw"/>.
    /// </summary>
    public StateMachineInvalidTriggerPolicy InvalidTrigger { get; set; } = StateMachineInvalidTriggerPolicy.Throw;

    /// <summary>
    /// Gets or sets the guard failure policy. Default: <see cref="StateMachineGuardFailurePolicy.Throw"/>.
    /// </summary>
    public StateMachineGuardFailurePolicy GuardFailure { get; set; } = StateMachineGuardFailurePolicy.Throw;
}

/// <summary>
/// Marks a method as a state transition action.
/// The method is invoked when the specified trigger is fired from the specified state.
/// </summary>
/// <remarks>
/// Transition methods may return void or ValueTask and accept zero parameters
/// or a CancellationToken parameter for async transitions.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StateTransitionAttribute : Attribute
{
    /// <summary>
    /// The source state (enum value). Must match the state enum type declared on [StateMachine].
    /// </summary>
    public object From { get; set; } = null!;

    /// <summary>
    /// The trigger (enum value). Must match the trigger enum type declared on [StateMachine].
    /// </summary>
    public object Trigger { get; set; } = null!;

    /// <summary>
    /// The destination state (enum value). Must match the state enum type declared on [StateMachine].
    /// </summary>
    public object To { get; set; } = null!;
}

/// <summary>
/// Marks a method as a guard for a state transition.
/// The method must return bool and is called before the transition executes.
/// If it returns false, the transition is blocked.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StateGuardAttribute : Attribute
{
    /// <summary>
    /// The source state (enum value) this guard applies to.
    /// </summary>
    public object From { get; set; } = null!;

    /// <summary>
    /// The trigger (enum value) this guard applies to.
    /// </summary>
    public object Trigger { get; set; } = null!;
}

/// <summary>
/// Marks a method as a state entry hook.
/// The method is invoked whenever the state machine enters the specified state.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StateEntryAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="StateEntryAttribute"/>.
    /// </summary>
    /// <param name="state">The state (enum value) this entry hook applies to.</param>
    public StateEntryAttribute(object state)
    {
        State = state;
    }

    /// <summary>
    /// Gets the state this entry hook applies to.
    /// </summary>
    public object State { get; }
}

/// <summary>
/// Marks a method as a state exit hook.
/// The method is invoked whenever the state machine exits the specified state.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class StateExitAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of <see cref="StateExitAttribute"/>.
    /// </summary>
    /// <param name="state">The state (enum value) this exit hook applies to.</param>
    public StateExitAttribute(object state)
    {
        State = state;
    }

    /// <summary>
    /// Gets the state this exit hook applies to.
    /// </summary>
    public object State { get; }
}
