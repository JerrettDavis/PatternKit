namespace PatternKit.Structural.Composite;

/// <summary>
/// Async Composite that treats individual leaves and compositions uniformly with async operations.
/// Build a node as either a <b>Leaf</b> (single async operation) or a <b>Composite</b> (seed + async fold over children).
/// </summary>
/// <typeparam name="TIn">Input type.</typeparam>
/// <typeparam name="TOut">Result type produced by execution.</typeparam>
/// <remarks>
/// <para>
/// This is the async counterpart to <see cref="Composite{TIn,TOut}"/>.
/// Use when leaf or composite operations need to perform async work.
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/> the produced tree is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var tree = AsyncComposite&lt;string, decimal&gt;.Node(
///         seed: async (_, ct) => 0m,
///         combine: async (_, acc, child, ct) => acc + child)
///     .AddChild(AsyncComposite&lt;string, decimal&gt;.Leaf(
///         async (productId, ct) => await GetPriceAsync(productId, ct)))
///     .AddChild(AsyncComposite&lt;string, decimal&gt;.Leaf(
///         async (productId, ct) => await GetTaxAsync(productId, ct)))
///     .Build();
///
/// var total = await tree.ExecuteAsync("SKU-123");
/// </code>
/// </example>
public sealed class AsyncComposite<TIn, TOut>
{
    /// <summary>
    /// Async delegate for a <b>leaf</b> operation that produces a typed response.
    /// </summary>
    public delegate ValueTask<TOut> LeafOp(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for a <b>composite</b> seed function that initializes the accumulator.
    /// </summary>
    public delegate ValueTask<TOut> Seed(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for a <b>composite</b> combiner that folds a child result into the accumulator.
    /// </summary>
    public delegate ValueTask<TOut> Combine(TIn input, TOut acc, TOut childResult, CancellationToken ct);

    private readonly bool _isLeaf;
    private readonly LeafOp? _leaf;
    private readonly Seed? _seed;
    private readonly Combine? _combine;
    private readonly AsyncComposite<TIn, TOut>[] _children;

    private AsyncComposite(bool isLeaf, LeafOp? leaf, Seed? seed, Combine? combine, AsyncComposite<TIn, TOut>[] children)
    {
        _isLeaf = isLeaf;
        _leaf = leaf;
        _seed = seed;
        _combine = combine;
        _children = children;
    }

    /// <summary>Execute the leaf/composite operation asynchronously.</summary>
    public async ValueTask<TOut> ExecuteAsync(TIn input, CancellationToken ct = default)
    {
        if (_isLeaf)
            return await _leaf!(input, ct).ConfigureAwait(false);

        var acc = await _seed!(input, ct).ConfigureAwait(false);
        foreach (var child in _children)
        {
            var childResult = await child.ExecuteAsync(input, ct).ConfigureAwait(false);
            acc = await _combine!(input, acc, childResult, ct).ConfigureAwait(false);
        }
        return acc;
    }

    /// <summary>Create a builder for a <b>Leaf</b> node with the given async operation.</summary>
    public static Builder Leaf(LeafOp op) => new(leaf: op);

    /// <summary>Create a builder for a <b>Leaf</b> node with a sync operation.</summary>
    public static Builder Leaf(Func<TIn, TOut> op) =>
        new(leaf: (input, _) => new ValueTask<TOut>(op(input)));

    /// <summary>Create a builder for a <b>Composite</b> node with async seed and combiner.</summary>
    public static Builder Node(Seed seed, Combine combine) => new(seed, combine);

    /// <summary>Create a builder for a <b>Composite</b> node with sync seed and combiner.</summary>
    public static Builder Node(Func<TIn, TOut> seed, Func<TIn, TOut, TOut, TOut> combine) =>
        new(
            (input, _) => new ValueTask<TOut>(seed(input)),
            (input, acc, child, _) => new ValueTask<TOut>(combine(input, acc, child)));

    /// <summary>Fluent builder for <see cref="AsyncComposite{TIn, TOut}"/> nodes.</summary>
    public sealed class Builder
    {
        private readonly bool _isLeaf;
        private readonly LeafOp? _leaf;
        private readonly Seed? _seed;
        private readonly Combine? _combine;
        private readonly List<Builder> _children = new(4);

        internal Builder(LeafOp leaf)
        {
            _isLeaf = true;
            _leaf = leaf;
        }

        internal Builder(Seed seed, Combine combine)
        {
            _isLeaf = false;
            _seed = seed;
            _combine = combine;
        }

        /// <summary>Add a child node (ignored for leaf nodes).</summary>
        public Builder AddChild(Builder child)
        {
            if (!_isLeaf) _children.Add(child);
            return this;
        }

        /// <summary>Add multiple child nodes (ignored for leaf nodes).</summary>
        public Builder AddChildren(params Builder[]? children)
        {
            if (!_isLeaf && children is not null && children.Length > 0)
                _children.AddRange(children);
            return this;
        }

        /// <summary>Build an immutable async composite tree.</summary>
        public AsyncComposite<TIn, TOut> Build()
        {
            if (_isLeaf)
            {
                return _leaf is null
                    ? throw new InvalidOperationException("Leaf operation is required.")
                    : new AsyncComposite<TIn, TOut>(true, _leaf, null, null, []);
            }

            if (_seed is null || _combine is null)
                throw new InvalidOperationException("Composite node requires Seed and Combine.");

            var children = new AsyncComposite<TIn, TOut>[_children.Count];
            for (var i = 0; i < _children.Count; i++)
                children[i] = _children[i].Build();

            return new AsyncComposite<TIn, TOut>(false, null, _seed, _combine, children);
        }
    }
}
