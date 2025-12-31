namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// Represents an element that can accept a visitor for double dispatch.
/// This is the core interface for implementing the Gang of Four Visitor pattern.
/// </summary>
/// <remarks>
/// <para>
/// The Visitor pattern uses double dispatch to determine behavior based on both
/// the concrete type of the element and the concrete type of the visitor.
/// Elements implement this interface and call back to the visitor in their Accept method.
/// </para>
/// <para>
/// <b>Example implementation:</b>
/// <code>
/// public class ConcreteElement : IVisitable
/// {
///     public TResult Accept&lt;TResult&gt;(IVisitor&lt;TResult&gt; visitor)
///         => visitor.Visit(this);
/// }
/// </code>
/// </para>
/// </remarks>
public interface IVisitable
{
    /// <summary>
    /// Accepts a visitor and returns the result of the visitation.
    /// Implementations should call the appropriate Visit method on the visitor.
    /// </summary>
    /// <typeparam name="TResult">The type of result produced by the visitor.</typeparam>
    /// <param name="visitor">The visitor to accept.</param>
    /// <returns>The result of the visitation.</returns>
    TResult Accept<TResult>(IVisitor<TResult> visitor);
}

/// <summary>
/// Represents an element that can accept an action visitor (no return value).
/// </summary>
public interface IActionVisitable
{
    /// <summary>
    /// Accepts a visitor that performs an action without returning a result.
    /// </summary>
    /// <param name="visitor">The visitor to accept.</param>
    void Accept(IActionVisitor visitor);
}
