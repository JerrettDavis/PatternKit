using PatternKit.Creational.Singleton;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Creational.Singleton;

[Feature("Creational - Singleton<T> (fluent, thread-safe)")]
public sealed class SingletonTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed class Thing
    {
        public int InitCount;
        public int Value;
    }

    private static Thing NewThing() => new() { Value = 1 };

    [Scenario("Lazy by default: factory not called until Instance; same instance returned")]
    [Fact]
    public Task Lazy_Default_Behavior()
        => Given("a singleton with factory and two init steps", () =>
                Singleton<Thing>.Create(NewThing)
                    .Init(t => t.InitCount++)
                    .Init(t => t.Value += 41)
                    .Build())
            .When("access Instance twice", s => (a: s.Instance, b: s.Instance))
            .Then("both references are same", t => ReferenceEquals(t.a, t.b))
            .And("init ran once and value is 42", t => t.a is { InitCount: 1, Value: 42 })
            .AssertPassed();

    [Scenario("Eager creation: instance created at Build, init runs immediately")]
    [Fact]
    public Task Eager_Creation_At_Build()
        => Given("an eager singleton", () =>
                Singleton<Thing>.Create(NewThing)
                    .Init(t => t.InitCount++)
                    .Eager()
                    .Build())
            .When("get Instance", s => s.Instance)
            .Then("init already ran once", t => t.InitCount == 1)
            .AssertPassed();

    [Scenario("Thread-safety: many concurrent reads result in a single factory invocation")]
    [Fact]
    public async Task Thread_Safety_Single_Factory_Invocation()
    {
        var calls = 0;
        var singleton = Singleton<Thing>.Create(() => { Interlocked.Increment(ref calls); return NewThing(); }).Build();

        await Given("32 concurrent Instance reads", () => singleton)
            .When("parallel read",
                (Func<Singleton<Thing>, ValueTask<Singleton<Thing>>>) (async s =>
                {
                    var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() => s.Instance)).ToArray();
                    await Task.WhenAll(tasks);
                    return s;
                }))
            .Then("factory called exactly once", _ => Volatile.Read(ref calls) == 1)
            .AssertPassed();
    }

    [Scenario("Null factory throws ArgumentNullException at Create")]
    [Fact]
    public Task Null_Factory_Throws()
        => Given("calling Create(null)", () => (Singleton<Thing>.Builder?)null)
            .When("invoke and capture", _ => Record.Exception(() => Singleton<Thing>.Create(null!)))
            .Then("ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Init composition preserves order")]
    [Fact]
    public Task Init_Order_Preserved()
        => Given("two init actions that modify value", () =>
                Singleton<Thing>.Create(() => new Thing { Value = 1 })
                    .Init(t => t.Value *= 10)
                    .Init(t => t.Value += 5)
                    .Build())
            .When("access Instance", s => s.Instance)
            .Then("(1*10)+5 = 15", t => t.Value == 15)
            .AssertPassed();

    [Scenario("Builder reuse creates independent singletons (distinct instances)")]
    [Fact]
    public Task Builder_Reuse_Yields_Distinct_Singletons()
        => Given("reuse the same builder to build S1 and S2", () =>
            {
                var b = Singleton<Thing>.Create(NewThing).Init(t => t.InitCount++);
                var s1 = b.Build();
                var s2 = b.Build();
                return (s1, s2);
            })
            .When("get both instances", t => (a: t.s1.Instance, b: t.s2.Instance))
            .Then("instances are not ReferenceEquals", t => !ReferenceEquals(t.a, t.b))
            .And("each was initialized once", t => t.a.InitCount == 1 && t.b.InitCount == 1)
            .AssertPassed();
}
