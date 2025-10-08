using PatternKit.Structural.Flyweight;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural.Flyweight;

[Feature("Structural - Flyweight<TKey,TValue> (intrinsic sharing / lazy creation)")]
public sealed class FlyweightTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Preloaded key returns same instance")]
    [Fact]
    public Task Preloaded_Key_Reused()
        => Given("flyweight with preloaded entry", () =>
                Flyweight<string, object>.Create()
                    .WithFactory(static _ => new object())
                    .Preload("x", new object())
                    .Build())
            .When("get same key twice", fw => (a: fw.Get("x"), b: fw.Get("x")))
            .Then("instances identical", r => ReferenceEquals(r.a, r.b))
            .AssertPassed();

    [Scenario("Factory called once per unique key")]
    [Fact]
    public Task Factory_Called_Once_Per_Key()
        => Given("flyweight with counting factory", () =>
            {
                var counts = new Dictionary<char, int>();
                var fw = Flyweight<char, string>.Create()
                    .WithFactory(c =>
                    {
                        counts[c] = counts.TryGetValue(c, out var v) ? v + 1 : 1;
                        return c.ToString();
                    })
                    .Build();
                return (fw, counts);
            })
            .When("get 'A' thrice and 'B' twice", ctx =>
            {
                ctx.fw.Get('A');
                ctx.fw.Get('A');
                ctx.fw.Get('A');
                ctx.fw.Get('B');
                ctx.fw.Get('B');
                return ctx.counts;
            })
            .Then("each key created exactly once", counts => counts['A'] == 1 && counts['B'] == 1)
            .AssertPassed();

    [Scenario("TryGetExisting does not create new value")]
    [Fact]
    public Task TryGetExisting_Does_Not_Create()
        => Given("empty flyweight", () =>
                Flyweight<int, string>.Create().WithFactory(static i => i.ToString()).Build())
            .When("TryGetExisting before Get", fw => fw.TryGetExisting(5, out var _))
            .Then("returns false", exists => exists == false)
            .AssertPassed();

    [Scenario("Case-insensitive comparer merges keys")]
    [Fact]
    public Task Comparer_Merges_Keys()
        => Given("flyweight with case-insensitive keys", () =>
            {
                var created = 0;
                var fw = Flyweight<string, string>.Create()
                    .WithComparer(StringComparer.OrdinalIgnoreCase)
                    .WithFactory(s =>
                    {
                        created++;
                        return s.ToUpperInvariant();
                    })
                    .Build();
                return (fw, createdRef: new[] { created });
            })
            .When("get 'hello' and 'HELLO'", ctx =>
            {
                var a = ctx.fw.Get("hello");
                var b = ctx.fw.Get("HELLO");
                return (a, b, ctx.fw.Count);
            })
            .Then("same reference reused", r => ReferenceEquals(r.a, r.b))
            .And("single count entry", r => r.Count == 1)
            .AssertPassed();

    [Scenario("Thread-safe single creation under concurrency")]
    [Fact]
    public Task Thread_Safe_Single_Creation()
        => Given("flyweight with slow factory", () =>
            {
                var calls = 0;
                var fw = Flyweight<int, object>.Create()
                    .WithFactory(_ =>
                    {
                        Interlocked.Increment(ref calls);
                        Thread.Sleep(10);
                        return new object();
                    })
                    .Build();
                return (fw, callsRef: new int[1], callsCounter: (Func<int>)(() => calls));
            })
            .When("parallel gets for same key", ctx =>
            {
                var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(() => ctx.fw.Get(42))).ToArray();
                var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
                return (results, calls: ctx.callsCounter());
            })
            .Then("all references identical", r => r.results.All(o => ReferenceEquals(o, r.results[0])))
            .And("factory called once", r => r.calls == 1)
            .AssertPassed();

    [Scenario("Missing factory throws on Build")]
    [Fact]
    public Task Missing_Factory_Throws()
        => Given("builder without factory", Flyweight<int, int>.Create)
            .When("build invoked", b => Record.Exception(b.Build))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Factory returning null throws")] // Reference type only meaningful
    [Fact]
    public Task Null_Factory_Throws()
        => Given("flyweight with null-returning factory", () =>
            {
                var fw = Flyweight<int, string>.Create().WithFactory(_ => null!); // will throw on first Get
                return fw.Build();
            })
            .When("Get invoked", fw => Record.Exception(() => fw.Get(1)))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    // Additional scenarios

    [Scenario("Value type keys and values work")]
    [Fact]
    public Task Value_Types_Work()
        => Given("int->int doubling flyweight", () =>
                Flyweight<int, int>.Create().WithFactory(static i => i * 2).Build())
            .When("get 5 and 10", fw => (a: fw.Get(5), b: fw.Get(10)))
            .Then("results correct", r => r is { a: 10, b: 20 })
            .AssertPassed();

    [Scenario("Count reflects unique keys")]
    [Fact]
    public Task Count_Reflects_Uniques()
        => Given("flyweight", () => Flyweight<string, string>.Create().WithFactory(s => s).Build())
            .When("request three unique keys", fw =>
            {
                fw.Get("a");
                fw.Get("b");
                fw.Get("c");
                return fw.Count;
            })
            .Then("count is 3", c => c == 3)
            .AssertPassed();

    [Scenario("TryGetExisting true after Get")]
    [Fact]
    public Task TryGetExisting_After_Get()
        => Given("flyweight", () => Flyweight<string, string>.Create().WithFactory(s => s).Build())
            .When("get then try existing", fw =>
            {
                fw.Get("x");
                return fw.TryGetExisting("x", out var v) && v == "x";
            })
            .Then("returns true", b => b)
            .AssertPassed();

    [Scenario("Duplicate preload last wins")]
    [Fact]
    public Task Duplicate_Preload_Last_Wins()
        => Given("builder with duplicate preload", () =>
                Flyweight<string, string>.Create()
                    .Preload("k", "v1")
                    .Preload("k", "v2")
                    .WithFactory(s => s)
                    .Build())
            .When("get key", fw => fw.Get("k"))
            .Then("value is last", v => v == "v2")
            .AssertPassed();

    [Scenario("Snapshot returns copy")]
    [Fact]
    public Task Snapshot_Returns_Copy()
        => Given("flyweight with entries", () =>
            {
                var fw = Flyweight<int, string>.Create().WithFactory(i => i.ToString()).Build();
                fw.Get(1);
                fw.Get(2);
                return fw;
            })
            .When("take snapshot", fw => fw.Snapshot())
            .Then("contains two keys", snap => snap.Count == 2)
            .AssertPassed();
}