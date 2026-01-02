namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// Represents an element that can accept an async visitor for double dispatch.
/// This is the async counterpart to <see cref="IVisitable"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use this interface when element visitation needs to perform async work.
/// </para>
/// <para>
/// <b>Example implementation:</b>
/// <code>
/// public class AsyncElement : IAsyncVisitable
/// {
///     public ValueTask&lt;TResult&gt; AcceptAsync&lt;TResult&gt;(IAsyncVisitor&lt;TResult&gt; visitor, CancellationToken ct)
///         => visitor.VisitAsync(this, ct);
/// }
/// </code>
/// </para>
/// </remarks>
public interface IAsyncVisitable
{
    /// <summary>
    /// Accepts an async visitor and returns the result of the visitation.
    /// </summary>
    /// <typeparam name="TResult">The type of result produced by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the visitation.</returns>
    ValueTask<TResult> AcceptAsync<TResult>(IAsyncVisitor<TResult> visitor, CancellationToken ct = default);
}

/// <summary>
/// Represents an element that can accept an async action visitor (no return value).
/// </summary>
public interface IAsyncActionVisitable
{
    /// <summary>
    /// Accepts an async visitor that performs an action without returning a result.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask AcceptAsync(IAsyncActionVisitor visitor, CancellationToken ct = default);
}
