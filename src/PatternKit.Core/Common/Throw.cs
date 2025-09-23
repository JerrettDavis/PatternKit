using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace PatternKit.Common;

/// <summary>
/// Provides centralized guard and throw helpers for common error scenarios in <c>PatternKit</c>.
/// </summary>
/// <remarks>
/// <para>
/// These helpers are intended for use in hot paths where throwing is exceptional but must
/// terminate execution predictably. 
/// </para>
/// <para>
/// Typical usage is within strategy engines or fluent builders when no handler, predicate,
/// or branch matches and no default was configured.
/// </para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// // Example usage in a strategy executor:
/// public TOut Execute(in TIn input)
/// {
///     foreach (var (pred, handler) in _pairs)
///         if (pred(in input))
///             return handler(in input);
///
///     // No predicate matched:
///     return Throw.NoStrategyMatched&lt;TOut&gt;();
/// }
/// </code>
/// </example>
public static class Throw
{
    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that no strategy branch matched
    /// and no default result was provided.
    /// </summary>
    /// <typeparam name="T">The expected return type of the calling method.</typeparam>
    /// <returns>
    /// This method never returns. The <typeparamref name="T"/> return type exists solely so it can
    /// be used in expression contexts requiring a value.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Always thrown with the message <c>"No strategy matched and no default provided."</c>
    /// </exception>
    /// <remarks>
    /// <para>
    /// Use this overload when the caller must syntactically return a <typeparamref name="T"/> value:
    /// </para>
    /// <code language="csharp">
    /// return Throw.NoStrategyMatched&lt;TOut&gt;();
    /// </code>
    /// </remarks>
    [DoesNotReturn]
    public static T NoStrategyMatched<T>() =>
        throw new InvalidOperationException("No strategy matched and no default provided.");

    /// <summary>
    /// Throws an <see cref="InvalidOperationException"/> indicating that no strategy branch matched
    /// and no default result was provided.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Always thrown with the message <c>"No strategy matched and no default provided."</c>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Use this overload when the method return type is <see langword="void"/> or when
    /// you do not need to return a value:
    /// </para>
    /// <code language="csharp">
    /// if (!_handlers.Any()) Throw.NoStrategyMatched();
    /// </code>
    /// </remarks>
    [DoesNotReturn]
    public static void NoStrategyMatched() =>
        throw new InvalidOperationException("No strategy matched and no default provided.");
    
    public static void ArgumentNullWhenNull([CallerMemberName] object? arg = null)
    {
        if (arg is null) throw new ArgumentNullException(nameof(arg));
    }
}