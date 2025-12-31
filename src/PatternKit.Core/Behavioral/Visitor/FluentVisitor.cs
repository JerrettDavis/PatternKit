using System.Collections.Concurrent;

namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// A fluent, composable visitor that uses double dispatch for the Gang of Four Visitor pattern.
/// Unlike <see cref="TypeDispatcher.TypeDispatcher{TBase, TResult}"/>, this implementation
/// requires elements to implement <see cref="IVisitable"/> and uses true double dispatch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Key differences from TypeDispatcher:</b>
/// <list type="bullet">
/// <item>TypeDispatcher: Single dispatch using runtime type checks (node is T)</item>
/// <item>FluentVisitor: Double dispatch via Accept/Visit pattern</item>
/// </list>
/// </para>
/// <para>
/// <b>When to use FluentVisitor:</b>
/// <list type="bullet">
/// <item>When elements are designed to be visited (implement IVisitable)</item>
/// <item>When you need to add new operations without modifying element classes</item>
/// <item>When you have an M:N relationship between visitors and elements</item>
/// </list>
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// // Elements implement IVisitable
/// public class Paragraph : IVisitable
/// {
///     public TResult Accept&lt;TResult&gt;(IVisitor&lt;TResult&gt; visitor)
///         => visitor is IDocumentVisitor&lt;TResult&gt; dv
///             ? dv.Visit(this)
///             : visitor.VisitDefault(this);
/// }
///
/// // Create visitor with fluent builder
/// var htmlRenderer = FluentVisitor&lt;Document, string&gt;.Create()
///     .When&lt;Paragraph&gt;(p => $"&lt;p&gt;{p.Text}&lt;/p&gt;")
///     .When&lt;Image&gt;(i => $"&lt;img src='{i.Url}'/&gt;")
///     .Default(_ => "")
///     .Build();
///
/// // Visit using double dispatch
/// string html = htmlRenderer.Visit(document);
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TElement">The base type of elements (must implement IVisitable).</typeparam>
/// <typeparam name="TResult">The type of result produced by visiting.</typeparam>
public sealed class FluentVisitor<TElement, TResult> : IVisitor<TResult>
    where TElement : class, IVisitable
{
    private readonly ConcurrentDictionary<Type, Delegate> _handlers;
    private readonly Func<TElement, TResult>? _default;

    private FluentVisitor(ConcurrentDictionary<Type, Delegate> handlers, Func<TElement, TResult>? @default)
    {
        _handlers = handlers;
        _default = @default;
    }

    /// <summary>
    /// Visits an element using double dispatch.
    /// The element's Accept method will call back to this visitor.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    /// <returns>The result of visiting the element.</returns>
    public TResult Visit(TElement element)
        => element.Accept<TResult>(this);

    /// <summary>
    /// Handles a specific element type. Called by the element's Accept method.
    /// </summary>
    /// <typeparam name="T">The concrete element type.</typeparam>
    /// <param name="element">The element to handle.</param>
    /// <returns>The result of handling the element.</returns>
    public TResult Handle<T>(T element) where T : TElement
    {
        if (_handlers.TryGetValue(typeof(T), out var handler))
            return ((Func<T, TResult>)handler)(element);

        if (_default is not null)
            return _default(element);

        throw new NotSupportedException($"No handler registered for {typeof(T).Name}");
    }

    /// <inheritdoc/>
    TResult IVisitor<TResult>.VisitDefault(IVisitable element)
    {
        if (_default is not null && element is TElement e)
            return _default(e);

        throw new NotSupportedException($"No default handler for {element.GetType().Name}");
    }

    /// <summary>Creates a new fluent builder for a visitor.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for composing a visitor from individual type handlers.</summary>
    public sealed class Builder
    {
        private readonly ConcurrentDictionary<Type, Delegate> _handlers = new();
        private Func<TElement, TResult>? _default;

        /// <summary>
        /// Registers a handler for elements of type <typeparamref name="T"/>.
        /// The handler will be invoked via double dispatch when visiting elements of this type.
        /// </summary>
        /// <typeparam name="T">The concrete element type to handle.</typeparam>
        /// <param name="handler">The handler function.</param>
        /// <returns>This builder for method chaining.</returns>
        public Builder When<T>(Func<T, TResult> handler) where T : TElement
        {
            _handlers[typeof(T)] = handler;
            return this;
        }

        /// <summary>
        /// Registers a constant result for elements of type <typeparamref name="T"/>.
        /// </summary>
        public Builder When<T>(TResult constant) where T : TElement
            => When<T>(_ => constant);

        /// <summary>
        /// Sets a default handler for elements without a specific handler.
        /// </summary>
        /// <param name="handler">The default handler function.</param>
        /// <returns>This builder for method chaining.</returns>
        public Builder Default(Func<TElement, TResult> handler)
        {
            _default = handler;
            return this;
        }

        /// <summary>
        /// Sets a constant default result.
        /// </summary>
        public Builder Default(TResult constant)
            => Default(_ => constant);

        /// <summary>
        /// Builds the immutable visitor.
        /// </summary>
        /// <returns>A new visitor instance.</returns>
        public FluentVisitor<TElement, TResult> Build()
            => new(_handlers, _default);
    }
}

/// <summary>
/// A fluent, composable visitor that performs actions without returning a result.
/// </summary>
/// <typeparam name="TElement">The base type of elements (must implement IActionVisitable).</typeparam>
public sealed class FluentActionVisitor<TElement> : IActionVisitor
    where TElement : class, IActionVisitable
{
    private readonly ConcurrentDictionary<Type, Delegate> _handlers;
    private readonly Action<TElement>? _default;

    private FluentActionVisitor(ConcurrentDictionary<Type, Delegate> handlers, Action<TElement>? @default)
    {
        _handlers = handlers;
        _default = @default;
    }

    /// <summary>
    /// Visits an element using double dispatch.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    public void Visit(TElement element)
        => element.Accept(this);

    /// <summary>
    /// Handles a specific element type. Called by the element's Accept method.
    /// </summary>
    public void Handle<T>(T element) where T : TElement
    {
        if (_handlers.TryGetValue(typeof(T), out var handler))
        {
            ((Action<T>)handler)(element);
            return;
        }

        if (_default is not null)
        {
            _default(element);
            return;
        }

        throw new NotSupportedException($"No handler registered for {typeof(T).Name}");
    }

    /// <inheritdoc/>
    void IActionVisitor.VisitDefault(IActionVisitable element)
    {
        if (_default is not null && element is TElement e)
        {
            _default(e);
            return;
        }

        throw new NotSupportedException($"No default handler for {element.GetType().Name}");
    }

    /// <summary>Creates a new fluent builder.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for composing an action visitor.</summary>
    public sealed class Builder
    {
        private readonly ConcurrentDictionary<Type, Delegate> _handlers = new();
        private Action<TElement>? _default;

        /// <summary>
        /// Registers an action for elements of type <typeparamref name="T"/>.
        /// </summary>
        public Builder When<T>(Action<T> handler) where T : TElement
        {
            _handlers[typeof(T)] = handler;
            return this;
        }

        /// <summary>
        /// Sets a default action for elements without a specific handler.
        /// </summary>
        public Builder Default(Action<TElement> handler)
        {
            _default = handler;
            return this;
        }

        /// <summary>
        /// Builds the immutable visitor.
        /// </summary>
        public FluentActionVisitor<TElement> Build()
            => new(_handlers, _default);
    }
}
