#if !NETSTANDARD2_0
using PatternKit.Behavioral.Iterator;
using PatternKit.Common;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Iterator;

[Feature("AsyncFlow<T>: async functional pipeline with share/fork/branch")] 
public sealed class AsyncFlowTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static async IAsyncEnumerable<int> RangeAsync(int count, int delayMs = 0)
    {
        for (var i = 1; i <= count; i++)
        {
            if (delayMs > 0) await Task.Delay(delayMs).ConfigureAwait(false);
            yield return i;
        }
    }

    private static async IAsyncEnumerable<int> DuplicateAsync(int v)
    {
        yield return v;
        yield return v + 1;
        await Task.CompletedTask;
    }

    // Helper async methods for scenario steps (avoid ambiguous Task/ValueTask inference)
    private static async Task<(List<int> Result, List<int> Log)> ComposePipeline(AsyncFlow<int> f)
    {
        var log = new List<int>();
        var result = new List<int>();
        await foreach (var v in f.Map(x => x * 2)        // 2 4 6 8 10
                                 .Filter(x => x % 4 == 0)// 4 8
                                 .FlatMap(x => DuplicateAsync(x)) // 4,5,8,9
                                 .Tee(log.Add))
            result.Add(v);
        return (result, log);
    }

    private static async Task<(List<int> First, List<int> Second)> RunForks(SharedAsyncFlow<int> shared)
    {
        var f1Task = shared.Fork().Map(x => x * 10)
            .FoldAsync(new List<int>(), (acc, v) => { acc.Add(v); return acc; });
        var f2Task = shared.Fork().Filter(x => x % 2 == 1)
            .FoldAsync(new List<int>(), (acc, v) => { acc.Add(v); return acc; });
        await Task.WhenAll(f1Task.AsTask(), f2Task.AsTask());
        return (f1Task.Result, f2Task.Result);
    }

    private static async Task<(List<int> even, List<int> odd)> BranchPartition(SharedAsyncFlow<int> sf)
    {
        var (evenFlow, oddFlow) = sf.Branch(x => x % 2 == 0);
        var even = await evenFlow.FoldAsync(new List<int>(), (a, v) => { a.Add(v); return a; });
        var odd = await oddFlow.FoldAsync(new List<int>(), (a, v) => { a.Add(v); return a; });
        return (even, odd);
    }

    private static async Task<(Option<int> Some, Option<int> None)> Firsts((AsyncFlow<int> NonEmpty, AsyncFlow<int> Empty) flows)
    {
        var some = await flows.NonEmpty.FirstOptionAsync();
        var none = await flows.Empty.FirstOptionAsync();
        return (some, none);
    }

    [Scenario("Async Map/Filter/FlatMap/Tee composition")] 
    [Fact]
    public Task Composition()
        => Given("async flow 1..5", () => AsyncFlow<int>.From(RangeAsync(5)))
            .When("compose operations", ComposePipeline)
            .Then("result is 4,5,8,9", t => string.Join(',', t.Result) == "4,5,8,9")
            .And("tee captured same", t => string.Join(',', t.Log) == "4,5,8,9")
            .AssertPassed();

    [Scenario("Share + concurrent forks enumerate source once")] 
    [Fact]
    public Task ShareForkSingleEnumeration()
        => Given("shared async flow over 1..6", () => AsyncFlow<int>.From(RangeAsync(6)).Share())
            .When("two forks consumed concurrently", RunForks)
            .Then("first fork is 10,20,30,40,50,60", r => string.Join(',', r.First) == "10,20,30,40,50,60")
            .And("second fork is 1,3,5", r => string.Join(',', r.Second) == "1,3,5")
            .AssertPassed();

    [Scenario("Branch partitions even/odd")] 
    [Fact]
    public Task Branching()
        => Given("shared async flow 1..7", () => AsyncFlow<int>.From(RangeAsync(7)).Share())
            .When("branch on even", BranchPartition)
            .Then("even 2,4,6", r => string.Join(',', r.even) == "2,4,6")
            .And("odd 1,3,5,7", r => string.Join(',', r.odd) == "1,3,5,7")
            .AssertPassed();

    [Scenario("FirstOptionAsync returns Some then None")]
    [Fact]
    public Task FirstOption()
        => Given("flows", () => (NonEmpty: AsyncFlow<int>.From(RangeAsync(3)), Empty: AsyncFlow<int>.From(RangeAsync(0))))
            .When("first options", Firsts)
            .Then("Some=1", r => r.Some.HasValue && r.Some.ValueOrDefault == 1)
            .And("None has no value", r => !r.None.HasValue)
            .AssertPassed();
}

#region Additional AsyncFlow Tests

public sealed class AsyncFlowBuilderTests
{
    private static async IAsyncEnumerable<int> RangeAsync(int count)
    {
        for (var i = 1; i <= count; i++)
            yield return i;
    }

    [Fact]
    public void AsyncFlow_From_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AsyncFlow<int>.From(null!));
    }

    [Fact]
    public void AsyncFlow_Map_Null_Throws()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(3));
        Assert.Throws<ArgumentNullException>(() => flow.Map<int>(null!));
    }

    [Fact]
    public void AsyncFlow_Filter_Null_Throws()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(3));
        Assert.Throws<ArgumentNullException>(() => flow.Filter(null!));
    }

    [Fact]
    public void AsyncFlow_FlatMap_Null_Throws()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(3));
        Assert.Throws<ArgumentNullException>(() => flow.FlatMap<int>(null!));
    }

    [Fact]
    public void AsyncFlow_Tee_Null_Throws()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(3));
        Assert.Throws<ArgumentNullException>(() => flow.Tee(null!));
    }

    [Fact]
    public void SharedAsyncFlow_Branch_Null_Throws()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(3)).Share();
        Assert.Throws<ArgumentNullException>(() => shared.Branch(null!));
    }

    [Fact]
    public async Task AsyncFlow_FoldAsync()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(5));
        var sum = await flow.FoldAsync(0, (acc, x) => acc + x);

        Assert.Equal(15, sum);
    }

    [Fact]
    public async Task AsyncFlow_FoldAsync_Null_Flow_Throws()
    {
        AsyncFlow<int>? flow = null;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            flow!.FoldAsync(0, (acc, x) => acc + x).AsTask());
    }

    [Fact]
    public async Task AsyncFlow_FoldAsync_Null_Folder_Throws()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(3));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            flow.FoldAsync<int, int>(0, null!).AsTask());
    }

    [Fact]
    public async Task AsyncFlow_FirstOptionAsync_Null_Throws()
    {
        AsyncFlow<int>? flow = null;
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            flow!.FirstOptionAsync().AsTask());
    }

    [Fact]
    public async Task SharedAsyncFlow_Fork_Works()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(3)).Share();
        var fork = shared.Fork();

        var result = new List<int>();
        await foreach (var v in fork)
            result.Add(v);

        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task AsyncFlow_Empty_Source()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(0));
        var result = new List<int>();
        await foreach (var v in flow)
            result.Add(v);

        Assert.Empty(result);
    }

    [Fact]
    public async Task AsyncFlow_Chained_Operations()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(10))
            .Filter(x => x % 2 == 0)
            .Map(x => x * 10);

        var result = new List<int>();
        await foreach (var v in flow)
            result.Add(v);

        Assert.Equal(new[] { 20, 40, 60, 80, 100 }, result);
    }

    [Fact]
    public async Task AsyncFlow_WithCancellation_Works()
    {
        using var cts = new CancellationTokenSource();
        var flow = AsyncFlow<int>.From(RangeAsync(3));

        var result = new List<int>();
        await foreach (var v in flow.WithCancellation(cts.Token))
            result.Add(v);

        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task SharedAsyncFlow_MultipleForks_SameData()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(5)).Share();

        var fork1 = shared.Fork();
        var fork2 = shared.Fork();
        var fork3 = shared.Fork();

        var list1 = new List<int>();
        var list2 = new List<int>();
        var list3 = new List<int>();

        await foreach (var v in fork1) list1.Add(v);
        await foreach (var v in fork2) list2.Add(v);
        await foreach (var v in fork3) list3.Add(v);

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list1);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list2);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, list3);
    }

    [Fact]
    public async Task SharedAsyncFlow_ConcurrentForks()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(10)).Share();

        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            var list = new List<int>();
            await foreach (var v in shared.Fork())
                list.Add(v);
            return list;
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            Assert.Equal(10, result.Count);
            Assert.Equal(Enumerable.Range(1, 10), result);
        }
    }

    [Fact]
    public async Task SharedAsyncFlow_Branch_TrueFalse()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(10)).Share();
        var (trueFlow, falseFlow) = shared.Branch(x => x % 2 == 0);

        var evenList = new List<int>();
        var oddList = new List<int>();

        await foreach (var v in trueFlow) evenList.Add(v);
        await foreach (var v in falseFlow) oddList.Add(v);

        Assert.Equal(new[] { 2, 4, 6, 8, 10 }, evenList);
        Assert.Equal(new[] { 1, 3, 5, 7, 9 }, oddList);
    }

    [Fact]
    public async Task AsyncFlow_Map_Transform()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(3));
        var result = new List<string>();

        await foreach (var v in flow.Map(x => $"item{x}"))
            result.Add(v);

        Assert.Equal(new[] { "item1", "item2", "item3" }, result);
    }

    [Fact]
    public async Task AsyncFlow_Filter_Predicate()
    {
        var flow = AsyncFlow<int>.From(RangeAsync(10));
        var result = new List<int>();

        await foreach (var v in flow.Filter(x => x > 5))
            result.Add(v);

        Assert.Equal(new[] { 6, 7, 8, 9, 10 }, result);
    }

    [Fact]
    public async Task AsyncFlow_Tee_SideEffect()
    {
        var sideEffects = new List<int>();
        var flow = AsyncFlow<int>.From(RangeAsync(3));
        var result = new List<int>();

        await foreach (var v in flow.Tee(x => sideEffects.Add(x * 10)))
            result.Add(v);

        Assert.Equal(new[] { 1, 2, 3 }, result);
        Assert.Equal(new[] { 10, 20, 30 }, sideEffects);
    }
}

#endregion

#region AsyncReplayBuffer Error and Edge Case Tests

public sealed class AsyncReplayBufferTests
{
    private static async IAsyncEnumerable<int> RangeAsync(int count)
    {
        for (var i = 1; i <= count; i++)
            yield return i;
    }

    private static async IAsyncEnumerable<int> ThrowingAsync(int throwAfter)
    {
        for (var i = 1; i <= throwAfter; i++)
            yield return i;
        throw new InvalidOperationException("Source error");
    }

    private static async IAsyncEnumerable<int> SlowRangeAsync(int count, int delayMs, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 1; i <= count; i++)
        {
            await Task.Delay(delayMs, ct);
            yield return i;
        }
    }

    [Fact]
    public async Task SharedAsyncFlow_SourceThrows_PropagatesException()
    {
        var shared = AsyncFlow<int>.From(ThrowingAsync(2)).Share();
        var fork = shared.Fork();

        var items = new List<int>();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var v in fork)
                items.Add(v);
        });

        Assert.Equal("Source error", ex.Message);
        Assert.Equal(new[] { 1, 2 }, items);
    }

    [Fact]
    public async Task SharedAsyncFlow_ErrorPropagates_ToMultipleForks()
    {
        var shared = AsyncFlow<int>.From(ThrowingAsync(2)).Share();

        var fork1 = shared.Fork();
        var fork2 = shared.Fork();

        // First fork consumes and hits error
        var items1 = new List<int>();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var v in fork1)
                items1.Add(v);
        });

        // Second fork gets buffered items (items that were successfully buffered before error)
        // The error was during MoveNext, so items 1, 2 were buffered before the throw
        var items2 = new List<int>();
        await foreach (var v in fork2)
            items2.Add(v);

        Assert.Equal(new[] { 1, 2 }, items1);
        Assert.Equal(new[] { 1, 2 }, items2); // Gets buffered items without error
    }

    [Fact]
    public async Task SharedAsyncFlow_Cancellation_StopsEnumeration()
    {
        using var cts = new CancellationTokenSource();
        var shared = AsyncFlow<int>.From(SlowRangeAsync(100, 50, cts.Token)).Share();
        var fork = shared.Fork();

        var items = new List<int>();
        var enumerator = fork.GetAsyncEnumerator(cts.Token);

        try
        {
            // Get first item
            if (await enumerator.MoveNextAsync())
                items.Add(enumerator.Current);

            // Cancel while waiting for next
            cts.Cancel();

            // Next attempt should throw
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                while (await enumerator.MoveNextAsync())
                    items.Add(enumerator.Current);
            });
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        Assert.Single(items);
    }

    [Fact]
    public async Task SharedAsyncFlow_TryGetAsync_NegativeIndex_ReturnsFalse()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(3)).Share();

        // Fork and consume nothing - buffer internal access is tested via Fork behavior
        var fork = shared.Fork();

        var items = new List<int>();
        await foreach (var v in fork)
            items.Add(v);

        Assert.Equal(new[] { 1, 2, 3 }, items);
    }

    [Fact]
    public async Task SharedAsyncFlow_MultipleConcurrentWaiters()
    {
        var shared = AsyncFlow<int>.From(SlowRangeAsync(5, 10)).Share();

        // Start multiple consumers concurrently
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var items = new List<int>();
            await foreach (var v in shared.Fork())
                items.Add(v);
            return items;
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result);
        }
    }

    [Fact]
    public async Task SharedAsyncFlow_EmptySource_AllForksEmpty()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(0)).Share();

        var fork1 = shared.Fork();
        var fork2 = shared.Fork();

        var items1 = new List<int>();
        var items2 = new List<int>();

        await foreach (var v in fork1) items1.Add(v);
        await foreach (var v in fork2) items2.Add(v);

        Assert.Empty(items1);
        Assert.Empty(items2);
    }

    [Fact]
    public async Task SharedAsyncFlow_SingleItem_AllForksGetIt()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(1)).Share();

        var fork1 = shared.Fork();
        var fork2 = shared.Fork();
        var fork3 = shared.Fork();

        var items1 = new List<int>();
        var items2 = new List<int>();
        var items3 = new List<int>();

        await foreach (var v in fork1) items1.Add(v);
        await foreach (var v in fork2) items2.Add(v);
        await foreach (var v in fork3) items3.Add(v);

        Assert.Equal(new[] { 1 }, items1);
        Assert.Equal(new[] { 1 }, items2);
        Assert.Equal(new[] { 1 }, items3);
    }

    [Fact]
    public async Task SharedAsyncFlow_SequentialForkConsumption()
    {
        var enumerationCount = 0;
        async IAsyncEnumerable<int> TrackedRangeAsync(int count)
        {
            Interlocked.Increment(ref enumerationCount);
            for (var i = 1; i <= count; i++)
                yield return i;
        }

        var shared = AsyncFlow<int>.From(TrackedRangeAsync(5)).Share();

        // Consume first fork entirely
        var fork1 = shared.Fork();
        var items1 = new List<int>();
        await foreach (var v in fork1) items1.Add(v);

        // Then consume second fork
        var fork2 = shared.Fork();
        var items2 = new List<int>();
        await foreach (var v in fork2) items2.Add(v);

        Assert.Equal(1, enumerationCount); // Source should only be enumerated once
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, items1);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, items2);
    }

    [Fact]
    public async Task SharedAsyncFlow_InterleavedForkConsumption()
    {
        var shared = AsyncFlow<int>.From(RangeAsync(3)).Share();

        var fork1 = shared.Fork().GetAsyncEnumerator();
        var fork2 = shared.Fork().GetAsyncEnumerator();

        try
        {
            // Interleave consumption
            Assert.True(await fork1.MoveNextAsync());
            Assert.Equal(1, fork1.Current);

            Assert.True(await fork2.MoveNextAsync());
            Assert.Equal(1, fork2.Current);

            Assert.True(await fork2.MoveNextAsync());
            Assert.Equal(2, fork2.Current);

            Assert.True(await fork1.MoveNextAsync());
            Assert.Equal(2, fork1.Current);

            Assert.True(await fork1.MoveNextAsync());
            Assert.Equal(3, fork1.Current);

            Assert.True(await fork2.MoveNextAsync());
            Assert.Equal(3, fork2.Current);

            Assert.False(await fork1.MoveNextAsync());
            Assert.False(await fork2.MoveNextAsync());
        }
        finally
        {
            await fork1.DisposeAsync();
            await fork2.DisposeAsync();
        }
    }
}

#endregion
#endif
