using PatternKit.Examples.Generators.Strategies;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

// OrderRouter, ScoreLabeler, IntParser

namespace PatternKit.Examples.Tests.Generators;

[Feature("Generated strategies (Action / Result / Try)")]
public sealed class StrategySpecsTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // -------- Helpers --------------------------------------------------------

    private static (OrderRouter Router, List<string> Log) BuildRouterWithLog()
    {
        var log = new List<string>();
        var router = OrderRouter.Create()
            .When((in c) => char.IsLetter(c)).Then((in c) => log.Add($"L:{c}"))
            .When((in c) => char.IsDigit(c)).Then((in c) => log.Add($"D:{c}"))
            .Default((in c) => log.Add($"O:{c}"))
            .Build();
        return (router, log);
    }

    private static (OrderRouter Router, List<string> Log) Exec(in (OrderRouter Router, List<string> Log) s, char c)
    {
        s.Router.Execute(c);
        return s;
    }

    private static bool LogIs(in (OrderRouter Router, List<string> Log) s, params string[] expected)
        => s.Log.Count == expected.Length && s.Log.Select((v, i) => v == expected[i]).All(v => v);

    private static ScoreLabeler BuildLabeler() =>
        ScoreLabeler.Create()
            .When(static (in x) => x > 0).Then(static (in _) => "pos")
            .When(static (in x) => x < 0).Then(static (in _) => "neg")
            .Default(static (in _) => "zero")
            .Build();

    private static ScoreLabeler BuildLabeler_NoDefault() =>
        ScoreLabeler.Create()
            .When(static (in x) => x > 0).Then(static (in _) => "pos")
            .When(static (in x) => x < 0).Then(static (in _) => "neg")
            .Build();

    private static string Label(ScoreLabeler s, int x) => s.Execute(x);

    private static (bool Ok, int Value) ParseOnly(string input)
    {
        var p = IntParser.Create()
            .Always(static (in s, out r) =>
            {
                if (int.TryParse(s, out var tmp)) { r = tmp; return true; }
                r = null; return false;
            })
            .Build();

        var ok = p.Execute(input, out var res);
        return (ok, res ?? 0);
    }

    private static (bool Ok, int Value) ParseWithFallback(string input)
    {
        var p = IntParser.Create()
            .Always(static (in s, out r) =>
            {
                if (int.TryParse(s, out var tmp)) { r = tmp; return true; }
                r = null; return false;
            })
            .Finally(static (in _, out r) => { r = 0; return true; })
            .Build();

        var ok = p.Execute(input, out var res);
        return (ok, res ?? 0);
    }

    private static Exception? ExecuteAndCapture(OrderRouter r, char c)
    {
        try { r.Execute(c); return null; }
        catch (Exception ex) { return ex; }
    }

    // -------- Scenarios ------------------------------------------------------

    [Scenario("OrderRouter routes letter, digit, then default")]
    [Fact]
    public async Task OrderRouter_Routes()
    {
        await Given("a router with log", BuildRouterWithLog)
            .When("execute 'A'", s => Exec(s, 'A'))
            .And("execute '7'", s => Exec(s, '7'))
            .But("execute '@'", s => Exec(s, '@'))
            .Then("log should be L:A, D:7, O:@", s => LogIs(s, "L:A", "D:7", "O:@"))
            .AssertPassed();
    }

    [Scenario("OrderRouter TryExecute is false without default")]
    [Fact]
    public async Task OrderRouter_TryExecute_NoDefault()
    {
        await Given("router with only letter branch",
                () => OrderRouter.Create()
                      .When(static (in c) => char.IsLetter(c)).Then(static (in _) => { })
                      .Build())
            .When("TryExecute '!'", r => r.TryExecute('!'))
            .Then("should be false", ok => ok == false)
            .AssertPassed();
    }

    [Scenario("OrderRouter Execute throws without default")]
    [Fact]
    public async Task OrderRouter_Execute_NoDefault()
    {
        await Given("router with only letter branch",
                () => OrderRouter.Create()
                      .When(static (in c) => char.IsLetter(c)).Then(static (in _) => { })
                      .Build())
            .When("Execute '!'", r => ExecuteAndCapture(r, '!'))
            .Then("is InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();
    }

    [Scenario("ScoreLabeler labels +, -, zero")]
    [Fact]
    public async Task ScoreLabeler_Labels()
    {
        await Given("a score labeler", BuildLabeler)
            .When("label 3", s => Label(s, 3))
            .Then("pos", v => v == "pos")
            .And("label -2", _ => Label(BuildLabeler(), -2) == "neg")
            .But("label 0", _ => Label(BuildLabeler(), 0) == "zero")
            .AssertPassed();
    }

    [Scenario("ScoreLabeler without default throws on zero")]
    [Fact]
    public async Task ScoreLabeler_NoDefault_Throws()
    {
        await Given("labeler without default", BuildLabeler_NoDefault)
            .When("execute(0) capture exception", s =>
            {
                try { _ = s.Execute(0); throw new Exception("Should not reach here"); }
                catch (Exception ex) { return ex; }
            })
            .Then("is InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();
    }

    [Scenario("IntParser success and failure (no fallback)")]
    [Fact]
    public async Task IntParser_NoFallback()
    {
        await Given("input '42'", () => "42")
            .When("parse", ParseOnly)
            .Then("ok && 42", r => r is { Ok: true, Value: 42 })
            .AssertPassed();

        await Given("input 'x'", () => "x")
            .When("parse", ParseOnly)
            .Then("!ok && 0", r => r is { Ok: false, Value: 0 })
            .AssertPassed();
    }

    [Scenario("IntParser failure with fallback returns 0")]
    [Fact]
    public async Task IntParser_Fallback()
    {
        await Given("input 'x'", () => "x")
            .When("parse with fallback", ParseWithFallback)
            .Then("ok && 0", r => r is { Ok: true, Value: 0 })
            .AssertPassed();
    }
}
