namespace PatternKit.Structural.Composite;

/// <summary>
/// Fluent, allocation-light Composite that treats individual leaves and compositions uniformly.
/// Build a node as either a <b>Leaf</b> (single operation) or a <b>Composite</b> (seed + combine over children).
/// </summary>
/// <typeparam name="TIn">Input type passed by <c>in</c>.</typeparam>
/// <typeparam name="TOut">Result type produced by execution.</typeparam>
/// <remarks>
/// <para>
/// <b>Mental model</b>: a <i>leaf</i> returns <c>LeafOp(input)</c>. A <i>composite</i> evaluates
/// <c>acc = Seed(input)</c>, then for each child in registration order: <c>acc = Combine(input, acc, child.Execute(input))</c>.
/// The final <c>acc</c> is returned. This is an ordered fold over child results.
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/> the produced tree is immutable and safe for concurrent reuse.
/// Builders are mutable and not thread-safe.
/// </para>
/// </remarks>
public sealed class Composite<TIn, TOut>
{
    /// <summary>
    /// Delegate for a <b>leaf</b> operation that produces a typed response from an input.
    /// </summary>
    /// <param name="input">Input value (readonly via <c>in</c>).</param>
    /// <returns>The computed result for this leaf.</returns>
    public delegate TOut LeafOp(in TIn input);

    /// <summary>
    /// Delegate for a <b>composite</b> seed function that initializes the accumulator before folding child results.
    /// </summary>
    /// <param name="input">Input value (readonly via <c>in</c>).</param>
    /// <returns>The initial accumulator value used by <see cref="Combine"/>.</returns>
    public delegate TOut Seed(in TIn input);

    /// <summary>
    /// Delegate for a <b>composite</b> combiner that folds one <paramref name="childResult"/> into <paramref name="acc"/>.
    /// Called once per child in registration order.
    /// </summary>
    /// <param name="input">Input value (readonly via <c>in</c>).</param>
    /// <param name="acc">The current accumulator value.</param>
    /// <param name="childResult">The result produced by the child node.</param>
    /// <returns>The next accumulator value.</returns>
    public delegate TOut Combine(in TIn input, TOut acc, TOut childResult);

    private readonly bool _isLeaf;
    private readonly LeafOp? _leaf;
    private readonly Seed? _seed;
    private readonly Combine? _combine;
    private readonly Composite<TIn, TOut>[] _children;

    private Composite(bool isLeaf, LeafOp? leaf, Seed? seed, Combine? combine, Composite<TIn, TOut>[] children)
    {
        _isLeaf = isLeaf;
        _leaf = leaf;
        _seed = seed;
        _combine = combine;
        _children = children;
    }

    /// <summary>Execute the leaf/composite operation.</summary>
    /// <param name="input">Input value (readonly via <c>in</c>).</param>
    /// <returns>
    /// For a leaf, the result of <see cref="LeafOp"/>. For a composite, the final accumulator after running
    /// <see cref="Seed"/> and folding each child result via <see cref="Combine"/>.
    /// </returns>
    /// <remarks>
    /// This method does not perform defensive checks beyond those guaranteed by <see cref="Builder.Build"/>; any
    /// exceptions will originate from user-provided delegates.
    /// </remarks>
    public TOut Execute(in TIn input)
    {
        if (_isLeaf)
        {
            return _leaf!(in input);
        }
        var acc = _seed!(in input);
        foreach (var child in _children)
        {
            var childResult = child.Execute(in input);
            acc = _combine!(in input, acc, childResult);
        }
        return acc;
    }

    /// <summary>Create a builder for a <b>Leaf</b> node with the given operation.</summary>
    /// <param name="op">The leaf operation to execute at runtime.</param>
    /// <returns>A <see cref="Builder"/> configured as a leaf.</returns>
    /// <remarks>
    /// The provided delegate is captured by the builder; pass a static lambda or method group to avoid allocations.
    /// If a <c>null</c> operation is provided, <see cref="Builder.Build"/> will throw <see cref="InvalidOperationException"/>.
    /// </remarks>
    public static Builder Leaf(LeafOp op) => new(leaf: op);

    /// <summary>Create a builder for a <b>Composite</b> node with the given seed and combiner.</summary>
    /// <param name="seed">Initializer for the accumulator prior to folding child results.</param>
    /// <param name="combine">Combiner that folds each child result into the accumulator.</param>
    /// <returns>A <see cref="Builder"/> configured as a composite node.</returns>
    /// <remarks>
    /// If <paramref name="seed"/> or <paramref name="combine"/> is <c>null</c>, <see cref="Builder.Build"/> will throw
    /// <see cref="InvalidOperationException"/>.
    /// </remarks>
    public static Builder Node(Seed seed, Combine combine) => new(seed, combine);

    /// <summary>Fluent builder for <see cref="Composite{TIn, TOut}"/> nodes (leaf or composite).</summary>
    /// <remarks>
    /// Builders are mutable and not thread-safe. Each call to <see cref="Build"/> snapshots the current shape into an
    /// immutable tree. Adding children to a leaf is a no-op to preserve uniform treatment.
    /// </remarks>
    public sealed class Builder
    {
        private readonly bool _isLeaf;
        private readonly LeafOp? _leaf;
        private readonly Seed? _seed;
        private readonly Combine? _combine;
        private readonly List<Builder> _children = new(4);

        internal Builder(LeafOp leaf)
        {
            _isLeaf = true; _leaf = leaf;
        }

        internal Builder(Seed seed, Combine combine)
        {
            _isLeaf = false; _seed = seed; _combine = combine;
        }

        /// <summary>
        /// Add a child node (ignored for leaf nodes).
        /// </summary>
        /// <param name="child">The child builder to append; order is preserved.</param>
        /// <returns>The same builder instance for chaining.</returns>
        /// <remarks>If this builder represents a leaf, this call is a no-op.</remarks>
        public Builder AddChild(Builder child)
        {
            if (!_isLeaf) _children.Add(child);
            return this;
        }

        /// <summary>
        /// Add multiple child nodes (ignored for leaf nodes).
        /// </summary>
        /// <param name="children">Child builders to append; registration order is preserved.</param>
        /// <returns>The same builder instance for chaining.</returns>
        /// <remarks>If this builder represents a leaf, this call is a no-op.</remarks>
        public Builder AddChildren(params Builder[]? children)
        {
            if (!_isLeaf && children is not null && children.Length > 0)
                _children.AddRange(children);
            return this;
        }

        /// <summary>
        /// Build an immutable composite tree.
        /// </summary>
        /// <returns>A <see cref="Composite{TIn, TOut}"/> that can be safely reused across threads.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown for a leaf when no <see cref="LeafOp"/> was configured, or for a composite when either
        /// <see cref="Seed"/> or <see cref="Combine"/> was not configured.
        /// </exception>
        public Composite<TIn, TOut> Build()
        {
            if (_isLeaf)
            {
                return _leaf is null
                    ? throw new InvalidOperationException("Leaf operation is required.")
                    : new Composite<TIn, TOut>(true, _leaf, null, null, []);
            }

            if (_seed is null || _combine is null)
                throw new InvalidOperationException("Composite node requires Seed and Combine.");

            var children = new Composite<TIn, TOut>[_children.Count];
            for (var i = 0; i < _children.Count; i++)
                children[i] = _children[i].Build();

            return new Composite<TIn, TOut>(false, null, _seed, _combine, children);
        }
    }
}
