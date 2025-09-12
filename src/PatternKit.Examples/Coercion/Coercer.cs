using System.Globalization;
using System.Reflection;
using System.Text.Json;
using PatternKit.Behavioral.Strategy;
using PatternKit.Common;

namespace PatternKit.Examples.Coercion;

/// <summary>
/// Provides strategy-driven, allocation-light coercion from <see cref="object"/> to <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">
/// The target type to coerce values into. Common cases are primitives, nullable primitives,
/// <see cref="string"/>, and <see cref="string"/>[].
/// </typeparam>
/// <remarks>
/// <para>
/// <see cref="Coercer{T}"/> compiles a typed pipeline of <see cref="TryStrategy{TIn,TOut}.TryHandler"/> delegates
/// exactly once per closed generic type (<c>Coercer&lt;int&gt;</c>, <c>Coercer&lt;string&gt;</c>, etc.).
/// At runtime, <see cref="From(object?)"/> performs:
/// </para>
/// <list type="number">
///   <item><description>Fast path: direct cast when <paramref name="v"/> is already <typeparamref name="T"/>.</description></item>
///   <item><description>Strategy chain execution: first matching handler transforms <paramref name="v"/> to <typeparamref name="T"/>.</description></item>
///   <item><description>Default: returns <see langword="default"/> when no handler matches.</description></item>
/// </list>
/// <para>
/// The default pipeline includes handlers for:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="JsonElement"/> → <see cref="string"/> via <see cref="JsonElement.ToString"/>.</description></item>
///   <item><description><see cref="JsonElement"/> array → <see cref="string"/>[] (by enumerating elements).</description></item>
///   <item><description><see cref="JsonElement"/> number → <see cref="int"/>, <see cref="float"/>, <see cref="double"/>.</description></item>
///   <item><description><see cref="JsonElement"/> boolean → <see cref="bool"/>.</description></item>
///   <item>
///     <description>
///       Convertible fallback: if <paramref name="v"/> is <see cref="IConvertible"/> and the target underlying type is
///       primitive or <see cref="decimal"/>, uses <see cref="Convert.ChangeType(object?, System.Type, IFormatProvider?)"/> with
///       <see cref="CultureInfo.InvariantCulture"/>.
///     </description>
///   </item>
/// </list>
/// <para><b>Thread safety:</b> The compiled pipeline is immutable and static per closed generic type; <see cref="From(object?)"/> is thread-safe.</para>
/// <para><b>Performance:</b> No LINQ; handlers are traversed as a flat array. The first matching handler wins.</para>
/// </remarks>
/// <example>
/// <code language="csharp">
/// var i = Coercer&lt;int&gt;.From(JsonDocument.Parse("123").RootElement); // 123
/// var b = Coercer&lt;bool&gt;.From(JsonDocument.Parse("true").RootElement); // true
/// var s = Coercer&lt;string&gt;.From(JsonDocument.Parse("\"hello\"").RootElement); // "hello"
/// var arr = Coercer&lt;string[]&gt;.From(JsonDocument.Parse("[\"a\",\"b\"]").RootElement); // ["a","b"]
/// var n = Coercer&lt;int?&gt;.From("27"); // 27 via convertible fallback
/// </code>
/// </example>
public static class Coercer<T>
{
    /// <summary>
    /// The compiled sequence of handlers used to coerce values to <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// Built once per closed <see cref="Coercer{T}"/> type by <see cref="Build()"/>.
    /// </remarks>
    private static readonly TryStrategy<object, T>.TryHandler[] Strategies = Build();

    /// <summary>
    /// Attempts to coerce <paramref name="v"/> to <typeparamref name="T"/>.
    /// </summary>
    /// <param name="v">The input value to coerce. May be <see langword="null"/>.</param>
    /// <returns>
    /// The coerced value when successful; otherwise <see langword="default"/>.
    /// </returns>
    /// <remarks>
    /// <para>Order of operations:</para>
    /// <list type="number">
    ///   <item><description>If <paramref name="v"/> is <see langword="null"/>, returns <see langword="default"/>.</description></item>
    ///   <item><description>If <paramref name="v"/> is already <typeparamref name="T"/>, returns it unmodified.</description></item>
    ///   <item><description>Executes the strategy chain and returns the first successful result.</description></item>
    /// </list>
    /// </remarks>
    public static T? From(object? v) =>
        v switch
        {
            null => default,
            T t => t,
            _ => Strategies.FirstMatch(in v).OrDefault()
        };

    /// <summary>
    /// Compiles the strategy pipeline for the current closed <see cref="Coercer{T}"/>.
    /// </summary>
    /// <returns>
    /// An array of non-capturing <see cref="TryStrategy{TIn, TOut}.TryHandler"/> delegates, evaluated in order.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The pipeline is tailored based on <typeparamref name="T"/> to avoid unnecessary branching in the hot path.
    /// </para>
    /// </remarks>
    private static TryStrategy<object, T>.TryHandler[] Build()
    {
        var isString = typeof(T) == typeof(string);
        var isStringArray = typeof(T) == typeof(string[]);
        var isBoolLike = typeof(T) == typeof(bool) || typeof(T) == typeof(bool?);
        var isIntLike = typeof(T) == typeof(int) || typeof(T) == typeof(int?);
        var isFloatLike = typeof(T) == typeof(float) || typeof(T) == typeof(float?);
        var isDoubleLike = typeof(T) == typeof(double) || typeof(T) == typeof(double?);

        return TryStrategy<object, T>.Create()
            .Always(DirectCast)
            .When(() => isString)
                .Add(FromJsonString).Or
            .When(() => isStringArray)
                .Add(FromJsonStringArray)
                .And(FromSingleStringToArray).Or
            .When(() => isIntLike)
                .Add(FromJsonNumberInt).Or
            .When(() => isFloatLike)
                .Add(FromJsonNumberFloat).Or
            .When(() => isDoubleLike)
                .Add(FromJsonNumberDouble).Or
            .When(() => isBoolLike)
                .Add(FromJsonBool).Or
            .Finally(ConvertibleFallback)
            .Build()
            .GetHandlersArray();
    }

    /// <summary>
    /// Handler: direct pass-through when the input is already <typeparamref name="T"/>.
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting value when successful.</param>
    /// <returns><see langword="true"/> if <paramref name="v"/> is <typeparamref name="T"/>; otherwise <see langword="false"/>.</returns>
    private static bool DirectCast(in object v, out T? r)
    {
        if (v is T t)
        {
            r = t;
            return true;
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Handler: coerces <see cref="JsonElement"/> to <see cref="string"/> via <see cref="JsonElement.ToString"/>.
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting string as <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="v"/> is <see cref="JsonElement"/>; otherwise <see langword="false"/>.</returns>
    private static bool FromJsonString(in object v, out T? r)
    {
        if (v is JsonElement je)
        {
            r = (T?)(object?)je.ToString();
            return true;
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Handler: coerces <see cref="JsonElement"/> arrays to <see cref="string"/>[] by enumerating elements.
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting array as <typeparamref name="T"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="v"/> is a <see cref="JsonElement"/> with <see cref="JsonValueKind.Array"/>; otherwise <see langword="false"/>.
    /// </returns>
    private static bool FromJsonStringArray(in object v, out T? r)
    {
        if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var e in je.EnumerateArray()) list.Add(e.ToString());
            r = (T?)(object?)list.ToArray();
            return true;
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Handler: wraps a single <see cref="string"/> instance into a single-element <see cref="string"/>[].
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting array as <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="v"/> is <see cref="string"/>; otherwise <see langword="false"/>.</returns>
    private static bool FromSingleStringToArray(in object v, out T? r)
    {
        if (v is string s)
        {
            r = (T?)(object?)new[] { s };
            return true;
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Handler: coerces numeric <see cref="JsonElement"/> to <see cref="int"/>.
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting value as <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="v"/> is a numeric <see cref="JsonElement"/>; otherwise <see langword="false"/>.</returns>
    private static bool FromJsonNumberInt(in object v, out T? r)
    {
        if (v is JsonElement { ValueKind: JsonValueKind.Number } je)
        {
            r = (T?)(object?)je.GetInt32();
            return true;
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Handler: coerces numeric <see cref="JsonElement"/> to <see cref="float"/>.
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting value as <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="v"/> is a numeric <see cref="JsonElement"/>; otherwise <see langword="false"/>.</returns>
    private static bool FromJsonNumberFloat(in object v, out T? r)
    {
        if (v is JsonElement { ValueKind: JsonValueKind.Number } je)
        {
            r = (T?)(object?)je.GetSingle();
            return true;
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Handler: coerces numeric <see cref="JsonElement"/> to <see cref="double"/>.
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting value as <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="v"/> is a numeric <see cref="JsonElement"/>; otherwise <see langword="false"/>.</returns>
    private static bool FromJsonNumberDouble(in object v, out T? r)
    {
        if (v is JsonElement { ValueKind: JsonValueKind.Number } je)
        {
            r = (T?)(object?)je.GetDouble();
            return true;
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Handler: coerces boolean <see cref="JsonElement"/> to <see cref="bool"/>.
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting value as <typeparamref name="T"/>.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="v"/> is a boolean <see cref="JsonElement"/>; otherwise <see langword="false"/>.
    /// </returns>
    private static bool FromJsonBool(in object v, out T? r)
    {
        if (v is JsonElement { ValueKind: JsonValueKind.True or JsonValueKind.False } je)
        {
            r = (T?)(object?)je.GetBoolean();
            return true;
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Handler: as a last resort, uses <see cref="Convert.ChangeType(object?, System.Type, IFormatProvider?)"/> with invariant culture
    /// when <paramref name="v"/> is <see cref="IConvertible"/> and <typeparamref name="T"/> (or its underlying type) is primitive or <see cref="decimal"/>.
    /// </summary>
    /// <param name="v">The input value.</param>
    /// <param name="r">The resulting value as <typeparamref name="T"/> when conversion succeeds.</param>
    /// <returns>
    /// <see langword="true"/> if conversion succeeds; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Exceptions during conversion are swallowed; the handler simply fails and allows subsequent handlers (if any).
    /// </remarks>
    private static bool ConvertibleFallback(in object v, out T? r)
    {
        try
        {
            var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (v is IConvertible && (target.IsPrimitive || target == typeof(decimal)))
            {
                r = (T?)Convert.ChangeType(v, target, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
            /* swallow */
        }

        r = default;
        return false;
    }

    /// <summary>
    /// Convenience entry point that forwards to <see cref="From(object?)"/>.
    /// </summary>
    /// <typeparam name="TType">The target type to coerce into.</typeparam>
    /// <param name="v">The input value.</param>
    /// <returns>The coerced value when successful; otherwise <see langword="default"/>.</returns>
    /// <seealso cref="From(object?)"/>
    public static TType? Coerce<TType>(object? v) => Coercer<TType>.From(v);
}

/// <summary>
/// Extension methods for <see cref="Coercer{T}"/> and strategy inspection.
/// </summary>
public static class CoercerExtensions
{
    /// <summary>
    /// Coerces the current value to <typeparamref name="T"/> using <see cref="Coercer{T}.From(object?)"/>.
    /// </summary>
    /// <typeparam name="T">The target type to coerce into.</typeparam>
    /// <param name="v">The input value to coerce.</param>
    /// <returns>The coerced value when successful; otherwise <see langword="default"/>.</returns>
    /// <example>
    /// <code language="csharp">
    /// object any = "42";
    /// int? value = any.Coerce&lt;int&gt;(); // 42
    /// </code>
    /// </example>
    public static T? Coerce<T>(this object? v) => Coercer<T>.From(v);

    /// <summary>
    /// Extracts the compiled handler array from a <see cref="TryStrategy{TIn, TOut}"/> instance.
    /// </summary>
    /// <typeparam name="T">The output type of the strategy.</typeparam>
    /// <param name="s">The strategy instance.</param>
    /// <returns>The compiled <see cref="TryStrategy{TIn, TOut}.TryHandler"/> array, or an empty array when unavailable.</returns>
    /// <remarks>
    /// This uses reflection to access the strategy's internal backing field. Intended for diagnostics and tests.
    /// </remarks>
    public static TryStrategy<object, T>.TryHandler[] GetHandlersArray<T>(
        this TryStrategy<object, T> s)
        => typeof(TryStrategy<object, T>)
            .GetField("_handlers", BindingFlags.NonPublic |
                                   BindingFlags.Instance)!
            .GetValue(s) as TryStrategy<object, T>.TryHandler[] ?? [];
}

