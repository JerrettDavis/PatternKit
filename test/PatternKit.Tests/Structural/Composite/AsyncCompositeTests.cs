using PatternKit.Structural.Composite;

namespace PatternKit.Tests.Structural.Composite;

public sealed class AsyncCompositeTests
{
    #region AsyncComposite<TIn, TOut> Tests

    [Fact]
    public async Task AsyncComposite_Leaf_Executes()
    {
        var leaf = AsyncComposite<int, int>.Leaf(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                return x * 2;
            })
            .Build();

        var result = await leaf.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncComposite_Leaf_Sync_Executes()
    {
        var leaf = AsyncComposite<int, int>.Leaf(x => x * 2)
            .Build();

        var result = await leaf.ExecuteAsync(5);

        Assert.Equal(10, result);
    }

    [Fact]
    public async Task AsyncComposite_Node_Aggregates_Results()
    {
        var composite = AsyncComposite<int, int>.Node(
                seed: (input, ct) => new ValueTask<int>(0),
                combine: (input, acc, child, ct) => new ValueTask<int>(acc + child))
            .AddChild(AsyncComposite<int, int>.Leaf(x => x * 2))
            .AddChild(AsyncComposite<int, int>.Leaf(x => x * 3))
            .Build();

        var result = await composite.ExecuteAsync(5);

        Assert.Equal(25, result); // 10 + 15
    }

    [Fact]
    public async Task AsyncComposite_Node_Sync_Aggregates()
    {
        var composite = AsyncComposite<int, int>.Node(
                seed: _ => 0,
                combine: (_, acc, child) => acc + child)
            .AddChild(AsyncComposite<int, int>.Leaf(x => x))
            .AddChild(AsyncComposite<int, int>.Leaf(x => x))
            .Build();

        var result = await composite.ExecuteAsync(5);

        Assert.Equal(10, result); // 5 + 5
    }

    [Fact]
    public async Task AsyncComposite_Nested_Composites()
    {
        var innerComposite = AsyncComposite<int, int>.Node(
                _ => 0,
                (_, acc, child) => acc + child)
            .AddChild(AsyncComposite<int, int>.Leaf(x => x))
            .AddChild(AsyncComposite<int, int>.Leaf(x => x))
            .Build();

        var outerComposite = AsyncComposite<int, int>.Node(
                _ => 0,
                (_, acc, child) => acc + child)
            .AddChild(AsyncComposite<int, int>.Leaf(_ => innerComposite.ExecuteAsync(5).AsTask().Result))
            .AddChild(AsyncComposite<int, int>.Leaf(x => x * 10))
            .Build();

        // Note: This test shows composition but uses sync approach for inner
        var result = await outerComposite.ExecuteAsync(5);

        Assert.Equal(60, result); // 10 + 50
    }

    [Fact]
    public async Task AsyncComposite_Empty_Node_Returns_Seed()
    {
        var composite = AsyncComposite<int, int>.Node(
                _ => 42,
                (_, acc, child) => acc + child)
            .Build();

        var result = await composite.ExecuteAsync(5);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task AsyncComposite_AddChildren_Multiple()
    {
        var composite = AsyncComposite<int, int>.Node(
                _ => 1,
                (_, acc, child) => acc * child)
            .AddChildren(
                AsyncComposite<int, int>.Leaf(_ => 2),
                AsyncComposite<int, int>.Leaf(_ => 3),
                AsyncComposite<int, int>.Leaf(_ => 4))
            .Build();

        var result = await composite.ExecuteAsync(0);

        Assert.Equal(24, result); // 1 * 2 * 3 * 4
    }

    [Fact]
    public async Task AsyncComposite_AddChild_Ignored_On_Leaf()
    {
        var leaf = AsyncComposite<int, int>.Leaf(x => x * 2)
            .AddChild(AsyncComposite<int, int>.Leaf(x => x * 100)) // Should be ignored
            .Build();

        var result = await leaf.ExecuteAsync(5);

        Assert.Equal(10, result); // Child is ignored
    }

    #endregion

    #region ActionComposite<TIn> Tests

    [Fact]
    public void ActionComposite_Leaf_Executes()
    {
        var executed = false;
        var leaf = ActionComposite<int>.Leaf((in x) => executed = true)
            .Build();

        leaf.Execute(5);

        Assert.True(executed);
    }

    [Fact]
    public void ActionComposite_Node_Executes_All_Children()
    {
        var log = new List<string>();
        var composite = ActionComposite<int>.Node()
            .AddChild(ActionComposite<int>.Leaf((in x) => log.Add($"leaf1-{x}")))
            .AddChild(ActionComposite<int>.Leaf((in x) => log.Add($"leaf2-{x}")))
            .Build();

        composite.Execute(5);

        Assert.Equal(2, log.Count);
        Assert.Equal("leaf1-5", log[0]);
        Assert.Equal("leaf2-5", log[1]);
    }

    [Fact]
    public void ActionComposite_Node_With_Pre_Post()
    {
        var log = new List<string>();
        var composite = ActionComposite<int>.Node(
                pre: (in _) => log.Add("pre"),
                post: (in _) => log.Add("post"))
            .AddChild(ActionComposite<int>.Leaf((in _) => log.Add("child")))
            .Build();

        composite.Execute(5);

        Assert.Equal(3, log.Count);
        Assert.Equal("pre", log[0]);
        Assert.Equal("child", log[1]);
        Assert.Equal("post", log[2]);
    }

    [Fact]
    public void ActionComposite_Nested()
    {
        var log = new List<string>();
        var inner = ActionComposite<int>.Node()
            .AddChild(ActionComposite<int>.Leaf((in _) => log.Add("inner")))
            .Build();

        var outer = ActionComposite<int>.Node()
            .AddChild(ActionComposite<int>.Leaf((in _) => inner.Execute(1)))
            .AddChild(ActionComposite<int>.Leaf((in _) => log.Add("outer")))
            .Build();

        outer.Execute(5);

        Assert.Equal(2, log.Count);
        Assert.Equal("inner", log[0]);
        Assert.Equal("outer", log[1]);
    }

    [Fact]
    public void ActionComposite_AddChildren_Multiple()
    {
        var log = new List<int>();
        var composite = ActionComposite<int>.Node()
            .AddChildren(
                ActionComposite<int>.Leaf((in _) => log.Add(1)),
                ActionComposite<int>.Leaf((in _) => log.Add(2)),
                ActionComposite<int>.Leaf((in _) => log.Add(3)))
            .Build();

        composite.Execute(0);

        Assert.Equal([1, 2, 3], log);
    }

    [Fact]
    public void ActionComposite_Empty_Node_Executes()
    {
        var preExecuted = false;
        var postExecuted = false;
        var composite = ActionComposite<int>.Node(
                pre: (in _) => preExecuted = true,
                post: (in _) => postExecuted = true)
            .Build();

        composite.Execute(5);

        Assert.True(preExecuted);
        Assert.True(postExecuted);
    }

    #endregion

    #region AsyncActionComposite<TIn> Tests

    [Fact]
    public async Task AsyncActionComposite_Leaf_Executes()
    {
        var executed = false;
        var composite = AsyncActionComposite<int>.Leaf(async (x, ct) =>
            {
                await Task.Delay(1, ct);
                executed = true;
            })
            .Build();

        await composite.ExecuteAsync(5);

        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionComposite_Leaf_Sync_Executes()
    {
        var executed = false;
        var composite = AsyncActionComposite<int>.Leaf(x => executed = true)
            .Build();

        await composite.ExecuteAsync(5);

        Assert.True(executed);
    }

    [Fact]
    public async Task AsyncActionComposite_Node_Executes_Children_Sequentially()
    {
        var log = new List<string>();
        var composite = AsyncActionComposite<int>.Node()
            .AddChild(AsyncActionComposite<int>.Leaf(async (x, ct) =>
            {
                await Task.Delay(10, ct);
                log.Add("child1");
            }))
            .AddChild(AsyncActionComposite<int>.Leaf((x, ct) =>
            {
                log.Add("child2");
                return default;
            }))
            .Build();

        await composite.ExecuteAsync(5);

        Assert.Equal(2, log.Count);
        Assert.Equal("child1", log[0]);
        Assert.Equal("child2", log[1]);
    }

    [Fact]
    public async Task AsyncActionComposite_ParallelNode_Executes_Children_Concurrently()
    {
        var log = new List<string>();
        var lockObj = new object();
        var composite = AsyncActionComposite<int>.ParallelNode()
            .AddChild(AsyncActionComposite<int>.Leaf(async (x, ct) =>
            {
                await Task.Delay(50, ct);
                lock (lockObj) log.Add("slow");
            }))
            .AddChild(AsyncActionComposite<int>.Leaf((x, ct) =>
            {
                lock (lockObj) log.Add("fast");
                return default;
            }))
            .Build();

        await composite.ExecuteAsync(5);

        Assert.Equal(2, log.Count);
        // Fast should complete before slow because parallel execution
        Assert.Equal("fast", log[0]);
        Assert.Equal("slow", log[1]);
    }

    [Fact]
    public async Task AsyncActionComposite_Node_With_PrePost_Hooks()
    {
        var log = new List<string>();
        var composite = AsyncActionComposite<int>.Node(
                pre: async (x, ct) => log.Add("pre"),
                post: async (x, ct) => log.Add("post"))
            .AddChild(AsyncActionComposite<int>.Leaf(x => log.Add("child")))
            .Build();

        await composite.ExecuteAsync(5);

        Assert.Equal(3, log.Count);
        Assert.Equal("pre", log[0]);
        Assert.Equal("child", log[1]);
        Assert.Equal("post", log[2]);
    }

    [Fact]
    public async Task AsyncActionComposite_AddChildren_Multiple()
    {
        var count = 0;
        var composite = AsyncActionComposite<int>.Node()
            .AddChildren(
                AsyncActionComposite<int>.Leaf(x => Interlocked.Increment(ref count)),
                AsyncActionComposite<int>.Leaf(x => Interlocked.Increment(ref count)),
                AsyncActionComposite<int>.Leaf(x => Interlocked.Increment(ref count)))
            .Build();

        await composite.ExecuteAsync(5);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task AsyncActionComposite_Nested_Structure()
    {
        var log = new List<string>();
        var composite = AsyncActionComposite<int>.Node()
            .AddChild(AsyncActionComposite<int>.Node(
                    pre: async (x, ct) => log.Add("inner-pre"))
                .AddChild(AsyncActionComposite<int>.Leaf(x => log.Add("inner-leaf"))))
            .AddChild(AsyncActionComposite<int>.Leaf(x => log.Add("outer-leaf")))
            .Build();

        await composite.ExecuteAsync(5);

        Assert.Equal(3, log.Count);
        Assert.Equal("inner-pre", log[0]);
        Assert.Equal("inner-leaf", log[1]);
        Assert.Equal("outer-leaf", log[2]);
    }

    [Fact]
    public void AsyncActionComposite_AddChild_Ignored_For_Leaf()
    {
        // Adding children to a leaf should be ignored
        var builder = AsyncActionComposite<int>.Leaf(x => { })
            .AddChild(AsyncActionComposite<int>.Leaf(x => { }));

        // Should still build successfully as a leaf
        var composite = builder.Build();
        Assert.NotNull(composite);
    }

    [Fact]
    public void AsyncActionComposite_Leaf_Null_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AsyncActionComposite<int>.Leaf((AsyncActionComposite<int>.LeafAction)null!).Build());
    }

    #endregion

    #region Null Argument Tests

    [Fact]
    public void AsyncComposite_Leaf_Null_Throws()
    {
        // Build() validates that leaf operation is set and throws InvalidOperationException
        Assert.Throws<InvalidOperationException>(() =>
            AsyncComposite<int, int>.Leaf((AsyncComposite<int, int>.LeafOp)null!).Build());
    }

    [Fact]
    public void ActionComposite_Leaf_Null_Throws()
    {
        // Build() validates that leaf operation is set and throws InvalidOperationException
        Assert.Throws<InvalidOperationException>(() =>
            ActionComposite<int>.Leaf(null!).Build());
    }

    #endregion
}
