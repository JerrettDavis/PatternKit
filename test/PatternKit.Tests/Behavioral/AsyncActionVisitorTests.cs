using PatternKit.Behavioral.Visitor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - AsyncActionVisitor Basics")]
public sealed class AsyncActionVisitorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("On<T>(Action<T>) executes for matching type")]
    [Fact]
    public Task AsyncActionVisitor_SyncOn_Executes()
        => Given("counters and visitor using sync On", () =>
            {
                var counters = new int[3];
                var v = AsyncActionVisitor<Node>
                    .Create()
                    .On<Add>(_ => Interlocked.Increment(ref counters[0]))
                    .Build();
                return (v, counters);
            })
           .When("visiting Add", VisitAdd)
           .Then("Add counter incremented", x => x.counters[0] == 1)
           .AssertPassed();

    private static async Task<(AsyncActionVisitor<Node> v, int[] counters)> VisitAdd((AsyncActionVisitor<Node> v, int[] counters) x)
    {
        await x.v.VisitAsync(new Add(new Number(1), new Number(2)));
        return x;
    }

    [Scenario("TryVisitAsync returns false when no default and no match")]
    [Fact]
    public Task AsyncActionVisitor_TryVisit_NoDefault()
        => Given("visitor with single handler", () =>
            AsyncActionVisitor<Node>.Create().On<Number>(_ => { }).Build())
           .When("TryVisit Neg", TryNeg)
           .Then("ok == false", ok => !ok)
           .AssertPassed();

    private static async Task<bool> TryNeg(AsyncActionVisitor<Node> v)
        => await v.TryVisitAsync(new Neg(new Number(0)));

    [Scenario("VisitAsync throws when no match and no default")]
    [Fact]
    public Task AsyncActionVisitor_Throws_When_NoMatch_NoDefault()
        => Given("visitor with only Number handler", () =>
            AsyncActionVisitor<Node>.Create().On<Number>(_ => { }).Build())
           .When("visiting Neg", ExpectInvalidOp)
           .Then("threw InvalidOperationException", threw => threw)
           .AssertPassed();

    private static async Task<bool> ExpectInvalidOp(AsyncActionVisitor<Node> v)
    {
        try
        {
            await v.VisitAsync(new Neg(new Number(1)));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    [Scenario("Default async handler executes for unmatched type")]
    [Fact]
    public Task AsyncActionVisitor_DefaultAsync_Executes()
        => Given("counters and visitor with async default", () =>
            {
                var counters = new int[3];
                var v = AsyncActionVisitor<Node>
                    .Create()
                    .On<Number>(_ => { Interlocked.Increment(ref counters[1]); })
                    .Default((_, _) => { Interlocked.Increment(ref counters[2]); return default; })
                    .Build();
                return (v, counters);
            })
           .When("visiting Neg", VisitNeg)
           .Then("default counter incremented", x => x.counters[2] == 1)
           .AssertPassed();

    [Scenario("Default sync action executes for unmatched type")]
    [Fact]
    public Task AsyncActionVisitor_DefaultSync_Executes()
        => Given("counters and visitor with sync default", () =>
            {
                var counters = new int[3];
                var v = AsyncActionVisitor<Node>
                    .Create()
                    .On<Number>(_ => { Interlocked.Increment(ref counters[1]); })
                    .Default(_ => { Interlocked.Increment(ref counters[2]); })
                    .Build();
                return (v, counters);
            })
           .When("visiting Neg", VisitNeg)
           .Then("default counter incremented", x => x.counters[2] == 1)
           .AssertPassed();

    private static async Task<(AsyncActionVisitor<Node> v, int[] counters)> VisitNeg((AsyncActionVisitor<Node> v, int[] counters) x)
    {
        await x.v.VisitAsync(new Neg(new Number(9)));
        return x;
    }

    [Scenario("TryVisitAsync returns true when default configured")]
    [Fact]
    public Task AsyncActionVisitor_TryVisit_With_Default_ReturnsTrue()
        => Given("visitor with default", () =>
            {
                var counters = new int[3];
                var v = AsyncActionVisitor<Node>
                    .Create()
                    .Default(_ => { Interlocked.Increment(ref counters[2]); })
                    .Build();
                return (v, counters);
            })
           .When("TryVisit Neg", TryVisitNegDefault)
           .Then("ok == true", r => r.ok)
           .And("default executed once", r => r.counters[2] == 1)
           .AssertPassed();

    private static async Task<(bool ok, int[] counters)> TryVisitNegDefault((AsyncActionVisitor<Node> v, int[] counters) x)
    {
        var ok = await x.v.TryVisitAsync(new Neg(new Number(0)));
        return (ok, x.counters);
    }
}
