namespace PatternKit.Structural.Composite;

/// <summary>
/// Action Composite that treats individual leaves and compositions uniformly for side-effect operations.
/// Build a node as either a <b>Leaf</b> (single action) or a <b>Composite</b> (execute all children).
/// </summary>
/// <typeparam name="TIn">Input type.</typeparam>
/// <remarks>
/// <para>
/// This is the action (void-returning) counterpart to <see cref="Composite{TIn,TOut}"/>.
/// Use when operations produce side effects rather than values (logging, notifications, etc.).
/// </para>
/// <para>
/// <b>Immutability</b>: After <see cref="Builder.Build"/> the produced tree is immutable and safe for concurrent reuse.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var tree = ActionComposite&lt;Document&gt;.Node()
///     .AddChild(ActionComposite&lt;Document&gt;.Leaf(doc => Console.WriteLine($"Processing: {doc.Name}")))
///     .AddChild(ActionComposite&lt;Document&gt;.Node()
///         .AddChild(ActionComposite&lt;Document&gt;.Leaf(doc => ValidateDoc(doc)))
///         .AddChild(ActionComposite&lt;Document&gt;.Leaf(doc => IndexDoc(doc))))
///     .Build();
///
/// tree.Execute(document);
/// </code>
/// </example>
public sealed class ActionComposite<TIn>
{
    /// <summary>
    /// Delegate for a <b>leaf</b> action.
    /// </summary>
    public delegate void LeafAction(in TIn input);

    /// <summary>
    /// Delegate for a <b>composite</b> pre-action before processing children.
    /// </summary>
    public delegate void PreAction(in TIn input);

    /// <summary>
    /// Delegate for a <b>composite</b> post-action after processing children.
    /// </summary>
    public delegate void PostAction(in TIn input);

    private readonly bool _isLeaf;
    private readonly LeafAction? _leaf;
    private readonly PreAction? _pre;
    private readonly PostAction? _post;
    private readonly ActionComposite<TIn>[] _children;

    private ActionComposite(bool isLeaf, LeafAction? leaf, PreAction? pre, PostAction? post, ActionComposite<TIn>[] children)
    {
        _isLeaf = isLeaf;
        _leaf = leaf;
        _pre = pre;
        _post = post;
        _children = children;
    }

    /// <summary>Execute the leaf/composite action.</summary>
    public void Execute(in TIn input)
    {
        if (_isLeaf)
        {
            _leaf!(in input);
            return;
        }

        _pre?.Invoke(in input);

        foreach (var child in _children)
            child.Execute(in input);

        _post?.Invoke(in input);
    }

    /// <summary>Create a builder for a <b>Leaf</b> node with the given action.</summary>
    public static Builder Leaf(LeafAction action) => new(leaf: action);

    /// <summary>Create a builder for a <b>Composite</b> node.</summary>
    public static Builder Node(PreAction? pre = null, PostAction? post = null) => new(pre, post);

    /// <summary>Fluent builder for <see cref="ActionComposite{TIn}"/> nodes.</summary>
    public sealed class Builder
    {
        private readonly bool _isLeaf;
        private readonly LeafAction? _leaf;
        private readonly PreAction? _pre;
        private readonly PostAction? _post;
        private readonly List<Builder> _children = new(4);

        internal Builder(LeafAction leaf)
        {
            _isLeaf = true;
            _leaf = leaf;
        }

        internal Builder(PreAction? pre, PostAction? post)
        {
            _isLeaf = false;
            _pre = pre;
            _post = post;
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

        /// <summary>Build an immutable action composite tree.</summary>
        public ActionComposite<TIn> Build()
        {
            if (_isLeaf)
            {
                return _leaf is null
                    ? throw new InvalidOperationException("Leaf action is required.")
                    : new ActionComposite<TIn>(true, _leaf, null, null, []);
            }

            var children = new ActionComposite<TIn>[_children.Count];
            for (var i = 0; i < _children.Count; i++)
                children[i] = _children[i].Build();

            return new ActionComposite<TIn>(false, null, _pre, _post, children);
        }
    }
}
