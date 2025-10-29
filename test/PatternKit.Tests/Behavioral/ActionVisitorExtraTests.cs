using PatternKit.Behavioral.Visitor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - ActionVisitor edge cases")]
public sealed class ActionVisitorExtraTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("Visit throws when no match and no default")]
    [Fact]
    public Task ActionVisitor_Throws_NoDefault()
        => Given("visitor without default", () =>
            ActionVisitor<Node>.Create().On<Number>(_ => { }).Build())
           .When("visiting Neg", ExpectInvalidOp)
           .Then("threw InvalidOperationException", threw => threw)
           .AssertPassed();

    private static bool ExpectInvalidOp(ActionVisitor<Node> v)
    {
        try
        {
            v.Visit(new Neg(new Number(1)));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    [Scenario("TryVisit returns true when default configured")]
    [Fact]
    public Task ActionVisitor_TryVisit_Default_ReturnsTrue()
        => Given("visitor with default", () =>
            ActionVisitor<Node>.Create().Default(_ => { }).Build())
           .When("TryVisit Neg", v => v.TryVisit(new Neg(new Number(2))))
           .Then("ok == true", ok => ok)
           .AssertPassed();

    [Scenario("Registration order matters: base before derived")]
    [Fact]
    public Task ActionVisitor_Order_Matters()
        => Given("counters and visitor with base first", () =>
            {
                var counters = new int[3]; // [0]=Add, [1]=Number, [2]=Node
                var v = ActionVisitor<Node>
                    .Create()
                    .On<Node>(_ => Interlocked.Increment(ref counters[2])) // base first
                    .On<Number>(_ => Interlocked.Increment(ref counters[1])) // derived later (won't hit)
                    .Build();
                return (v, counters);
            })
           .When("visiting Number", x => { x.v.Visit(new Number(5)); return x.counters; })
           .Then("base handler executed", c => c[2] == 1)
           .And("derived handler did not execute", c => c[1] == 0)
           .AssertPassed();
}

