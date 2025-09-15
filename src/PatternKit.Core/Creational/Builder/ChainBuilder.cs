namespace PatternKit.Creational.Builder;

/// <summary>
/// Lightweight, allocation-friendly builder that collects items in registration order and
/// projects them into a concrete product.
/// </summary>
/// <typeparam name="T">The element type stored by the builder.</typeparam>
/// <remarks>
/// <para>
/// <b>Design goals:</b> minimal overhead, predictable iteration order, and a small fluent API
/// for collecting items to later transform via <see cref="Build{TProduct}"/>.
/// </para>
/// <para>
/// Instances are mutable until consumed by callers; builders are not thread-safe.
/// </para>
/// </remarks>
public sealed class ChainBuilder<T>
{
    private readonly List<T> _items = new(8);

    private ChainBuilder()
    {
    }

    /// <summary>
    /// Creates a new <see cref="ChainBuilder{T}"/>.
    /// </summary>
    /// <returns>A fresh <see cref="ChainBuilder{T}"/> instance.</returns>
    public static ChainBuilder<T> Create() => new();

    /// <summary>
    /// Appends <paramref name="item"/> to the builder in registration order.
    /// </summary>
    /// <param name="item">The item to append.</param>
    /// <returns>The same <see cref="ChainBuilder{T}"/> instance for fluent chaining.</returns>
    public ChainBuilder<T> Add(T item)
    {
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Conditionally appends <paramref name="item"/> when <paramref name="condition"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="condition">When <see langword="true"/> the <paramref name="item"/> is added.</param>
    /// <param name="item">The item to append when the condition holds.</param>
    /// <returns>The same <see cref="ChainBuilder{T}"/> instance for fluent chaining.</returns>
    public ChainBuilder<T> AddIf(bool condition, T item)
    {
        if (condition) _items.Add(item);
        return this;
    }

    /// <summary>
    /// Projects the collected items into a product using the provided <paramref name="projector"/>.
    /// </summary>
    /// <typeparam name="TProduct">The resulting product type.</typeparam>
    /// <param name="projector">
    /// Function that receives a snapshot array of the collected items and produces the desired product.
    /// The builder provides a defensive copy via <c>ToArray()</c> to avoid exposing internal storage.
    /// </param>
    /// <returns>The projected product returned by <paramref name="projector"/>.</returns>
    /// <example>
    /// <code language="csharp">
    /// var csv = ChainBuilder{string}.Create()
    ///     .Add("A")
    ///     .Add("B")
    ///     .Build(arr =&gt; string.Join(",", arr));
    /// </code>
    /// </example>
    public TProduct Build<TProduct>(Func<T[], TProduct> projector)
        => projector(_items.ToArray());
}