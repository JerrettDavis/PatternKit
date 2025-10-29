using PatternKit.Behavioral.Visitor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - Visitor<TBase,TResult> and ActionVisitor<TBase>")]
public sealed class VisitorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("Result visitor dispatches by runtime type; order matters; default used")]
    [Fact]
    public Task ResultVisitor_Dispatch_And_Default()
        => Given("a result visitor for Node", () =>
            {
                var v = Visitor<Node, string>
                    .Create()
                    .On<Add>(_ => "+")
                    .On<Number>(n => $"#{n.Value}")
                    .Default(_ => "?")
                    .Build();
                return v;
            })
           .When("visiting three nodes", v => (
               a: v.Visit(new Add(new Number(1), new Number(2))),
               b: v.Visit(new Number(7)),
               c: v.Visit(new Neg(new Number(1))) // no match, hits default
           ))
           .Then("Add -> +", r => r.a == "+")
           .And("Number -> #7", r => r.b == "#7")
           .And("Neg -> ? (default)", r => r.c == "?")
           .AssertPassed();

    [Scenario("TryVisit returns false when no handler and no default")]
    [Fact]
    public Task ResultVisitor_TryVisit_NoDefault()
        => Given("a visitor without default", () =>
            Visitor<Node, string>.Create().On<Number>(n => n.Value.ToString()).Build())
           .When("TryVisit Neg", v => v.TryVisit(new Neg(new Number(3)), out var res) ? res : null)
           .Then("returns null (no match)", r => r is null)
           .AssertPassed();

    [Scenario("Action visitor executes side effects by runtime type")]
    [Fact]
    public Task ActionVisitor_Dispatch()
        => Given("a counter and action visitor", () =>
            {
                var counters = new int[3]; // [0]=Add, [1]=Number, [2]=Default
                var v = ActionVisitor<Node>
                    .Create()
                    .On<Add>(_ => Interlocked.Increment(ref counters[0]))
                    .On<Number>(_ => Interlocked.Increment(ref counters[1]))
                    .Default(_ => Interlocked.Increment(ref counters[2]))
                    .Build();
                return (v, counters);
            })
           .When("visit mixed nodes", x =>
            {
                x.v.Visit(new Add(new Number(1), new Number(2)));
                x.v.Visit(new Number(5));
                x.v.Visit(new Neg(new Number(9))); // default
                return x.counters;
            })
           .Then("Add handled once", c => c[0] == 1)
           .And("Number handled once", c => c[1] == 1)
           .And("Default handled once", c => c[2] == 1)
           .AssertPassed();
}
