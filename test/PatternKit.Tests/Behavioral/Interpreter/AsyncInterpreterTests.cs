using PatternKit.Behavioral.Interpreter;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral;

[Feature("AsyncInterpreter<TContext,TResult> (async expression interpreter)")]
public sealed class AsyncInterpreterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Ctx(
        AsyncInterpreter<object?, double> Interp,
        double? Result = null,
        (bool success, double? result)? TryResult = null,
        Exception? Ex = null
    );

    private static Ctx Build_Calculator()
    {
        var interp = AsyncInterpreter.Create<object?, double>()
            .Terminal("number", token => double.Parse(token))
            .Binary("add", (left, right) => left + right)
            .Binary("mul", (left, right) => left * right)
            .Binary("sub", (left, right) => left - right)
            .Unary("neg", value => -value)
            .Build();

        return new Ctx(interp);
    }

    private static Ctx Build_AsyncLookup()
    {
        var prices = new Dictionary<string, double> { ["A"] = 10.0, ["B"] = 20.0, ["C"] = 30.0 };

        var interp = AsyncInterpreter.Create<object?, double>()
            .Terminal("price", async (sku, _, ct) =>
            {
                await Task.Delay(1, ct); // Simulate async lookup
                return prices.TryGetValue(sku, out var p) ? p : 0;
            })
            .Binary("add", (l, r) => l + r)
            .Build();

        return new Ctx(interp);
    }

    private static async Task<Ctx> InterpretAsync(Ctx c, IExpression expr, CancellationToken ct = default)
    {
        var result = await c.Interp.InterpretAsync(expr, null, ct);
        return c with { Result = result };
    }

    private static async Task<Ctx> TryInterpretAsync(Ctx c, IExpression expr, CancellationToken ct = default)
    {
        var result = await c.Interp.TryInterpretAsync(expr, null, ct);
        return c with { TryResult = result };
    }

    [Scenario("Sync arithmetic operations work")]
    [Fact]
    public async Task SyncArithmetic()
    {
        // (2 + 3) * 4 = 20
        var expr = new NonTerminalExpression("mul",
            new NonTerminalExpression("add",
                new TerminalExpression("number", "2"),
                new TerminalExpression("number", "3")),
            new TerminalExpression("number", "4"));

        await Given("a calculator async interpreter", Build_Calculator)
            .When("interpreting (2+3)*4", c => InterpretAsync(c, expr))
            .Then("returns 20", c => c.Result == 20.0)
            .AssertPassed();
    }

    [Scenario("Unary operations work")]
    [Fact]
    public async Task UnaryOperations()
    {
        // -(5 - 3) = -2
        var expr = new NonTerminalExpression("neg",
            new NonTerminalExpression("sub",
                new TerminalExpression("number", "5"),
                new TerminalExpression("number", "3")));

        await Given("a calculator async interpreter", Build_Calculator)
            .When("interpreting -(5-3)", c => InterpretAsync(c, expr))
            .Then("returns -2", c => c.Result == -2.0)
            .AssertPassed();
    }

    [Scenario("Async terminal lookups work")]
    [Fact]
    public async Task AsyncLookups()
    {
        // price(A) + price(B) = 10 + 20 = 30
        var expr = new NonTerminalExpression("add",
            new TerminalExpression("price", "A"),
            new TerminalExpression("price", "B"));

        await Given("an async price lookup interpreter", Build_AsyncLookup)
            .When("interpreting price(A)+price(B)", c => InterpretAsync(c, expr))
            .Then("returns 30", c => c.Result == 30.0)
            .AssertPassed();
    }

    [Scenario("TryInterpretAsync returns success tuple")]
    [Fact]
    public async Task TryInterpretSuccess()
    {
        var expr = new TerminalExpression("number", "42");

        await Given("a calculator async interpreter", Build_Calculator)
            .When("try-interpreting 42", c => TryInterpretAsync(c, expr))
            .Then("returns success with 42", c => c.TryResult!.Value.success && c.TryResult.Value.result == 42.0)
            .AssertPassed();
    }

    [Scenario("HasTerminal and HasNonTerminal work")]
    [Fact]
    public async Task HasMethods()
    {
        await Given("a calculator async interpreter", Build_Calculator)
            .Then("HasTerminal('number') is true", c => c.Interp.HasTerminal("number"))
            .And("HasTerminal('unknown') is false", c => !c.Interp.HasTerminal("unknown"))
            .And("HasNonTerminal('add') is true", c => c.Interp.HasNonTerminal("add"))
            .And("HasNonTerminal('unknown') is false", c => !c.Interp.HasNonTerminal("unknown"))
            .AssertPassed();
    }
}
