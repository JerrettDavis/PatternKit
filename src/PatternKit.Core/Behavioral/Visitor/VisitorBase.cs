namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// Abstract base class for implementing the Gang of Four Visitor pattern with double dispatch.
/// Provides infrastructure for visiting element hierarchies with type-safe handlers.
/// </summary>
/// <remarks>
/// <para>
/// This class implements the true GoF Visitor pattern using double dispatch:
/// 1. Client calls visitor.Visit(element)
/// 2. Visit calls element.Accept(this)
/// 3. Element's Accept calls visitor.Visit(this) with concrete type
/// 4. Correct Visit overload is resolved at compile time
/// </para>
/// <para>
/// <b>Usage pattern:</b>
/// <code>
/// // 1. Define element hierarchy implementing IVisitable
/// public abstract class Expression : IVisitable { ... }
/// public class NumberExpr : Expression { ... }
/// public class AddExpr : Expression { ... }
///
/// // 2. Create concrete visitor
/// public class EvaluatorVisitor : VisitorBase&lt;Expression, int&gt;
/// {
///     public int Visit(NumberExpr n) => n.Value;
///     public int Visit(AddExpr a) => Visit(a.Left) + Visit(a.Right);
/// }
///
/// // 3. Use visitor
/// var result = new EvaluatorVisitor().Visit(expression);
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TElement">The base type of elements this visitor can visit.</typeparam>
/// <typeparam name="TResult">The type of result produced by visiting.</typeparam>
public abstract class VisitorBase<TElement, TResult> : IVisitor<TResult>
    where TElement : IVisitable
{
    /// <summary>
    /// Visits an element using double dispatch.
    /// The element's Accept method will call back to the appropriate Visit overload.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    /// <returns>The result of visiting the element.</returns>
    public TResult Visit(TElement element)
        => element.Accept<TResult>(this);

    /// <summary>
    /// Default handler for elements without a specific Visit overload.
    /// Override this to provide custom handling for unknown element types.
    /// </summary>
    /// <param name="element">The unhandled element.</param>
    /// <returns>The default result.</returns>
    /// <exception cref="NotSupportedException">Thrown when no handler is defined for the element type.</exception>
    public virtual TResult VisitDefault(IVisitable element)
        => throw new NotSupportedException($"No visitor method defined for {element.GetType().Name}");
}

/// <summary>
/// Abstract base class for visitors that perform actions without returning a result.
/// </summary>
/// <typeparam name="TElement">The base type of elements this visitor can visit.</typeparam>
public abstract class ActionVisitorBase<TElement> : IActionVisitor
    where TElement : IActionVisitable
{
    /// <summary>
    /// Visits an element using double dispatch.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    public void Visit(TElement element)
        => element.Accept(this);

    /// <summary>
    /// Default handler for elements without a specific Visit overload.
    /// </summary>
    /// <param name="element">The unhandled element.</param>
    /// <exception cref="NotSupportedException">Thrown when no handler is defined for the element type.</exception>
    public virtual void VisitDefault(IActionVisitable element)
        => throw new NotSupportedException($"No visitor method defined for {element.GetType().Name}");
}
