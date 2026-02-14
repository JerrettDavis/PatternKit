namespace PatternKit.Generators.State;

/// <summary>
/// Marks a method to be invoked when entering a specific state.
/// Entry hooks are executed after the State property is updated to the new state
/// and after the transition action method has been called.
/// The method can be synchronous (void) or asynchronous (ValueTask).
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class StateEntryAttribute : Attribute
{
    /// <summary>
    /// The state for which this entry hook applies.
    /// </summary>
    public object State { get; set; } = null!;

    /// <summary>
    /// Initializes a new instance of the <see cref="StateEntryAttribute"/> class.
    /// </summary>
    /// <param name="state">The state for which this entry hook applies.</param>
    public StateEntryAttribute(object state)
    {
        State = state;
    }
}
