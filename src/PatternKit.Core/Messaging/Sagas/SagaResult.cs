namespace PatternKit.Messaging.Sagas;

/// <summary>
/// Result returned after a saga processes a message.
/// </summary>
public sealed class SagaResult<TState>
{
    /// <summary>Creates a saga result.</summary>
    public SagaResult(TState state, bool matched, bool completed)
        => (State, Matched, Completed) = (state, matched, completed);

    /// <summary>The state after message processing.</summary>
    public TState State { get; }

    /// <summary><see langword="true"/> when at least one saga step handled the message.</summary>
    public bool Matched { get; }

    /// <summary><see langword="true"/> when the saga completion policy is satisfied.</summary>
    public bool Completed { get; }
}
