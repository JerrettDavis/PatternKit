namespace PatternKit.Generators.State;

/// <summary>
/// Marks a method to be invoked when exiting a specific state.
/// Exit hooks are executed before the State property is updated to the new state.
/// The method can be synchronous (void) or asynchronous (ValueTask).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class StateExitAttribute : Attribute
{
    /// <summary>
    /// The state for which this exit hook applies.
    /// </summary>
    public object State { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateExitAttribute"/> class.
    /// </summary>
    /// <param name="state">The state for which this exit hook applies.</param>
    public StateExitAttribute(object state)
    {
        State = state;
    }
}
