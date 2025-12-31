namespace PatternKit.Structural.Decorator;

/// <summary>
/// Fluent, allocation-light async action decorator that wraps a component and applies layered enhancements
/// for async side effects (void-returning async operations).
/// Build once, then call <see cref="ExecuteAsync"/> to run the component through the decorator pipeline.
/// </summary>
/// <typeparam name="TIn">Input type passed to the component.</typeparam>
/// <remarks>
/// <para>
/// <b>Mental model</b>: A base async <i>action</i> is wrapped by zero or more async <i>decorators</i>.
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/>, the decorator chain is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var decorator = AsyncActionDecorator&lt;string&gt;.Create(
///         async (msg, ct) => await SaveToDbAsync(msg, ct))
///     .Before(async (msg, ct) => $"[{DateTime.UtcNow}] {msg}")
///     .After(async (msg, ct) => await LogAsync("Saved", ct))
///     .Build();
///
/// await decorator.ExecuteAsync("Hello");
/// </code>
/// </example>
public sealed class AsyncActionDecorator<TIn>
{
    /// <summary>
    /// Async component action delegate.
    /// </summary>
    public delegate ValueTask Component(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for transforming input before it reaches the next layer.
    /// </summary>
    public delegate ValueTask<TIn> BeforeTransform(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for executing logic after the next layer completes.
    /// </summary>
    public delegate ValueTask AfterAction(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for wrapping the entire execution with custom logic.
    /// </summary>
    public delegate ValueTask AroundTransform(TIn input, CancellationToken ct, Component next);

    private enum DecoratorType : byte { Before, After, Around }

    private readonly Component _component;
    private readonly DecoratorType[] _types;
    private readonly object[] _decorators;

    private AsyncActionDecorator(Component component, DecoratorType[] types, object[] decorators)
    {
        _component = component;
        _types = types;
        _decorators = decorators;
    }

    /// <summary>
    /// Executes the decorated action asynchronously.
    /// </summary>
    public ValueTask ExecuteAsync(TIn input, CancellationToken ct = default)
        => _types.Length == 0 ? _component(input, ct) : ExecuteLayerAsync(input, ct, 0);

    private async ValueTask ExecuteLayerAsync(TIn input, CancellationToken ct, int index)
    {
        if (index >= _types.Length)
        {
            await _component(input, ct).ConfigureAwait(false);
            return;
        }

        switch (_types[index])
        {
            case DecoratorType.Before:
                await ExecuteBeforeLayerAsync(input, ct, index).ConfigureAwait(false);
                break;
            case DecoratorType.After:
                await ExecuteAfterLayerAsync(input, ct, index).ConfigureAwait(false);
                break;
            case DecoratorType.Around:
                await ExecuteAroundLayerAsync(input, ct, index).ConfigureAwait(false);
                break;
        }
    }

    private async ValueTask ExecuteBeforeLayerAsync(TIn input, CancellationToken ct, int index)
    {
        var transform = (BeforeTransform)_decorators[index];
        var transformedInput = await transform(input, ct).ConfigureAwait(false);
        await ExecuteLayerAsync(transformedInput, ct, index + 1).ConfigureAwait(false);
    }

    private async ValueTask ExecuteAfterLayerAsync(TIn input, CancellationToken ct, int index)
    {
        var action = (AfterAction)_decorators[index];
        await ExecuteLayerAsync(input, ct, index + 1).ConfigureAwait(false);
        await action(input, ct).ConfigureAwait(false);
    }

    private async ValueTask ExecuteAroundLayerAsync(TIn input, CancellationToken ct, int index)
    {
        var transform = (AroundTransform)_decorators[index];
        await transform(input, ct, NextAsync).ConfigureAwait(false);

        ValueTask NextAsync(TIn inp, CancellationToken token) => ExecuteLayerAsync(inp, token, index + 1);
    }

    /// <summary>
    /// Creates a new builder for constructing an async decorated action.
    /// </summary>
    public static Builder Create(Component component) => new(component);

    /// <summary>
    /// Fluent builder for <see cref="AsyncActionDecorator{TIn}"/>.
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
        /// Adds an async decorator that executes logic after the next layer completes.
        /// </summary>
        public Builder After(AfterAction action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            _types.Add(DecoratorType.After);
            _decorators.Add(action);
            return this;
        }

        /// <summary>
        /// Adds a sync decorator that executes logic after the next layer completes.
        /// </summary>
        public Builder After(Action<TIn> action)
        {
            return After(Adapter);
            ValueTask Adapter(TIn x, CancellationToken _)
            {
                action(x);
                return default;
            }
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
        /// Builds an immutable async action decorator with the registered decorators.
        /// </summary>
        public AsyncActionDecorator<TIn> Build()
            => new(_component, _types.ToArray(), _decorators.ToArray());
    }
}
