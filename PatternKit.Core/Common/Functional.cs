using PatternKit.Behavioral.Strategy;

namespace PatternKit.Common;

/// <summary>
/// A lightweight optional value type with no allocations, suitable for hot paths.
/// </summary>
/// <typeparam name="T">The wrapped value type.</typeparam>
/// <remarks>
/// <para>
/// <see cref="Option{T}"/> is a simple triad of operations:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="Some(T)"/> to create a present value.</description></item>
///   <item><description><see cref="None"/> to represent absence.</description></item>
///   <item><description><see cref="OrDefault(T?)"/> / <see cref="OrThrow(string?)"/> / <see cref="Map{TOut}(System.Func{T?, TOut?})"/> for use.</description></item>
/// </list>
/// <para>It is immutable and does not box primitives.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var maybe = Option&lt;int&gt;.Some(5);
/// var value = maybe.OrDefault(); // 5
/// var mapped = maybe.Map(x => x * 2).OrDefault(); // 10
/// </code>
/// </example>
public readonly struct Option<T>
{
    private readonly T? _value;

    /// <summary>
    /// Indicates whether the option contains a value.
    /// </summary>
    public bool HasValue { get; }

    /// <summary>
    /// Returns the contained value when present; otherwise <see langword="default"/>.
    /// </summary>
    public T? ValueOrDefault => _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="Option{T}"/> struct.
    /// </summary>
    /// <param name="value">The wrapped value, or <see langword="default"/> when absent.</param>
    /// <param name="hasValue"><see langword="true"/> when a value is present; otherwise <see langword="false"/>.</param>
    private Option(T? value, bool hasValue)
    {
        _value = value;
        HasValue = hasValue;
    }

    /// <summary>
    /// Creates an <see cref="Option{T}"/> that contains a value.
    /// </summary>
    /// <param name="value">The value to wrap. May be <see langword="null"/> for reference types.</param>
    public static Option<T> Some(T? value) => new(value, true);

    /// <summary>
    /// Creates an empty <see cref="Option{T}"/>.
    /// </summary>
    public static Option<T> None() => new(default, false);

    /// <summary>
    /// Returns the contained value when present; otherwise returns <paramref name="fallback"/>.
    /// </summary>
    /// <param name="fallback">The fallback value to return when the option is empty.</param>
    public T? OrDefault(T? fallback = default) => HasValue ? _value : fallback;

    /// <summary>
    /// Returns the contained value when present; otherwise throws <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <param name="message">An optional error message for the thrown exception.</param>
    /// <exception cref="InvalidOperationException">Thrown when the option is empty.</exception>
    public T OrThrow(string? message = null)
        => HasValue ? _value! : throw new InvalidOperationException(message ?? "No value.");

    /// <summary>
    /// Maps the contained value to a new <see cref="Option{TOut}"/> using <paramref name="f"/>.
    /// </summary>
    /// <typeparam name="TOut">The result value type.</typeparam>
    /// <param name="f">The mapping function applied when a value is present.</param>
    /// <returns>
    /// <see cref="Option{TOut}.Some(TOut)"/> containing the mapped value, or <see cref="Option{TOut}.None"/> when empty.
    /// </returns>
    public Option<TOut> Map<TOut>(Func<T?, TOut?> f)
        => HasValue ? Option<TOut>.Some(f(_value)) : Option<TOut>.None();
}

/// <summary>
/// Extensions that add functional, chainable operations over compiled try-handlers.
/// </summary>
public static class TryHandlerExtensions
{
    /// <summary>
    /// Attempts to obtain the first successful result from a compiled handler array.
    /// </summary>
    /// <typeparam name="TIn">The handler input type.</typeparam>
    /// <typeparam name="TOut">The handler output type.</typeparam>
    /// <param name="handlers">The compiled handlers to evaluate.</param>
    /// <param name="input">The input value.</param>
    /// <param name="result">The first successful result, when found.</param>
    /// <returns><see langword="true"/> if any handler succeeds; otherwise <see langword="false"/>.</returns>
    public static bool TryGetResult<TIn, TOut>(
        this TryStrategy<TIn, TOut>.TryHandler[] handlers,
        in TIn input,
        out TOut? result)
    {
        foreach (var h in handlers)
            if (h(in input, out result))
                return true;

        result = default;
        return false;
    }

    /// <summary>
    /// Returns the first successful result as an <see cref="Option{T}"/> for fluent chaining.
    /// </summary>
    /// <typeparam name="TIn">The handler input type.</typeparam>
    /// <typeparam name="TOut">The handler output type.</typeparam>
    /// <param name="handlers">The compiled handlers to evaluate.</param>
    /// <param name="input">The input value.</param>
    /// <returns>
    /// <see cref="Option{T}.Some(T)"/> with the first successful result, or <see cref="Option{T}.None"/> when none succeed.
    /// </returns>
    /// <example>
    /// <code language="csharp">
    /// var value = handlers.FirstMatch(in input).OrDefault();
    /// </code>
    /// </example>
    public static Option<TOut> FirstMatch<TIn, TOut>(
        this TryStrategy<TIn, TOut>.TryHandler[] handlers,
        in TIn input)
    {
        foreach (var h in handlers)
            if (h(in input, out var r))
                return Option<TOut>.Some(r);
        return Option<TOut>.None();
    }
}