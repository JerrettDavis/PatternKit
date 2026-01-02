using PatternKit.Structural.Composite;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural;

[Feature("AsyncComposite<TIn,TOut> (async composite pattern)")]
public sealed class AsyncCompositeTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Ctx(
        AsyncComposite<int, int> Tree,
        int? Result = null
    );

    private static Ctx Build_SumTree()
    {
        // Root: sum of children
        // Children: leaf nodes that multiply input by 1, 2, 3
        var tree = AsyncComposite<int, int>.Node(
                seed: (_, _) => new ValueTask<int>(0),
                combine: (_, acc, child, _) => new ValueTask<int>(acc + child))
            .AddChild(AsyncComposite<int, int>.Leaf((n, _) => new ValueTask<int>(n * 1)))
            .AddChild(AsyncComposite<int, int>.Leaf((n, _) => new ValueTask<int>(n * 2)))
            .AddChild(AsyncComposite<int, int>.Leaf((n, _) => new ValueTask<int>(n * 3)))
            .Build();

        return new Ctx(tree);
    }

    private static Ctx Build_NestedTree()
    {
        // Nested: outer sums two inner sums
        var inner1 = AsyncComposite<int, int>.Node(
                seed: (_, _) => new ValueTask<int>(0),
                combine: (_, acc, child, _) => new ValueTask<int>(acc + child))
            .AddChild(AsyncComposite<int, int>.Leaf((n, _) => new ValueTask<int>(n)))
            .AddChild(AsyncComposite<int, int>.Leaf((n, _) => new ValueTask<int>(n)));

        var inner2 = AsyncComposite<int, int>.Node(
                seed: (_, _) => new ValueTask<int>(0),
                combine: (_, acc, child, _) => new ValueTask<int>(acc + child))
            .AddChild(AsyncComposite<int, int>.Leaf((n, _) => new ValueTask<int>(n * 2)));

        var tree = AsyncComposite<int, int>.Node(
                seed: (_, _) => new ValueTask<int>(0),
                combine: (_, acc, child, _) => new ValueTask<int>(acc + child))
            .AddChild(inner1)
            .AddChild(inner2)
            .Build();

        return new Ctx(tree);
    }

    private static Ctx Build_SyncLeaves()
    {
        var tree = AsyncComposite<int, int>.Node(
                seed: n => 0,
                combine: (_, acc, child) => acc + child)
            .AddChild(AsyncComposite<int, int>.Leaf(n => n))
            .AddChild(AsyncComposite<int, int>.Leaf(n => n * 2))
            .Build();

        return new Ctx(tree);
    }

    private static async Task<Ctx> ExecAsync(Ctx c, int n)
    {
        var result = await c.Tree.ExecuteAsync(n);
        return c with { Result = result };
    }

    [Scenario("Leaf nodes execute and return values")]
    [Fact]
    public async Task LeafExecution()
    {
        var leaf = AsyncComposite<int, int>.Leaf((n, _) => new ValueTask<int>(n * 10)).Build();
        var ctx = new Ctx(leaf);

        await Given("a single async leaf that multiplies by 10", () => ctx)
            .When("executing with 5", c => ExecAsync(c, 5))
            .Then("returns 50", c => c.Result == 50)
            .AssertPassed();
    }

    [Scenario("Composite sums child results")]
    [Fact]
    public async Task CompositeSumsChildren()
    {
        // n*1 + n*2 + n*3 = n*6
        await Given("a sum tree with multiplier leaves (1,2,3)", Build_SumTree)
            .When("executing with 10", c => ExecAsync(c, 10))
            .Then("returns 10+20+30=60", c => c.Result == 60)
            .AssertPassed();
    }

    [Scenario("Nested composites work correctly")]
    [Fact]
    public async Task NestedComposites()
    {
        // inner1: n + n = 2n
        // inner2: n*2 = 2n
        // outer: 2n + 2n = 4n
        await Given("nested sum composites", Build_NestedTree)
            .When("executing with 5", c => ExecAsync(c, 5))
            .Then("returns 5+5 + 10 = 20", c => c.Result == 20)
            .AssertPassed();
    }

    [Scenario("Sync leaf adapters work")]
    [Fact]
    public async Task SyncAdapters()
    {
        await Given("a tree using sync adapters", Build_SyncLeaves)
            .When("executing with 3", c => ExecAsync(c, 3))
            .Then("returns 3+6=9", c => c.Result == 9)
            .AssertPassed();
    }
}
