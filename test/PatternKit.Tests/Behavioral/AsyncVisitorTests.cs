using PatternKit.Behavioral.Visitor;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("Behavioral - AsyncVisitor Basics")]
public sealed class AsyncVisitorTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private abstract record Node;
    private sealed record Number(int Value) : Node;
    private sealed record Add(Node Left, Node Right) : Node;
    private sealed record Neg(Node Inner) : Node;

    [Scenario("AsyncVisitor constant overload and default handler")]
    [Fact]
    public Task AsyncVisitor_Constant_And_Default()
        => Given("a visitor with constants and default", () =>
            AsyncVisitor<Node, int>
                .Create()
                .On<Add>(42)
                .On<Number>(1)
                .Default((_, _) => new ValueTask<int>(9))
                .Build())
           .When("visiting Add, Number, Neg", VisitThree)
           .Then("Add -> 42", r => r.a == 42)
           .And("Number -> 1", r => r.b == 1)
           .And("Neg -> 9 (default)", r => r.c == 9)
           .AssertPassed();

    private static async Task<(int a, int b, int c)> VisitThree(AsyncVisitor<Node, int> v)
    {
        var a = await v.VisitAsync(new Add(new Number(1), new Number(2)));
        var b = await v.VisitAsync(new Number(7));
        var c = await v.VisitAsync(new Neg(new Number(0)));
        return (a, b, c);
    }

    [Scenario("TryVisitAsync returns false and default when no default configured")]
    [Fact]
    public Task AsyncVisitor_TryVisit_NoDefault()
        => Given("a visitor without default", () =>
            AsyncVisitor<Node, string>.Create()
                .On<Number>((n, _) => new ValueTask<string>(n.Value.ToString()))
                .Build())
           .When("TryVisit Neg", TryNeg)
           .Then("ok == false", r => r.ok == false)
           .And("result is null", r => r.result is null)
           .AssertPassed();

    private static async Task<(bool ok, string? result)> TryNeg(AsyncVisitor<Node, string> v)
        => await v.TryVisitAsync(new Neg(new Number(3)));

    [Scenario("VisitAsync throws when no match and no default")]
    [Fact]
    public Task AsyncVisitor_Throws_When_NoMatch_NoDefault()
        => Given("a visitor without default", () =>
            AsyncVisitor<Node, int>.Create().On<Number>((_, _) => new ValueTask<int>(3)).Build())
           .When("calling VisitAsync on Neg", ExpectInvalidOp)
           .Then("threw InvalidOperationException", threw => threw)
           .AssertPassed();

    private static async Task<bool> ExpectInvalidOp(AsyncVisitor<Node, int> v)
    {
        try
        {
            await v.VisitAsync(new Neg(new Number(0)));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    [Scenario("Cancellation is observed by async handler")]
    [Fact]
    public Task AsyncVisitor_Cancellation_Propagates()
        => Given("a visitor with cancellable handler", () =>
            AsyncVisitor<Node, int>.Create()
                .On<Number>((n, ct) => { ct.ThrowIfCancellationRequested(); return new ValueTask<int>(n.Value); })
                .Default((_, _) => new ValueTask<int>(-1))
                .Build())
           .When("invoking with canceled token", CallWithCanceledToken)
           .Then("throws OperationCanceledException", threw => threw)
           .AssertPassed();

    private static async Task<bool> CallWithCanceledToken(AsyncVisitor<Node, int> v)
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        try
        {
            await v.VisitAsync(new Number(5), cts.Token);
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }

    [Scenario("Sync default overload is honored")]
    [Fact]
    public Task AsyncVisitor_DefaultSyncOverload_Works()
        => Given("visitor with sync default", () =>
            AsyncVisitor<Node, int>.Create()
                .On<Number>((_, _) => new ValueTask<int>(1))
                .Default(_ => -5)
                .Build())
           .When("visiting Neg", VisitNegDefaultSync)
           .Then("result is -5", r => r == -5)
           .AssertPassed();

    private static async Task<int> VisitNegDefaultSync(AsyncVisitor<Node, int> v)
        => await v.VisitAsync(new Neg(new Number(1)));
}
