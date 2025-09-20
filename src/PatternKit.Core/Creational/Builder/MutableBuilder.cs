using System.Runtime.CompilerServices;

namespace PatternKit.Creational.Builder;

/// <summary>
/// A minimal, explicit, fluent builder that creates a new <typeparamref name="T"/> using a factory,
/// applies zero or more mutations, validates, and returns the instance.
/// </summary>
/// <typeparam name="T">The object type being built.</typeparam>
/// <remarks>
/// <para>
/// <b>Design goals:</b> explicit, allocation-light, reflection-free. Prefer <c>static</c> lambdas for
/// <see cref="With(System.Action{T})"/> to avoid closures. Validations are evaluated in build order,
/// and the first failure message throws an <see cref="InvalidOperationException"/>.
/// </para>
/// <para><b>Thread-safety:</b> instances of <see cref="MutableBuilder{T}"/> are not thread-safe.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// public sealed class Person
/// {
///     public string? Name { get; set; }
///     public int Age { get; set; }
/// }
///
/// var person = MutableBuilder&lt;Person&gt;.New(static () =&gt; new Person())
///     .With(static p =&gt; p.Name = "Ada")
///     .With(static p =&gt; p.Age = 30)
///     .Require(x =&gt; string.IsNullOrWhiteSpace(x.Name) ? "Name is required." : null)
///     .Build();
/// </code>
/// </example>
public sealed class MutableBuilder<T>
{
    private readonly Func<T> _factory;

    // store validators without closures
    private readonly List<IValidator<T>> _validators = new(4);
    private Action<T>? _mutations;

    /// <summary>
    /// Initializes a new builder with the provided <paramref name="factory"/>.
    /// </summary>
    /// <param name="factory">A function that creates a fresh instance of <typeparamref name="T"/>.</param>
    public MutableBuilder(Func<T> factory) => _factory = factory;

    /// <summary>
    /// Creates a new <see cref="MutableBuilder{T}"/> that uses the specified <paramref name="factory"/> to instantiate objects.
    /// </summary>
    /// <param name="factory">A function that creates a fresh instance of <typeparamref name="T"/>.</param>
    /// <returns>A new <see cref="MutableBuilder{T}"/> instance.</returns>
    /// <example>
    /// <code language="csharp">
    /// var builder = MutableBuilder&lt;MyOptions&gt;.New(static () =&gt; new MyOptions());
    /// </code>
    /// </example>
    public static MutableBuilder<T> New(Func<T> factory) => new(factory);

    /// <summary>
    /// Appends a mutation that will be applied to the instance before validation.
    /// </summary>
    /// <param name="mutate">A side-effect action that mutates the instance.</param>
    /// <returns>The current <see cref="MutableBuilder{T}"/> for fluent chaining.</returns>
    /// <remarks>
    /// Prefer <c>static</c> lambdas (e.g., <c>static x =&gt; x.Prop = ...</c>) to avoid capturing outer state.
    /// Multiple calls are combined and invoked in the order they were added.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// builder.With(static x =&gt; x.Timeout = TimeSpan.FromSeconds(5))
    ///        .With(static x =&gt; x.Enabled = true);
    /// </code>
    /// </example>
    public MutableBuilder<T> With(Action<T> mutate)
    {
        _mutations = _mutations is null ? mutate : (Action<T>)Delegate.Combine(_mutations, mutate);
        return this;
    }

    /// <summary>
    /// Adds a validation rule; return <see langword="null"/> when valid or a non-empty error message when invalid.
    /// </summary>
    /// <param name="validate">A function that validates the built instance.</param>
    /// <returns>The current <see cref="MutableBuilder{T}"/> for fluent chaining.</returns>
    /// <remarks>
    /// Validations are evaluated after all mutations during <see cref="Build"/>. The first non-null/ non-empty
    /// message triggers an <see cref="InvalidOperationException"/>.
    /// </remarks>
    public MutableBuilder<T> Require(Func<T, string?> validate)
    {
        _validators.Add(new FuncValidator(validate));
        return this;
    }

    /// <summary>
    /// Adds a stateful validation rule without capturing. Passes <paramref name="state"/> to the validator.
    /// </summary>
    /// <typeparam name="TState">The type of the additional state required for validation.</typeparam>
    /// <param name="state">Arbitrary state passed to <paramref name="validate"/>.</param>
    /// <param name="validate">
    /// A function that validates using the built value and <paramref name="state"/>; return <see langword="null"/>
    /// when valid or a non-empty error message when invalid.
    /// </param>
    /// <returns>The current <see cref="MutableBuilder{T}"/> for fluent chaining.</returns>
    /// <remarks>
    /// This overload enables use of <c>static</c> lambdas to avoid closures:
    /// <code language="csharp">
    /// builder.Require((min, max), static (x, s) =&gt; x.Value is &gt;= s.min and &lt;= s.max ? null : "Out of range");
    /// </code>
    /// </remarks>
    public MutableBuilder<T> Require<TState>(TState state, Func<T, TState, string?> validate)
    {
        _validators.Add(new StatefulValidator<TState>(state, validate));
        return this;
    }

    /// <summary>
    /// Builds the object, applying mutations and then validations.
    /// </summary>
    /// <returns>The fully built <typeparamref name="T"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when any validation returns a non-null, non-empty error message.
    /// </exception>
    /// <remarks>
    /// <para>Execution order:</para>
    /// <list type="number">
    ///   <item><description>Invoke <see cref="System.Func{TResult}"/> factory to create the object.</description></item>
    ///   <item><description>Apply all registered mutations in order.</description></item>
    ///   <item><description>Evaluate validations in order; throw on first failure.</description></item>
    /// </list>
    /// </remarks>
    public T Build()
    {
        var obj = _factory();
        _mutations?.Invoke(obj);

        foreach (var v in _validators)
        {
            var err = v.Validate(obj);
            if (!string.IsNullOrEmpty(err))
                throw new InvalidOperationException(err);
        }

        return obj;
    }

    // --- validator plumbing (internal) ---
    /// <summary>
    /// (Internal) Abstraction for a validation rule. Not intended for public consumption.
    /// </summary>
    private interface IValidator<in TValue>
    {
        /// <summary>Validates <paramref name="value"/>; return <see langword="null"/> if valid.</summary>
        string? Validate(TValue value);
    }

    private sealed class FuncValidator(Func<T, string?> f) : IValidator<T>
    {
        public string? Validate(T value) => f(value);
    }

    private sealed class StatefulValidator<TState>(TState state, Func<T, TState, string?> f) : IValidator<T>
    {
        public string? Validate(T value) => f(value, state);
    }
}

/// <summary>
/// Helper extensions for common builder/validation patterns.
/// </summary>
/// <remarks>
/// These helpers prefer <c>static</c> lambdas and use the stateful validation overloads to avoid closures.
/// </remarks>
public static class BuilderExtensions
{
    /// <summary>
    /// Adds a non-empty string requirement for a selected property.
    /// </summary>
    /// <typeparam name="T">The builder's target type.</typeparam>
    /// <param name="b">The builder.</param>
    /// <param name="selector">Selects the string to validate.</param>
    /// <param name="paramName">The parameter or property name to report in the error message.</param>
    /// <returns>The same <see cref="MutableBuilder{T}"/> for fluent chaining.</returns>
    /// <example>
    /// <code language="csharp">
    /// var result = MutableBuilder&lt;Person&gt;.New(static () =&gt; new Person())
    ///     .With(static p =&gt; p.Name = "")
    ///     .RequireNotEmpty(static p =&gt; p.Name, nameof(Person.Name))
    ///     .Build(); // throws InvalidOperationException
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MutableBuilder<T> RequireNotEmpty<T>(
        this MutableBuilder<T> b,
        Func<T, string?> selector,
        string paramName)
        => b.Require((selector, paramName), static (x, s) =>
        {
            var v = s.selector(x);
            return string.IsNullOrWhiteSpace(v) ? $"{s.paramName} must be non-empty." : null;
        });

    /// <summary>
    /// Adds an inclusive integer range requirement for a selected property.
    /// </summary>
    /// <typeparam name="T">The builder's target type.</typeparam>
    /// <param name="b">The builder.</param>
    /// <param name="selector">Selects the integer value to validate.</param>
    /// <param name="minInclusive">The minimum allowed value (inclusive).</param>
    /// <param name="maxInclusive">The maximum allowed value (inclusive).</param>
    /// <param name="paramName">The parameter or property name to report in the error message.</param>
    /// <returns>The same <see cref="MutableBuilder{T}"/> for fluent chaining.</returns>
    /// <example>
    /// <code language="csharp">
    /// var result = MutableBuilder&lt;Person&gt;.New(static () =&gt; new Person())
    ///     .With(static p =&gt; p.Age = -1)
    ///     .RequireRange(static p =&gt; p.Age, 0, 130, nameof(Person.Age))
    ///     .Build(); // throws InvalidOperationException
    /// </code>
    /// </example>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MutableBuilder<T> RequireRange<T>(
        this MutableBuilder<T> b,
        Func<T, int> selector,
        int minInclusive,
        int maxInclusive,
        string paramName)
        => b.Require((selector, minInclusive, maxInclusive, paramName), static (x, s) =>
        {
            var v = s.selector(x);
            return (v < s.minInclusive || v > s.maxInclusive)
                ? $"{s.paramName} must be within [{s.minInclusive}, {s.maxInclusive}] but was {v}."
                : null;
        });
}