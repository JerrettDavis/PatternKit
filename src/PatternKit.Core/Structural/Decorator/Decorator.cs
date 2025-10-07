namespace PatternKit.Structural.Decorator;

/// <summary>
/// Fluent, allocation-light decorator that wraps a component and applies layered enhancements via ordered decorators.
/// Build once, then call <see cref="Execute"/> to run the component through the decorator pipeline.
/// </summary>
/// <typeparam name="TIn">Input type passed to the component.</typeparam>
/// <typeparam name="TOut">Output type produced by the component.</typeparam>
/// <remarks>
/// <para>
/// <b>Mental model</b>: A base <i>component</i> is wrapped by zero or more <i>decorators</i>. Each decorator can:
/// <list type="bullet">
/// <item><description>Transform the input before passing it to the next layer (<see cref="BeforeTransform"/>).</description></item>
///   <item><description>Transform the output after receiving it from the next layer (<see cref="AfterTransform"/>).</description></item>
///   <item><description>Wrap the entire execution with custom logic (<see cref="AroundTransform"/>).</description></item>
/// </list>
/// Decorators are applied in the order they are registered. The innermost layer is the base component.
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/>, the decorator chain is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var decorator = Decorator&lt;int, int&gt;.Create(static x => x * 2)
///     .Before(static x => x + 1)  // Add 1 before
///     .After(static x => x * 10)   // Multiply result by 10
///     .Build();
///
/// var result = decorator.Execute(5); // ((5 + 1) * 2) * 10 = 120
/// </code>
/// </example>
public sealed class Decorator<TIn, TOut>
{
    /// <summary>
    /// Delegate representing the base component operation that transforms input to output.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <returns>The computed result.</returns>
    public delegate TOut Component(TIn input);

    /// <summary>
    /// Delegate for transforming the input before it reaches the next layer.
    /// </summary>
    /// <param name="input">The original input value.</param>
    /// <returns>The transformed input value.</returns>
    public delegate TIn BeforeTransform(TIn input);

    /// <summary>
    /// Delegate for transforming the output after it returns from the next layer.
    /// </summary>
    /// <param name="input">The original input value.</param>
    /// <param name="output">The output from the next layer.</param>
    /// <returns>The transformed output value.</returns>
    public delegate TOut AfterTransform(TIn input, TOut output);

    /// <summary>
    /// Delegate for wrapping the entire execution with custom logic (e.g., logging, error handling).
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="next">Delegate to invoke the next layer in the chain.</param>
    /// <returns>The result after applying the wrapper logic.</returns>
    public delegate TOut AroundTransform(TIn input, Component next);

    private enum DecoratorType : byte { Before, After, Around }

    private readonly Component _component;
    private readonly DecoratorType[] _types;
    private readonly object[] _decorators; // Holds BeforeTransform, AfterTransform, or AroundTransform

    private Decorator(Component component, DecoratorType[] types, object[] decorators)
    {
        _component = component;
        _types = types;
        _decorators = decorators;
    }

    /// <summary>
    /// Executes the decorated component with the given <paramref name="input"/>.
    /// </summary>
    /// <param name="input">The input value (readonly via <c>in</c>).</param>
    /// <returns>
    /// The result after applying all decorators in order, ending with the base component execution.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The execution flows outward-to-inward for <see cref="BeforeTransform"/> decorators (transforming input),
    /// then executes the base component, then flows inward-to-outward for <see cref="AfterTransform"/> decorators
    /// (transforming output). <see cref="AroundTransform"/> decorators control the entire flow at their layer.
    /// </para>
    /// </remarks>
    public TOut Execute(in TIn input)
        => _types.Length == 0 ? _component(input) : ExecuteLayer(input, 0);

    private TOut ExecuteLayer(TIn input, int index)
    {
        if (index >= _types.Length)
            return _component(input);

        return _types[index] switch
        {
            DecoratorType.Before => ExecuteBeforeLayer(input, index),
            DecoratorType.After => ExecuteAfterLayer(input, index),
            DecoratorType.Around => ExecuteAroundLayer(input, index),
            _ => throw new InvalidOperationException("Unknown decorator type.")
        };
    }

    private TOut ExecuteBeforeLayer(TIn input, int index)
    {
        var transform = (BeforeTransform)_decorators[index];
        var transformedInput = transform(input);
        return ExecuteLayer(transformedInput, index + 1);
    }

    private TOut ExecuteAfterLayer(TIn input, int index)
    {
        var transform = (AfterTransform)_decorators[index];
        var output = ExecuteLayer(input, index + 1);
        return transform(input, output);
    }

    private TOut ExecuteAroundLayer(TIn input, int index)
    {
        var transform = (AroundTransform)_decorators[index];

        return transform(input, Next);

        TOut Next(TIn inp) => ExecuteLayer(inp, index + 1);
    }

    /// <summary>
    /// Creates a new <see cref="Builder"/> for constructing a decorated component.
    /// </summary>
    /// <param name="component">The base component to decorate.</param>
    /// <returns>A new <see cref="Builder"/> instance.</returns>
    /// <example>
    /// <code language="csharp">
    /// var dec = Decorator&lt;string, int&gt;.Create(static s => s.Length)
    ///     .Before(static s => s.Trim())
    ///     .After(static (_, len) => len * 2)
    ///     .Build();
    ///
    /// var result = dec.Execute("  hello  "); // 10
    /// </code>
    /// </example>
    public static Builder Create(Component component) => new(component);

    /// <summary>
    /// Fluent builder for <see cref="Decorator{TIn, TOut}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The builder collects decorators in registration order. Decorators are applied from outermost to innermost
    /// for input transformation, and innermost to outermost for output transformation.
    /// </para>
    /// <para>
    /// Builders are mutable and not thread-safe. Each call to <see cref="Build"/> snapshots the current
    /// decorator chain into an immutable instance.
    /// </para>
    /// </remarks>
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
        /// Adds a decorator that transforms the input before it reaches the next layer.
        /// </summary>
        /// <param name="transform">The transformation function.</param>
        /// <returns>The same builder instance for chaining.</returns>
        /// <remarks>
        /// Multiple Before decorators are applied in registration order (outermost first).
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// .Before(static x => x + 10)
        /// .Before(static x => x * 2)  // Applied after the first Before
        /// </code>
        /// </example>
        public Builder Before(BeforeTransform transform)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            _types.Add(DecoratorType.Before);
            _decorators.Add(transform);
            return this;
        }

        /// <summary>
        /// Adds a decorator that transforms the output after it returns from the next layer.
        /// </summary>
        /// <param name="transform">The transformation function.</param>
        /// <returns>The same builder instance for chaining.</returns>
        /// <remarks>
        /// Multiple After decorators are applied in registration order (innermost first on the way out).
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// .After(static (_, result) => result * 2)
        /// .After(static (_, result) => result + 100)  // Applied to the result of the first After
        /// </code>
        /// </example>
        public Builder After(AfterTransform transform)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            _types.Add(DecoratorType.After);
            _decorators.Add(transform);
            return this;
        }

        /// <summary>
        /// Adds a decorator that wraps the entire execution with custom logic.
        /// </summary>
        /// <param name="transform">The wrapper function that receives the input and a delegate to the next layer.</param>
        /// <returns>The same builder instance for chaining.</returns>
        /// <remarks>
        /// <para>
        /// Around decorators have full control over whether and how the next layer is invoked.
        /// This is useful for cross-cutting concerns like logging, caching, error handling, or retries.
        /// </para>
        /// <para>
        /// The <paramref name="transform"/> delegate receives:
        /// <list type="bullet">
        ///   <item><description>The input value.</description></item>
        ///   <item><description>A <see cref="Component"/> delegate representing the next layer (call it to proceed).</description></item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <example>
        /// <code language="csharp">
        /// .Around((in int x, next) => {
        ///     Console.WriteLine($"Before: {x}");
        ///     var result = next(in x);
        ///     Console.WriteLine($"After: {result}");
        ///     return result;
        /// })
        /// </code>
        /// </example>
        public Builder Around(AroundTransform transform)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            _types.Add(DecoratorType.Around);
            _decorators.Add(transform);
            return this;
        }

        /// <summary>
        /// Builds an immutable <see cref="Decorator{TIn, TOut}"/> with the registered decorators.
        /// </summary>
        /// <returns>A new <see cref="Decorator{TIn, TOut}"/> instance.</returns>
        /// <remarks>
        /// The builder can be reused after calling <see cref="Build"/> to create variations with additional decorators.
        /// </remarks>
        public Decorator<TIn, TOut> Build()
            => new(_component, _types.ToArray(), _decorators.ToArray());
    }
}
