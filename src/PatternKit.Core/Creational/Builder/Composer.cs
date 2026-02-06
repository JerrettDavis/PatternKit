namespace PatternKit.Creational.Builder;

/// <summary>
/// A fluent, explicit composer that accumulates state in <typeparamref name="TState"/> (often a small struct)
/// via pure transformations and finally projects it to <typeparamref name="TOut"/>.
/// </summary>
/// <typeparam name="TState">The internal builder state (prefer a small struct for performance).</typeparam>
/// <typeparam name="TOut">The final output type.</typeparam>
/// <remarks>
/// Use when your output is immutable: build up state with <see cref="With(System.Func{TState, TState})"/>
/// and finalize with <see cref="Build(System.Func{TState, TOut})"/>.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public readonly record struct PersonState(string? Name, int Age);
/// public sealed record PersonDto(string Name, int Age);
///
/// var dto = Composer&lt;PersonState, PersonDto&gt;
///     .New(static () =&gt; default)
///     .With(static s =&gt; s with { Name = "Ada" })
///     .With(static s =&gt; s with { Age = 30 })
///     .Require(static s =&gt; string.IsNullOrWhiteSpace(s.Name) ? "Name is required." : null)
///     .Build(static s =&gt; new PersonDto(s.Name!, s.Age));
/// </code>
/// </example>
public sealed class Composer<TState, TOut>
{
    private readonly Func<TState> _seed;
    private Func<TState, TState>? _pipeline;
    private Func<TState, string?>? _validators;

    private Composer(Func<TState> seed) => _seed = seed;

    /// <summary>
    /// Creates a new <see cref="Composer{TState, TOut}"/> with the specified seed factory.
    /// </summary>
    /// <param name="seed">A factory for the initial state (e.g., <c>static () =&gt; default</c>).</param>
    /// <returns>A new <see cref="Composer{TState, TOut}"/>.</returns>
    public static Composer<TState, TOut> New(Func<TState> seed) => new(seed);

    /// <summary>
    /// Adds a pure state transformation; prefer <c>static</c> lambdas to avoid captures.
    /// </summary>
    /// <param name="transform">A function that transforms the current state.</param>
    /// <returns>The current <see cref="Composer{TState, TOut}"/> for fluent chaining.</returns>
    /// <remarks>
    /// Transformations are composed in the order they are added and applied once during <see cref="Build(System.Func{TState, TOut})"/>.
    /// </remarks>
    public Composer<TState, TOut> With(Func<TState, TState> transform)
    {
        _pipeline = _pipeline is null ? transform : Chain(_pipeline, transform);
        return this;

        static Func<TState, TState> Chain(Func<TState, TState> a, Func<TState, TState> b)
            => s => b(a(s));
    }

    /// <summary>
    /// Adds a validation rule; return <see langword="null"/> for success or a non-empty error message for failure.
    /// </summary>
    /// <param name="validate">A function that validates the composed state.</param>
    /// <returns>The current <see cref="Composer{TState, TOut}"/> for fluent chaining.</returns>
    /// <remarks>
    /// Validations are evaluated once during <see cref="Build(System.Func{TState, TOut})"/>.
    /// The first failure throws an <see cref="InvalidOperationException"/>.
    /// </remarks>
    public Composer<TState, TOut> Require(Func<TState, string?> validate)
    {
        _validators = _validators is null ? validate : Chain(_validators, validate);
        return this;

        static Func<TState, string?> Chain(Func<TState, string?> a, Func<TState, string?> b)
            => s => a(s) ?? b(s);
    }

    /// <summary>
    /// Builds the final output using <paramref name="project"/>, after applying transformations and validations.
    /// </summary>
    /// <param name="project">A projection that converts the final state into the output value.</param>
    /// <returns>The built <typeparamref name="TOut"/> value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if any validator returns a non-null, non-empty message.</exception>
    /// <remarks>
    /// <para>Execution order:</para>
    /// <list type="number">
    ///   <item><description>Compute the final state by applying the composed pipeline to the seed.</description></item>
    ///   <item><description>Evaluate all validations; throw on first failure.</description></item>
    ///   <item><description>Invoke the projection to obtain the output.</description></item>
    /// </list>
    /// </remarks>
    public TOut Build(Func<TState, TOut> project)
    {
        var state = _pipeline is null ? _seed() : _pipeline(_seed());

        var error = _validators?.Invoke(state);
        return error is { Length: > 0 }
            ? throw new InvalidOperationException(error)
            : project(state);
    }
}