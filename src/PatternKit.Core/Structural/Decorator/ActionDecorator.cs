namespace PatternKit.Structural.Decorator;

/// <summary>
/// Fluent, allocation-light action decorator that wraps a component and applies layered enhancements
/// for side effects (void-returning operations).
/// Build once, then call <see cref="Execute"/> to run the component through the decorator pipeline.
/// </summary>
/// <typeparam name="TIn">Input type passed to the component.</typeparam>
/// <remarks>
/// <para>
/// <b>Mental model</b>: A base <i>component</i> (action) is wrapped by zero or more <i>decorators</i>. Each decorator can:
/// <list type="bullet">
///   <item><description>Transform the input before passing it to the next layer (<see cref="BeforeTransform"/>).</description></item>
///   <item><description>Execute logic after the next layer completes (<see cref="AfterAction"/>).</description></item>
///   <item><description>Wrap the entire execution with custom logic (<see cref="AroundTransform"/>).</description></item>
/// </list>
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/>, the decorator chain is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var decorator = ActionDecorator&lt;string&gt;.Create(msg => Console.WriteLine(msg))
///     .Before(msg => $"[{DateTime.Now}] {msg}")  // Add timestamp
///     .After(msg => Console.WriteLine("---"))    // Add separator after
///     .Build();
///
/// decorator.Execute("Hello"); // Prints timestamped message and separator
/// </code>
/// </example>
public sealed class ActionDecorator<TIn>
{
    /// <summary>
    /// Delegate representing the base component action.
    /// </summary>
    public delegate void Component(TIn input);

    /// <summary>
    /// Delegate for transforming the input before it reaches the next layer.
    /// </summary>
    public delegate TIn BeforeTransform(TIn input);

    /// <summary>
    /// Delegate for executing logic after the next layer completes.
    /// </summary>
    public delegate void AfterAction(TIn input);

    /// <summary>
    /// Delegate for wrapping the entire execution with custom logic.
    /// </summary>
    public delegate void AroundTransform(TIn input, Component next);

    private enum DecoratorType : byte { Before, After, Around }

    private readonly Component _component;
    private readonly DecoratorType[] _types;
    private readonly object[] _decorators;

    private ActionDecorator(Component component, DecoratorType[] types, object[] decorators)
    {
        _component = component;
        _types = types;
        _decorators = decorators;
    }

    /// <summary>
    /// Executes the decorated component with the given input.
    /// </summary>
    public void Execute(in TIn input)
    {
        if (_types.Length == 0)
        {
            _component(input);
            return;
        }
        ExecuteLayer(input, 0);
    }

    private void ExecuteLayer(TIn input, int index)
    {
        if (index >= _types.Length)
        {
            _component(input);
            return;
        }

        switch (_types[index])
        {
            case DecoratorType.Before:
                ExecuteBeforeLayer(input, index);
                break;
            case DecoratorType.After:
                ExecuteAfterLayer(input, index);
                break;
            case DecoratorType.Around:
                ExecuteAroundLayer(input, index);
                break;
        }
    }

    private void ExecuteBeforeLayer(TIn input, int index)
    {
        var transform = (BeforeTransform)_decorators[index];
        var transformedInput = transform(input);
        ExecuteLayer(transformedInput, index + 1);
    }

    private void ExecuteAfterLayer(TIn input, int index)
    {
        var action = (AfterAction)_decorators[index];
        ExecuteLayer(input, index + 1);
        action(input);
    }

    private void ExecuteAroundLayer(TIn input, int index)
    {
        var transform = (AroundTransform)_decorators[index];
        transform(input, Next);

        void Next(TIn inp) => ExecuteLayer(inp, index + 1);
    }

    /// <summary>
    /// Creates a new builder for constructing a decorated action.
    /// </summary>
    public static Builder Create(Component component) => new(component);

    /// <summary>
    /// Fluent builder for <see cref="ActionDecorator{TIn}"/>.
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
        /// Adds a decorator that transforms the input before it reaches the next layer.
        /// </summary>
        public Builder Before(BeforeTransform transform)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            _types.Add(DecoratorType.Before);
            _decorators.Add(transform);
            return this;
        }

        /// <summary>
        /// Adds a decorator that executes logic after the next layer completes.
        /// </summary>
        public Builder After(AfterAction action)
        {
            if (action is null) throw new ArgumentNullException(nameof(action));
            _types.Add(DecoratorType.After);
            _decorators.Add(action);
            return this;
        }

        /// <summary>
        /// Adds a decorator that wraps the entire execution with custom logic.
        /// </summary>
        public Builder Around(AroundTransform transform)
        {
            if (transform is null) throw new ArgumentNullException(nameof(transform));
            _types.Add(DecoratorType.Around);
            _decorators.Add(transform);
            return this;
        }

        /// <summary>
        /// Builds an immutable decorator with the registered decorators.
        /// </summary>
        public ActionDecorator<TIn> Build()
            => new(_component, _types.ToArray(), _decorators.ToArray());
    }
}
