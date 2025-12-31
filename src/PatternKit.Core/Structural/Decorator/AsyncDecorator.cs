namespace PatternKit.Structural.Decorator;

/// <summary>
/// Fluent, allocation-light async decorator that wraps a component and applies layered enhancements.
/// Build once, then call <see cref="ExecuteAsync"/> to run the component through the decorator pipeline.
/// </summary>
/// <typeparam name="TIn">Input type passed to the component.</typeparam>
/// <typeparam name="TOut">Output type produced by the component.</typeparam>
/// <remarks>
/// <para>
/// <b>Mental model</b>: A base async <i>component</i> is wrapped by zero or more async <i>decorators</i>.
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/>, the decorator chain is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var decorator = AsyncDecorator&lt;int, int&gt;.Create(async (x, ct) => await ComputeAsync(x, ct))
///     .Before(async (x, ct) => x + 1)
///     .After(async (input, result, ct) => result * 10)
///     .Build();
///
/// var result = await decorator.ExecuteAsync(5);
/// </code>
/// </example>
public sealed class AsyncDecorator<TIn, TOut>
{
    /// <summary>
    /// Async component delegate.
    /// </summary>
    public delegate ValueTask<TOut> Component(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for transforming input before it reaches the next layer.
    /// </summary>
    public delegate ValueTask<TIn> BeforeTransform(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for transforming output after it returns from the next layer.
    /// </summary>
    public delegate ValueTask<TOut> AfterTransform(TIn input, TOut output, CancellationToken ct);

    /// <summary>
    /// Async delegate for wrapping the entire execution with custom logic.
    /// </summary>
    public delegate ValueTask<TOut> AroundTransform(TIn input, CancellationToken ct, Component next);

    private enum DecoratorType : byte { Before, After, Around }

    private readonly Component _component;
    private readonly DecoratorType[] _types;
    private readonly object[] _decorators;

    private AsyncDecorator(Component component, DecoratorType[] types, object[] decorators)
    {
        _component = component;
        _types = types;
        _decorators = decorators;
    }

    /// <summary>
    /// Executes the decorated component asynchronously.
    /// </summary>
    public ValueTask<TOut> ExecuteAsync(TIn input, CancellationToken ct = default)
        => _types.Length == 0 ? _component(input, ct) : ExecuteLayerAsync(input, ct, 0);

    private async ValueTask<TOut> ExecuteLayerAsync(TIn input, CancellationToken ct, int index)
    {
        if (index >= _types.Length)
            return await _component(input, ct).ConfigureAwait(false);

        return _types[index] switch
        {
            DecoratorType.Before => await ExecuteBeforeLayerAsync(input, ct, index).ConfigureAwait(false),
            DecoratorType.After => await ExecuteAfterLayerAsync(input, ct, index).ConfigureAwait(false),
            DecoratorType.Around => await ExecuteAroundLayerAsync(input, ct, index).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Unknown decorator type.")
        };
    }

    private async ValueTask<TOut> ExecuteBeforeLayerAsync(TIn input, CancellationToken ct, int index)
    {
        var transform = (BeforeTransform)_decorators[index];
        var transformedInput = await transform(input, ct).ConfigureAwait(false);
        return await ExecuteLayerAsync(transformedInput, ct, index + 1).ConfigureAwait(false);
    }

    private async ValueTask<TOut> ExecuteAfterLayerAsync(TIn input, CancellationToken ct, int index)
    {
        var transform = (AfterTransform)_decorators[index];
        var output = await ExecuteLayerAsync(input, ct, index + 1).ConfigureAwait(false);
        return await transform(input, output, ct).ConfigureAwait(false);
    }

    private async ValueTask<TOut> ExecuteAroundLayerAsync(TIn input, CancellationToken ct, int index)
    {
        var transform = (AroundTransform)_decorators[index];
        return await transform(input, ct, NextAsync).ConfigureAwait(false);

        ValueTask<TOut> NextAsync(TIn inp, CancellationToken token) => ExecuteLayerAsync(inp, token, index + 1);
    }

    /// <summary>
    /// Creates a new builder for constructing an async decorated component.
    /// </summary>
    public static Builder Create(Component component) => new(component);

    /// <summary>
    /// Fluent builder for <see cref="AsyncDecorator{TIn, TOut}"/>.
    /// </summary>
    public sealed class Builder
    {
        private readonly Component _component;
        private readonly List<DecoratorType> _types = new(4);
        private readonly List<object> _decorators = new(4);

        internal Builder(Component component)
        {
            _component = component ?? throw new ArgumentNullException(nameof(component));
        }

        /// <summary>
        /// Adds an async decorator that transforms input before it reaches the next layer.
        /// </summary>
        public Builder Before(BeforeTransform transform)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            _types.Add(DecoratorType.Before);
            _decorators.Add(transform);
            return this;
        }

        /// <summary>
        /// Adds a sync decorator that transforms input before it reaches the next layer.
        /// </summary>
        public Builder Before(Func<TIn, TIn> transform)
        {
            return Before(Adapter);
            ValueTask<TIn> Adapter(TIn x, CancellationToken _) => new(transform(x));
        }

        /// <summary>
        /// Adds an async decorator that transforms output after it returns from the next layer.
        /// </summary>
        public Builder After(AfterTransform transform)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            _types.Add(DecoratorType.After);
            _decorators.Add(transform);
            return this;
        }

        /// <summary>
        /// Adds a sync decorator that transforms output after it returns from the next layer.
        /// </summary>
        public Builder After(Func<TIn, TOut, TOut> transform)
        {
            return After(Adapter);
            ValueTask<TOut> Adapter(TIn x, TOut o, CancellationToken _) => new(transform(x, o));
        }

        /// <summary>
        /// Adds an async decorator that wraps the entire execution with custom logic.
        /// </summary>
        public Builder Around(AroundTransform transform)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            _types.Add(DecoratorType.Around);
            _decorators.Add(transform);
            return this;
        }

        /// <summary>
        /// Builds an immutable async decorator with the registered decorators.
        /// </summary>
        public AsyncDecorator<TIn, TOut> Build()
            => new(_component, _types.ToArray(), _decorators.ToArray());
    }
}
