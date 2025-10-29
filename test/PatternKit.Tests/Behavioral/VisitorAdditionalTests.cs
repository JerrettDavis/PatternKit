using PatternKit.Behavioral.Visitor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - Visitor additional scenarios")]
public sealed class VisitorAdditionalTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("Visit throws when no match and no default")]
    [Fact]
    public Task ResultVisitor_Throws_NoDefault()
        => Given("a visitor without default", () =>
            Visitor<Node, int>.Create().On<Number>(n => n.Value).Build())
           .When("visiting Neg", ExpectInvalidOp)
           .Then("threw InvalidOperationException", threw => threw)
           .AssertPassed();

    private static bool ExpectInvalidOp(Visitor<Node, int> v)
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

    [Scenario("TryVisit returns true and default result when default configured")]
    [Fact]
    public Task ResultVisitor_TryVisit_Default_ReturnsTrue()
        => Given("a visitor with default result", () =>
            Visitor<Node, string>.Create().Default(_ => "?").Build())
           .When("TryVisit Neg", v => { var ok = v.TryVisit(new Neg(new Number(0)), out var res); return (ok, res); })
           .Then("ok == true", x => x.ok)
           .And("result == ?", x => x.res == "?")
           .AssertPassed();

    [Scenario("Registration order matters: base before derived (result)")]
    [Fact]
    public Task ResultVisitor_Order_Matters()
        => Given("a visitor with base first", () =>
            Visitor<Node, string>.Create()
                .On<Node>(_ => "base")
                .On<Number>(_ => "number")
                .Build())
           .When("visiting Number", v => v.Visit(new Number(9)))
           .Then("base handled first", s => s == "base")
           .AssertPassed();
}

