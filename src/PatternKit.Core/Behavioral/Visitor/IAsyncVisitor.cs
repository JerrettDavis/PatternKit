namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// Base interface for async visitors that produce a result.
/// This is the async counterpart to <see cref="IVisitor{TResult}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use this interface when visitor operations need to perform async work.
/// </para>
/// <para>
/// <b>Example concrete async visitor interface:</b>
/// <code>
/// public interface IAsyncDocumentVisitor&lt;TResult&gt; : IAsyncVisitor&lt;TResult&gt;
/// {
///     ValueTask&lt;TResult&gt; VisitAsync(Paragraph p, CancellationToken ct);
///     ValueTask&lt;TResult&gt; VisitAsync(Image img, CancellationToken ct);
/// }
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TResult">The type of result produced by visiting elements.</typeparam>
public interface IAsyncVisitor<TResult>
{
    /// <summary>
    /// Visits an element asynchronously and returns a result.
    /// This method provides a fallback for unhandled element types.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of visiting the element.</returns>
    ValueTask<TResult> VisitDefaultAsync(IAsyncVisitable element, CancellationToken ct = default);
}

/// <summary>
/// Base interface for async visitors that perform actions without returning a result.
/// </summary>
public interface IAsyncActionVisitor
{
    /// <summary>
    /// Visits an element asynchronously and performs an action.
    /// This method provides a fallback for unhandled element types.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask VisitDefaultAsync(IAsyncActionVisitable element, CancellationToken ct = default);
}
