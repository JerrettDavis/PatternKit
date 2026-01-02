namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// Base interface for visitors that produce a result.
/// Concrete visitor interfaces should extend this with specific Visit methods for each element type.
/// </summary>
/// <remarks>
/// <para>
/// The Gang of Four Visitor pattern uses double dispatch: the element's Accept method
/// calls back to the visitor's Visit method, allowing behavior to be determined by
/// both the concrete element type and the concrete visitor type.
/// </para>
/// <para>
/// <b>Example concrete visitor interface:</b>
/// <code>
/// public interface IDocumentVisitor&lt;TResult&gt; : IVisitor&lt;TResult&gt;
/// {
///     TResult Visit(Paragraph p);
///     TResult Visit(Image img);
///     TResult Visit(Table tbl);
/// }
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TResult">The type of result produced by visiting elements.</typeparam>
public interface IVisitor<out TResult>
{
    /// <summary>
    /// Visits an element and returns a result.
    /// This method provides a fallback for unhandled element types.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    /// <returns>The result of visiting the element.</returns>
    TResult VisitDefault(IVisitable element);
}

/// <summary>
/// Base interface for visitors that perform actions without returning a result.
/// </summary>
public interface IActionVisitor
{
    /// <summary>
    /// Visits an element and performs an action.
    /// This method provides a fallback for unhandled element types.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    void VisitDefault(IActionVisitable element);
}
