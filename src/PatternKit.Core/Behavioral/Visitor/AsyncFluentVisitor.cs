using System.Collections.Concurrent;

namespace PatternKit.Behavioral.Visitor;

/// <summary>
/// An async fluent, composable visitor that uses double dispatch for the Gang of Four Visitor pattern.
/// This is the async counterpart to <see cref="FluentVisitor{TElement,TResult}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Use when visiting operations need to perform async work (API calls, database lookups, etc.).
/// </para>
/// <para>
/// <b>Example:</b>
/// <code>
/// var asyncRenderer = AsyncFluentVisitor&lt;Document, string&gt;.Create()
///     .When&lt;Paragraph&gt;(async (p, ct) => await RenderParagraphAsync(p, ct))
///     .When&lt;Image&gt;(async (i, ct) => await DownloadAndRenderImageAsync(i, ct))
///     .Default(async (e, ct) => "")
///     .Build();
///
/// string html = await asyncRenderer.VisitAsync(document);
/// </code>
/// </para>
/// </remarks>
/// <typeparam name="TElement">The base type of elements (must implement IAsyncVisitable).</typeparam>
/// <typeparam name="TResult">The type of result produced by visiting.</typeparam>
public sealed class AsyncFluentVisitor<TElement, TResult> : IAsyncVisitor<TResult>
    where TElement : class, IAsyncVisitable
{
    /// <summary>
    /// Async handler delegate.
    /// </summary>
    public delegate ValueTask<TResult> AsyncHandler<in T>(T element, CancellationToken ct) where T : TElement;

    private readonly ConcurrentDictionary<Type, Delegate> _handlers;
    private readonly Func<TElement, CancellationToken, ValueTask<TResult>>? _default;

    private AsyncFluentVisitor(
        ConcurrentDictionary<Type, Delegate> handlers,
        Func<TElement, CancellationToken, ValueTask<TResult>>? @default)
    {
        _handlers = handlers;
        _default = @default;
    }

    /// <summary>
    /// Visits an element asynchronously using double dispatch.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of visiting the element.</returns>
    public ValueTask<TResult> VisitAsync(TElement element, CancellationToken ct = default)
        => element.AcceptAsync<TResult>(this, ct);

    /// <summary>
    /// Handles a specific element type asynchronously. Called by the element's AcceptAsync method.
    /// </summary>
    /// <typeparam name="T">The concrete element type.</typeparam>
    /// <param name="element">The element to handle.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of handling the element.</returns>
    public ValueTask<TResult> HandleAsync<T>(T element, CancellationToken ct = default) where T : TElement
    {
        if (_handlers.TryGetValue(typeof(T), out var handler))
            return ((AsyncHandler<T>)handler)(element, ct);

        if (_default is not null)
            return _default(element, ct);

        throw new NotSupportedException($"No handler registered for {typeof(T).Name}");
    }

    /// <inheritdoc/>
    ValueTask<TResult> IAsyncVisitor<TResult>.VisitDefaultAsync(IAsyncVisitable element, CancellationToken ct)
    {
        if (_default is not null && element is TElement e)
            return _default(e, ct);

        throw new NotSupportedException($"No default handler for {element.GetType().Name}");
    }

    /// <summary>Creates a new fluent builder for an async visitor.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for composing an async visitor from individual type handlers.</summary>
    public sealed class Builder
    {
        private readonly ConcurrentDictionary<Type, Delegate> _handlers = new();
        private Func<TElement, CancellationToken, ValueTask<TResult>>? _default;

        /// <summary>
        /// Registers an async handler for elements of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The concrete element type to handle.</typeparam>
        /// <param name="handler">The async handler function.</param>
        /// <returns>This builder for method chaining.</returns>
        public Builder When<T>(AsyncHandler<T> handler) where T : TElement
        {
            _handlers[typeof(T)] = handler;
            return this;
        }

        /// <summary>
        /// Registers a sync handler for elements of type <typeparamref name="T"/>.
        /// </summary>
        public Builder When<T>(Func<T, TResult> handler) where T : TElement
        {
            _handlers[typeof(T)] = (AsyncHandler<T>)((e, _) => new ValueTask<TResult>(handler(e)));
            return this;
        }

        /// <summary>
        /// Registers a constant result for elements of type <typeparamref name="T"/>.
        /// </summary>
        public Builder When<T>(TResult constant) where T : TElement
            => When<T>(_ => constant);

        /// <summary>
        /// Sets an async default handler for elements without a specific handler.
        /// </summary>
        /// <param name="handler">The async default handler function.</param>
        /// <returns>This builder for method chaining.</returns>
        public Builder Default(Func<TElement, CancellationToken, ValueTask<TResult>> handler)
        {
            _default = handler;
            return this;
        }

        /// <summary>
        /// Sets a sync default handler.
        /// </summary>
        public Builder Default(Func<TElement, TResult> handler)
        {
            _default = (e, _) => new ValueTask<TResult>(handler(e));
            return this;
        }

        /// <summary>
        /// Sets a constant default result.
        /// </summary>
        public Builder Default(TResult constant)
            => Default(_ => constant);

        /// <summary>
        /// Builds the immutable async visitor.
        /// </summary>
        /// <returns>A new async visitor instance.</returns>
        public AsyncFluentVisitor<TElement, TResult> Build()
            => new(_handlers, _default);
    }
}

/// <summary>
/// An async fluent, composable visitor that performs actions without returning a result.
/// This is the async counterpart to <see cref="FluentActionVisitor{TElement}"/>.
/// </summary>
/// <typeparam name="TElement">The base type of elements (must implement IAsyncActionVisitable).</typeparam>
public sealed class AsyncFluentActionVisitor<TElement> : IAsyncActionVisitor
    where TElement : class, IAsyncActionVisitable
{
    /// <summary>
    /// Async action handler delegate.
    /// </summary>
    public delegate ValueTask AsyncHandler<in T>(T element, CancellationToken ct) where T : TElement;

    private readonly ConcurrentDictionary<Type, Delegate> _handlers;
    private readonly Func<TElement, CancellationToken, ValueTask>? _default;

    private AsyncFluentActionVisitor(
        ConcurrentDictionary<Type, Delegate> handlers,
        Func<TElement, CancellationToken, ValueTask>? @default)
    {
        _handlers = handlers;
        _default = @default;
    }

    /// <summary>
    /// Visits an element asynchronously using double dispatch.
    /// </summary>
    /// <param name="element">The element to visit.</param>
    /// <param name="ct">Cancellation token.</param>
    public ValueTask VisitAsync(TElement element, CancellationToken ct = default)
        => element.AcceptAsync(this, ct);

    /// <summary>
    /// Handles a specific element type asynchronously. Called by the element's AcceptAsync method.
    /// </summary>
    public async ValueTask HandleAsync<T>(T element, CancellationToken ct = default) where T : TElement
    {
        if (_handlers.TryGetValue(typeof(T), out var handler))
        {
            await ((AsyncHandler<T>)handler)(element, ct).ConfigureAwait(false);
            return;
        }

        if (_default is not null)
        {
            await _default(element, ct).ConfigureAwait(false);
            return;
        }

        throw new NotSupportedException($"No handler registered for {typeof(T).Name}");
    }

    /// <inheritdoc/>
    async ValueTask IAsyncActionVisitor.VisitDefaultAsync(IAsyncActionVisitable element, CancellationToken ct)
    {
        if (_default is not null && element is TElement e)
        {
            await _default(e, ct).ConfigureAwait(false);
            return;
        }

        throw new NotSupportedException($"No default handler for {element.GetType().Name}");
    }

    /// <summary>Creates a new fluent builder.</summary>
    public static Builder Create() => new();

    /// <summary>Fluent builder for composing an async action visitor.</summary>
    public sealed class Builder
    {
        private readonly ConcurrentDictionary<Type, Delegate> _handlers = new();
        private Func<TElement, CancellationToken, ValueTask>? _default;

        /// <summary>
        /// Registers an async action for elements of type <typeparamref name="T"/>.
        /// </summary>
        public Builder When<T>(AsyncHandler<T> handler) where T : TElement
        {
            _handlers[typeof(T)] = handler;
            return this;
        }

        /// <summary>
        /// Registers a sync action for elements of type <typeparamref name="T"/>.
        /// </summary>
        public Builder When<T>(Action<T> handler) where T : TElement
        {
            _handlers[typeof(T)] = (AsyncHandler<T>)((e, _) =>
            {
                handler(e);
                return default;
            });
            return this;
        }

        /// <summary>
        /// Sets an async default action for elements without a specific handler.
        /// </summary>
        public Builder Default(Func<TElement, CancellationToken, ValueTask> handler)
        {
            _default = handler;
            return this;
        }

        /// <summary>
        /// Sets a sync default action.
        /// </summary>
        public Builder Default(Action<TElement> handler)
        {
            _default = (e, _) =>
            {
                handler(e);
                return default;
            };
            return this;
        }

        /// <summary>
        /// Builds the immutable async action visitor.
        /// </summary>
        public AsyncFluentActionVisitor<TElement> Build()
            => new(_handlers, _default);
    }
}
