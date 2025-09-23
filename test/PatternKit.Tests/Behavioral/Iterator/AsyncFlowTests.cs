#if !NETSTANDARD2_0
using PatternKit.Behavioral.Iterator;
using TinyBDD;
using TinyBDD.Assertions;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Iterator;

[Feature("AsyncFlow<T>: async functional pipeline with share/fork/branch")] 
public sealed class AsyncFlowTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static async IAsyncEnumerable<int> RangeAsync(int count, int delayMs = 0)
    {
        for (int i = 1; i <= count; i++)
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

    private static async Task<(PatternKit.Common.Option<int> Some, PatternKit.Common.Option<int> None)> Firsts((AsyncFlow<int> NonEmpty, AsyncFlow<int> Empty) flows)
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
#endif
