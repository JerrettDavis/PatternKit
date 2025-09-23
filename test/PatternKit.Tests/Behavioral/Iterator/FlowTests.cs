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
            for (int i = 0; i < _count; i++)
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
        => Given("flow over 1..5", () => Flow<int>.From(Enumerable.Range(1,5)))
            .When("map *2, filter even, flatMap duplicates, tee collects", f =>
            {
                var log = new List<int>();
                var result = f.Map(x => x * 2)      // 2,4,6,8,10
                               .Filter(x => x % 4 == 0) // 4,8
                               .FlatMap(x => new[]{x, x}) // 4,4,8,8
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
        => Given("shared flow over 1..6", () => Flow<int>.From(Enumerable.Range(1,6)).Share())
            .When("branch is even predicate", sf => sf.Branch(x => x % 2 == 0))
            .Then("true branch has evens", b => string.Join(',', b.True.ToList()) == "2,4,6")
            .And("false branch has odds", b => string.Join(',', b.False.ToList()) == "1,3,5")
            .AssertPassed();

    [Scenario("FirstOption returns Some for non-empty flow and None for empty")]
    [Fact]
    public Task FirstOption()
        => Given("flow over numbers", () => Flow<int>.From(new[]{7,8,9}))
            .When("first option extracted", f => (Some:f.FirstOption(), None: Flow<int>.From(Array.Empty<int>()).FirstOption()))
            .Then("Some has value 7", r => r.Some.HasValue && r.Some.ValueOrDefault == 7)
            .And("None has no value", r => !r.None.HasValue)
            .AssertPassed();
}

