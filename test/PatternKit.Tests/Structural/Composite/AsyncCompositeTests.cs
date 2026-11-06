using PatternKit.Structural.Composite;
using TinyBDD;

namespace PatternKit.Tests.Structural.Composite;

public sealed class AsyncCompositeTests
{
    #region AsyncComposite<TIn, TOut> Tests

    [Scenario("AsyncComposite Leaf Executes")]
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

        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncComposite Leaf Sync Executes")]
    [Fact]
    public async Task AsyncComposite_Leaf_Sync_Executes()
    {
        var leaf = AsyncComposite<int, int>.Leaf(x => x * 2)
            .Build();

        var result = await leaf.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result);
    }

    [Scenario("AsyncComposite Node Aggregates Results")]
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

        ScenarioExpect.Equal(25, result); // 10 + 15
    }

    [Scenario("AsyncComposite Node Sync Aggregates")]
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

        ScenarioExpect.Equal(10, result); // 5 + 5
    }

    [Scenario("AsyncComposite Nested Composites")]
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

        ScenarioExpect.Equal(60, result); // 10 + 50
    }

    [Scenario("AsyncComposite Empty Node Returns Seed")]
    [Fact]
    public async Task AsyncComposite_Empty_Node_Returns_Seed()
    {
        var composite = AsyncComposite<int, int>.Node(
                _ => 42,
                (_, acc, child) => acc + child)
            .Build();

        var result = await composite.ExecuteAsync(5);

        ScenarioExpect.Equal(42, result);
    }

    [Scenario("AsyncComposite AddChildren Multiple")]
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

        ScenarioExpect.Equal(24, result); // 1 * 2 * 3 * 4
    }

    [Scenario("AsyncComposite AddChild Ignored On Leaf")]
    [Fact]
    public async Task AsyncComposite_AddChild_Ignored_On_Leaf()
    {
        var leaf = AsyncComposite<int, int>.Leaf(x => x * 2)
            .AddChild(AsyncComposite<int, int>.Leaf(x => x * 100)) // Should be ignored
            .Build();

        var result = await leaf.ExecuteAsync(5);

        ScenarioExpect.Equal(10, result); // Child is ignored
    }

    #endregion

    #region ActionComposite<TIn> Tests

    [Scenario("ActionComposite Leaf Executes")]
    [Fact]
    public void ActionComposite_Leaf_Executes()
    {
        var executed = false;
        var leaf = ActionComposite<int>.Leaf((in x) => executed = true)
            .Build();

        leaf.Execute(5);

        ScenarioExpect.True(executed);
    }

    [Scenario("ActionComposite Node Executes All Children")]
    [Fact]
    public void ActionComposite_Node_Executes_All_Children()
    {
        var log = new List<string>();
        var composite = ActionComposite<int>.Node()
            .AddChild(ActionComposite<int>.Leaf((in x) => log.Add($"leaf1-{x}")))
            .AddChild(ActionComposite<int>.Leaf((in x) => log.Add($"leaf2-{x}")))
            .Build();

        composite.Execute(5);

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("leaf1-5", log[0]);
        ScenarioExpect.Equal("leaf2-5", log[1]);
    }

    [Scenario("ActionComposite Node With Pre Post")]
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

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("pre", log[0]);
        ScenarioExpect.Equal("child", log[1]);
        ScenarioExpect.Equal("post", log[2]);
    }

    [Scenario("ActionComposite Nested")]
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

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("inner", log[0]);
        ScenarioExpect.Equal("outer", log[1]);
    }

    [Scenario("ActionComposite AddChildren Multiple")]
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

        ScenarioExpect.Equal([1, 2, 3], log);
    }

    [Scenario("ActionComposite Empty Node Executes")]
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

        ScenarioExpect.True(preExecuted);
        ScenarioExpect.True(postExecuted);
    }

    #endregion

    #region AsyncActionComposite<TIn> Tests

    [Scenario("AsyncActionComposite Leaf Executes")]
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

        ScenarioExpect.True(executed);
    }

    [Scenario("AsyncActionComposite Leaf Sync Executes")]
    [Fact]
    public async Task AsyncActionComposite_Leaf_Sync_Executes()
    {
        var executed = false;
        var composite = AsyncActionComposite<int>.Leaf(x => executed = true)
            .Build();

        await composite.ExecuteAsync(5);

        ScenarioExpect.True(executed);
    }

    [Scenario("AsyncActionComposite Node Executes Children Sequentially")]
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

        ScenarioExpect.Equal(2, log.Count);
        ScenarioExpect.Equal("child1", log[0]);
        ScenarioExpect.Equal("child2", log[1]);
    }

    [Scenario("AsyncActionComposite ParallelNode Executes Children Concurrently")]
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

        ScenarioExpect.Equal(2, log.Count);
        // Fast should complete before slow because parallel execution
        ScenarioExpect.Equal("fast", log[0]);
        ScenarioExpect.Equal("slow", log[1]);
    }

    [Scenario("AsyncActionComposite Node With PrePost Hooks")]
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

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("pre", log[0]);
        ScenarioExpect.Equal("child", log[1]);
        ScenarioExpect.Equal("post", log[2]);
    }

    [Scenario("AsyncActionComposite AddChildren Multiple")]
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

        ScenarioExpect.Equal(3, count);
    }

    [Scenario("AsyncActionComposite Nested Structure")]
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

        ScenarioExpect.Equal(3, log.Count);
        ScenarioExpect.Equal("inner-pre", log[0]);
        ScenarioExpect.Equal("inner-leaf", log[1]);
        ScenarioExpect.Equal("outer-leaf", log[2]);
    }

    [Scenario("AsyncActionComposite AddChild Ignored For Leaf")]
    [Fact]
    public void AsyncActionComposite_AddChild_Ignored_For_Leaf()
    {
        // Adding children to a leaf should be ignored
        var builder = AsyncActionComposite<int>.Leaf(x => { })
            .AddChild(AsyncActionComposite<int>.Leaf(x => { }));

        // Should still build successfully as a leaf
        var composite = builder.Build();
        ScenarioExpect.NotNull(composite);
    }

    [Scenario("AsyncActionComposite Leaf Null Throws")]
    [Fact]
    public void AsyncActionComposite_Leaf_Null_Throws()
    {
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncActionComposite<int>.Leaf((AsyncActionComposite<int>.LeafAction)null!).Build());
    }

    #endregion

    #region Null Argument Tests

    [Scenario("AsyncComposite Leaf Null Throws")]
    [Fact]
    public void AsyncComposite_Leaf_Null_Throws()
    {
        // Build() validates that leaf operation is set and throws InvalidOperationException
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncComposite<int, int>.Leaf((AsyncComposite<int, int>.LeafOp)null!).Build());
    }

    [Scenario("ActionComposite Leaf Null Throws")]
    [Fact]
    public void ActionComposite_Leaf_Null_Throws()
    {
        // Build() validates that leaf operation is set and throws InvalidOperationException
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            ActionComposite<int>.Leaf(null!).Build());
    }

    #endregion
}
