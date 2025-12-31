using PatternKit.Behavioral.TypeDispatcher;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - TypeDispatcher<TBase,TResult> and ActionTypeDispatcher<TBase>")]
public sealed class TypeDispatcherTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("Result dispatcher dispatches by runtime type; order matters; default used")]
    [Fact]
    public Task TypeDispatcher_Dispatch_And_Default()
        => Given("a type dispatcher for Node", () =>
            {
                var d = TypeDispatcher<Node, string>
                    .Create()
                    .On<Add>(_ => "+")
                    .On<Number>(n => $"#{n.Value}")
                    .Default(_ => "?")
                    .Build();
                return d;
            })
           .When("dispatching three nodes", d => (
               a: d.Dispatch(new Add(new Number(1), new Number(2))),
               b: d.Dispatch(new Number(7)),
               c: d.Dispatch(new Neg(new Number(1))) // no match, hits default
           ))
           .Then("Add -> +", r => r.a == "+")
           .And("Number -> #7", r => r.b == "#7")
           .And("Neg -> ? (default)", r => r.c == "?")
           .AssertPassed();

    [Scenario("TryDispatch returns false when no handler and no default")]
    [Fact]
    public Task TypeDispatcher_TryDispatch_NoDefault()
        => Given("a dispatcher without default", () =>
            TypeDispatcher<Node, string>.Create().On<Number>(n => n.Value.ToString()).Build())
           .When("TryDispatch Neg", d => d.TryDispatch(new Neg(new Number(3)), out var res) ? res : null)
           .Then("returns null (no match)", r => r is null)
           .AssertPassed();

    [Scenario("Dispatch throws when no handler and no default")]
    [Fact]
    public Task TypeDispatcher_Dispatch_Throws_NoDefault()
        => Given("a dispatcher without default", () =>
            TypeDispatcher<Node, string>.Create().On<Number>(n => n.Value.ToString()).Build())
           .When("Dispatch Neg", d =>
            {
                try
                {
                    d.Dispatch(new Neg(new Number(3)));
                    return false; // should have thrown
                }
                catch (InvalidOperationException)
                {
                    return true;
                }
            })
           .Then("throws InvalidOperationException", threw => threw)
           .AssertPassed();

    [Scenario("Action dispatcher executes side effects by runtime type")]
    [Fact]
    public Task ActionTypeDispatcher_Dispatch()
        => Given("a counter and action dispatcher", () =>
            {
                var counters = new int[3]; // [0]=Add, [1]=Number, [2]=Default
                var d = ActionTypeDispatcher<Node>
                    .Create()
                    .On<Add>(_ => Interlocked.Increment(ref counters[0]))
                    .On<Number>(_ => Interlocked.Increment(ref counters[1]))
                    .Default(_ => Interlocked.Increment(ref counters[2]))
                    .Build();
                return (d, counters);
            })
           .When("dispatch mixed nodes", x =>
            {
                x.d.Dispatch(new Add(new Number(1), new Number(2)));
                x.d.Dispatch(new Number(5));
                x.d.Dispatch(new Neg(new Number(9))); // default
                return x.counters;
            })
           .Then("Add handled once", c => c[0] == 1)
           .And("Number handled once", c => c[1] == 1)
           .And("Default handled once", c => c[2] == 1)
           .AssertPassed();

    [Scenario("TryDispatch action returns false when no handler and no default")]
    [Fact]
    public Task ActionTypeDispatcher_TryDispatch_NoDefault()
        => Given("a dispatcher without default", () =>
            {
                var counter = 0;
                var d = ActionTypeDispatcher<Node>
                    .Create()
                    .On<Number>(_ => counter++)
                    .Build();
                return (d, counter);
            })
           .When("TryDispatch Neg", x => x.d.TryDispatch(new Neg(new Number(3))))
           .Then("returns false", ok => !ok)
           .AssertPassed();

    [Scenario("First-match-wins: more specific types should be registered first")]
    [Fact]
    public Task TypeDispatcher_FirstMatchWins()
        => Given("a dispatcher with base type registered before specific type", () =>
            TypeDispatcher<Node, string>
                .Create()
                .On<Node>(_ => "node")      // base type first - will match everything!
                .On<Number>(n => "number")  // this will never match
                .Build())
           .When("dispatching a Number", d => d.Dispatch(new Number(42)))
           .Then("matches base type handler (first-match-wins)", r => r == "node")
           .AssertPassed();

    [Scenario("Constant result handlers work correctly")]
    [Fact]
    public Task TypeDispatcher_ConstantResult()
        => Given("a dispatcher with constant result handlers", () =>
            TypeDispatcher<Node, int>
                .Create()
                .On<Add>(100)
                .On<Number>(n => n.Value)
                .Default(_ => 0)
                .Build())
           .When("dispatching nodes", d => (
               add: d.Dispatch(new Add(new Number(1), new Number(2))),
               num: d.Dispatch(new Number(42)),
               neg: d.Dispatch(new Neg(new Number(1)))
           ))
           .Then("Add returns constant 100", r => r.add == 100)
           .And("Number returns value", r => r.num == 42)
           .And("Neg returns default 0", r => r.neg == 0)
           .AssertPassed();
}
