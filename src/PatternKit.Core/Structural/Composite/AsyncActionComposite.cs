namespace PatternKit.Structural.Composite;

/// <summary>
/// Async Action Composite that treats individual leaves and compositions uniformly for async side-effect operations.
/// Build a node as either a <b>Leaf</b> (single async action) or a <b>Composite</b> (execute all children).
/// </summary>
/// <typeparam name="TIn">Input type.</typeparam>
/// <remarks>
/// <para>
/// This combines features of <see cref="AsyncComposite{TIn,TOut}"/> and <see cref="ActionComposite{TIn}"/>
/// for async void-returning operations.
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/> the produced tree is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var tree = AsyncActionComposite&lt;Order&gt;.Node()
///     .AddChild(AsyncActionComposite&lt;Order&gt;.Leaf(
///         async (order, ct) => await NotifyWarehouseAsync(order, ct)))
///     .AddChild(AsyncActionComposite&lt;Order&gt;.Leaf(
///         async (order, ct) => await SendConfirmationAsync(order, ct)))
///     .Build();
///
/// await tree.ExecuteAsync(order);
/// </code>
/// </example>
public sealed class AsyncActionComposite<TIn>
{
    /// <summary>
    /// Async delegate for a <b>leaf</b> action.
    /// </summary>
    public delegate ValueTask LeafAction(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for a <b>composite</b> pre-action before processing children.
    /// </summary>
    public delegate ValueTask PreAction(TIn input, CancellationToken ct);

    /// <summary>
    /// Async delegate for a <b>composite</b> post-action after processing children.
    /// </summary>
    public delegate ValueTask PostAction(TIn input, CancellationToken ct);

    private readonly bool _isLeaf;
    private readonly LeafAction? _leaf;
    private readonly PreAction? _pre;
    private readonly PostAction? _post;
    private readonly bool _parallel;
    private readonly AsyncActionComposite<TIn>[] _children;

    private AsyncActionComposite(bool isLeaf, LeafAction? leaf, PreAction? pre, PostAction? post, bool parallel, AsyncActionComposite<TIn>[] children)
    {
        _isLeaf = isLeaf;
        _leaf = leaf;
        _pre = pre;
        _post = post;
        _parallel = parallel;
        _children = children;
    }

    /// <summary>Execute the leaf/composite action asynchronously.</summary>
    public async ValueTask ExecuteAsync(TIn input, CancellationToken ct = default)
    {
        if (_isLeaf)
        {
            await _leaf!(input, ct).ConfigureAwait(false);
            return;
        }

        if (_pre is not null)
            await _pre(input, ct).ConfigureAwait(false);

        if (_parallel && _children.Length > 1)
        {
            var tasks = new ValueTask[_children.Length];
            for (var i = 0; i < _children.Length; i++)
                tasks[i] = _children[i].ExecuteAsync(input, ct);

            foreach (var task in tasks)
                await task.ConfigureAwait(false);
        }
        else
        {
            foreach (var child in _children)
                await child.ExecuteAsync(input, ct).ConfigureAwait(false);
        }

        if (_post is not null)
            await _post(input, ct).ConfigureAwait(false);
    }

    /// <summary>Create a builder for a <b>Leaf</b> node with the given async action.</summary>
    public static Builder Leaf(LeafAction action) => new(leaf: action);

    /// <summary>Create a builder for a <b>Leaf</b> node with a sync action.</summary>
    public static Builder Leaf(Action<TIn> action) =>
        new(leaf: (input, _) =>
        {
            action(input);
            return default;
        });

    /// <summary>Create a builder for a <b>Composite</b> node with sequential execution.</summary>
    public static Builder Node(PreAction? pre = null, PostAction? post = null) => new(pre, post, parallel: false);

    /// <summary>Create a builder for a <b>Composite</b> node with parallel execution of children.</summary>
    public static Builder ParallelNode(PreAction? pre = null, PostAction? post = null) => new(pre, post, parallel: true);

    /// <summary>Fluent builder for <see cref="AsyncActionComposite{TIn}"/> nodes.</summary>
    public sealed class Builder
    {
        private readonly bool _isLeaf;
        private readonly LeafAction? _leaf;
        private readonly PreAction? _pre;
        private readonly PostAction? _post;
        private readonly bool _parallel;
        private readonly List<Builder> _children = new(4);

        internal Builder(LeafAction leaf)
        {
            _isLeaf = true;
            _leaf = leaf;
        }

        internal Builder(PreAction? pre, PostAction? post, bool parallel)
        {
            _isLeaf = false;
            _pre = pre;
            _post = post;
            _parallel = parallel;
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

        /// <summary>Build an immutable async action composite tree.</summary>
        public AsyncActionComposite<TIn> Build()
        {
            if (_isLeaf)
            {
                return _leaf is null
                    ? throw new InvalidOperationException("Leaf action is required.")
                    : new AsyncActionComposite<TIn>(true, _leaf, null, null, false, []);
            }

            var children = new AsyncActionComposite<TIn>[_children.Count];
            for (var i = 0; i < _children.Count; i++)
                children[i] = _children[i].Build();

            return new AsyncActionComposite<TIn>(false, null, _pre, _post, _parallel, children);
        }
    }
}
