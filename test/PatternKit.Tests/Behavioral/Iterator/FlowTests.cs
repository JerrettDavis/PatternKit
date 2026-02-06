using PatternKit.Behavioral.Iterator;
using TinyBDD;
using TinyBDD.Assertions;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Iterator;

[Feature("Flow<T>: functional iterator pipeline with share/fork/branch")]
public sealed class FlowTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed class CountingEnumerable : IEnumerable<int>
    {
        private readonly int _count;
        public int MoveNextCalls { get; private set; }
        public CountingEnumerable(int count) => _count = count;
        public IEnumerator<int> GetEnumerator()
        {
            for (var i = 0; i < _count; i++)
            {
                MoveNextCalls++;
                yield return i + 1; // 1..n
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Scenario("Map / Filter / FlatMap / Tee composition produces expected sequence")]
    [Fact]
    public Task Composition()
        => Given("flow over 1..5", () => Flow<int>.From(Enumerable.Range(1, 5)))
            .When("map *2, filter even, flatMap duplicates, tee collects", f =>
            {
                var log = new List<int>();
                var result = f.Map(x => x * 2)      // 2,4,6,8,10
                               .Filter(x => x % 4 == 0) // 4,8
                               .FlatMap(x => new[] { x, x }) // 4,4,8,8
                               .Tee(log.Add)
                               .ToList();
                return (result, log);
            })
            .Then("result is 4,4,8,8", t => Expect.For(string.Join(',', t.result)).ToBe("4,4,8,8"))
            .And("tee captured same sequence", t => Expect.For(string.Join(',', t.log)).ToBe("4,4,8,8"))
            .AssertPassed();

    [Scenario("Share + two forks enumerate only once upstream")]
    [Fact]
    public Task ShareForkSingleEnumeration()
        => Given("counting source 1..5", () => new CountingEnumerable(5))
            .When("shared and two forks consumed", src =>
            {
                var shared = Flow<int>.From(src).Map(x => x + 0).Share();
                var fork1 = shared.Fork().Map(x => x * 10).ToList();
                var fork2 = shared.Fork().Filter(x => x % 2 == 1).ToList();
                return (src, fork1, fork2);
            })
            .Then("source MoveNextCalls == 5 (single pass)", t => t.src.MoveNextCalls == 5)
            .And("fork1 is 10,20,30,40,50", t => string.Join(',', t.fork1) == "10,20,30,40,50")
            .And("fork2 is 1,3,5", t => string.Join(',', t.fork2) == "1,3,5")
            .AssertPassed();

    [Scenario("Branch splits into true/false flows")]
    [Fact]
    public Task Branching()
        => Given("shared flow over 1..6", () => Flow<int>.From(Enumerable.Range(1, 6)).Share())
            .When("branch is even predicate", sf => sf.Branch(x => x % 2 == 0))
            .Then("true branch has evens", b => string.Join(',', b.True.ToList()) == "2,4,6")
            .And("false branch has odds", b => string.Join(',', b.False.ToList()) == "1,3,5")
            .AssertPassed();

    [Scenario("FirstOption returns Some for non-empty flow and None for empty")]
    [Fact]
    public Task FirstOption()
        => Given("flow over numbers", () => Flow<int>.From(new[] { 7, 8, 9 }))
            .When("first option extracted", f => (Some: f.FirstOption(), None: Flow<int>.From(Array.Empty<int>()).FirstOption()))
            .Then("Some has value 7", r => r.Some.HasValue && r.Some.ValueOrDefault == 7)
            .And("None has no value", r => !r.None.HasValue)
            .AssertPassed();
}

#region Additional Flow Tests

public sealed class FlowBuilderTests
{
    [Fact]
    public void Flow_From_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Flow<int>.From(null!));
    }

    [Fact]
    public void Flow_Map_Null_Throws()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3 });
        Assert.Throws<ArgumentNullException>(() => flow.Map<int>(null!));
    }

    [Fact]
    public void Flow_Filter_Null_Throws()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3 });
        Assert.Throws<ArgumentNullException>(() => flow.Filter(null!));
    }

    [Fact]
    public void Flow_FlatMap_Null_Throws()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3 });
        Assert.Throws<ArgumentNullException>(() => flow.FlatMap<int>(null!));
    }

    [Fact]
    public void Flow_Tee_Null_Throws()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3 });
        Assert.Throws<ArgumentNullException>(() => flow.Tee(null!));
    }

    [Fact]
    public void SharedFlow_Branch_Null_Throws()
    {
        var shared = Flow<int>.From(new[] { 1, 2, 3 }).Share();
        Assert.Throws<ArgumentNullException>(() => shared.Branch(null!));
    }

    [Fact]
    public void SharedFlow_Fork_Multiple()
    {
        var shared = Flow<int>.From(new[] { 1, 2, 3 }).Share();
        var forks = shared.Fork(3);

        Assert.Equal(3, forks.Length);

        var results = forks.Select(f => f.ToList()).ToArray();
        foreach (var result in results)
        {
            Assert.Equal(new[] { 1, 2, 3 }, result);
        }
    }

    [Fact]
    public void SharedFlow_Fork_Zero_Throws()
    {
        var shared = Flow<int>.From(new[] { 1, 2, 3 }).Share();
        Assert.Throws<ArgumentOutOfRangeException>(() => shared.Fork(0));
    }

    [Fact]
    public void SharedFlow_Fork_Negative_Throws()
    {
        var shared = Flow<int>.From(new[] { 1, 2, 3 }).Share();
        Assert.Throws<ArgumentOutOfRangeException>(() => shared.Fork(-1));
    }

    [Fact]
    public void SharedFlow_Map()
    {
        var shared = Flow<int>.From(new[] { 1, 2, 3 }).Share();
        var result = shared.Map(x => x * 10).ToList();

        Assert.Equal(new[] { 10, 20, 30 }, result);
    }

    [Fact]
    public void SharedFlow_Filter()
    {
        var shared = Flow<int>.From(new[] { 1, 2, 3, 4, 5 }).Share();
        var result = shared.Filter(x => x % 2 == 0).ToList();

        Assert.Equal(new[] { 2, 4 }, result);
    }

    [Fact]
    public void SharedFlow_AsFlow()
    {
        var shared = Flow<int>.From(new[] { 1, 2, 3 }).Share();
        var flow = shared.AsFlow();
        var result = flow.ToList();

        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void FlowExtensions_Fold()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3, 4, 5 });
        var sum = flow.Fold(0, (acc, x) => acc + x);

        Assert.Equal(15, sum);
    }

    [Fact]
    public void FlowExtensions_Fold_Null_Flow_Throws()
    {
        Flow<int>? flow = null;
        Assert.Throws<ArgumentNullException>(() => flow!.Fold(0, (acc, x) => acc + x));
    }

    [Fact]
    public void FlowExtensions_Fold_Null_Folder_Throws()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3 });
        Assert.Throws<ArgumentNullException>(() => flow.Fold<int, int>(0, null!));
    }

    [Fact]
    public void SharedFlow_Fold()
    {
        var shared = Flow<int>.From(new[] { 1, 2, 3, 4, 5 }).Share();
        var sum = shared.Fold(0, (acc, x) => acc + x);

        Assert.Equal(15, sum);
    }

    [Fact]
    public void FlowExtensions_FirstOrDefault_Found()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3, 4, 5 });
        var result = flow.FirstOrDefault(x => x > 3);

        Assert.Equal(4, result);
    }

    [Fact]
    public void FlowExtensions_FirstOrDefault_NotFound()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3 });
        var result = flow.FirstOrDefault(x => x > 10);

        Assert.Equal(default, result);
    }

    [Fact]
    public void FlowExtensions_FirstOrDefault_NoPredicate()
    {
        var flow = Flow<int>.From(new[] { 5, 6, 7 });
        var result = flow.FirstOrDefault();

        Assert.Equal(5, result);
    }

    [Fact]
    public void FlowExtensions_FirstOrDefault_Null_Throws()
    {
        Flow<int>? flow = null;
        Assert.Throws<ArgumentNullException>(() => flow!.FirstOrDefault());
    }

    [Fact]
    public void FlowExtensions_FirstOption_Null_Throws()
    {
        Flow<int>? flow = null;
        Assert.Throws<ArgumentNullException>(() => flow!.FirstOption());
    }

    [Fact]
    public void Flow_Deferred_Execution()
    {
        var evaluated = false;
        IEnumerable<int> Source()
        {
            evaluated = true;
            yield return 1;
        }

        var flow = Flow<int>.From(Source());
        Assert.False(evaluated); // Should be deferred

        var list = flow.ToList();
        Assert.True(evaluated); // Now evaluated
        Assert.Equal(new[] { 1 }, list);
    }

    [Fact]
    public void Flow_Empty_Source()
    {
        var flow = Flow<int>.From(Array.Empty<int>());
        var result = flow.Map(x => x * 2).Filter(x => x > 0).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void SharedFlow_Multiple_Enumerations_Share_Data()
    {
        var callCount = 0;
        IEnumerable<int> Source()
        {
            callCount++;
            yield return 1;
            yield return 2;
            yield return 3;
        }

        var shared = Flow<int>.From(Source()).Share();

        var list1 = shared.Fork().ToList();
        var list2 = shared.Fork().ToList();
        var list3 = shared.Fork().ToList();

        // Source should only be enumerated once
        Assert.Equal(1, callCount);
        Assert.Equal(new[] { 1, 2, 3 }, list1);
        Assert.Equal(new[] { 1, 2, 3 }, list2);
        Assert.Equal(new[] { 1, 2, 3 }, list3);
    }

    [Fact]
    public void Flow_Chained_Operations()
    {
        var flow = Flow<int>.From(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
        var result = flow
            .Filter(x => x % 2 == 0)    // 2, 4, 6, 8, 10
            .Map(x => x * x)            // 4, 16, 36, 64, 100
            .Filter(x => x < 50)        // 4, 16, 36
            .ToList();

        Assert.Equal(new[] { 4, 16, 36 }, result);
    }
}

#endregion

