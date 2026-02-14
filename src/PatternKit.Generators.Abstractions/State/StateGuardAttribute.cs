namespace PatternKit.Generators.State;

/// <summary>
/// Marks a method as a guard condition for a state transition.
/// The method must return bool or ValueTask&lt;bool&gt; and is evaluated
/// before the transition occurs. If the guard returns false, the transition
/// is prevented according to the GuardFailurePolicy.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class StateGuardAttribute : Attribute
{
    /// <summary>
    /// The state from which this guard applies.
    /// </summary>
    public object From { get; set; } = null!;

    /// <summary>
    /// The trigger for which this guard applies.
    /// </summary>
    public object Trigger { get; set; } = null!;
}
