using PatternKit.Behavioral.Strategy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Core.Behavioral.Strategy;

[Feature("ActionStrategy<TIn> (first-match-wins action pipeline)")]
public sealed class ActionStrategyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Pipe this context through steps
    private sealed record Ctx(ActionStrategy<int> S, List<string> Log, bool? Ok = null, Exception? Ex = null);

    // --- Helpers to keep steps tiny ---
    private static Ctx Build_Defaulted()
    {
        var log = new List<string>();
        var s = ActionStrategy<int>.Create()
            .When((in i) => i > 0).Then((in i) => log.Add($"+{i}"))
            .When((in i) => i < 0).Then((in i) => log.Add($"{i}"))
            .Default((in _) => log.Add("zero"))
            .Build();
        return new Ctx(s, log);
    }

    private static Ctx Build_NoDefault()
    {
        var log = new List<string>();
        var s = ActionStrategy<int>.Create()
            .When((in i) => i > 0).Then((in i) => log.Add($"+{i}"))
            .When((in i) => i < 0).Then((in i) => log.Add($"{i}"))
            .Build();
        return new Ctx(s, log);
    }

    private static Ctx Exec(Ctx c, int x)
    {
        c.S.Execute(in x);
        return c;
    }

    private static Ctx TryExec(Ctx c, int x) => c with { Ok = c.S.TryExecute(in x) };

    private static Ctx ExecCatch(Ctx c, int x)
    {
        try
        {
            c.S.Execute(in x);
        }
        catch (Exception ex)
        {
            return c with { Ex = ex };
        }

        return c;
    }

    // ---------------------------------------------------------------------

    [Scenario("First matching branch runs; later matches are ignored; default runs when none match")]
    [Fact]
    public async Task FirstMatchAndDefault()
    {
        await Given("a strategy with >0, <0, and default branches", Build_Defaulted)
            .When("executing with 5", c => Exec(c, 5))
            .And("executing with -3", c => Exec(c, -3))
            .But("executing with 0", c => Exec(c, 0))
            .Then("should have logged +5, -3, zero in order",
                c => string.Join("|", c.Log) == "+5|-3|zero")
            .AssertPassed();
    }

    [Scenario("Execute throws when nothing matches and no default is configured")]
    [Fact]
    public async Task ExecuteThrowsWithoutDefault()
    {
        await Given("a strategy without a default branch", Build_NoDefault)
            .When("executing with 0 (no predicates match)", c => ExecCatch(c, 0))
            .Then("should capture InvalidOperationException",
                c => c.Ex is InvalidOperationException)
            .And("should not have logged anything",
                c => c.Log.Count == 0)
            .AssertPassed();
    }

    [Scenario("TryExecute returns true when a branch matches")]
    [Fact]
    public async Task TryExecuteTrueWhenMatched()
    {
        await Given("a strategy that logs even numbers", () =>
            {
                var log = new List<string>();
                var s = ActionStrategy<int>.Create()
                    .When((in i) => i % 2 == 0).Then((in i) => log.Add($"even:{i}"))
                    .Build();
                return new Ctx(s, log);
            })
            .When("TryExecute(4)", c => TryExec(c, 4))
            .Then("should return true", c => c.Ok == true)
            .And("should log even:4", c => string.Join("|", c.Log) == "even:4")
            .AssertPassed();
    }

    [Scenario("TryExecute returns true when only default ran; false when no default and no match")]
    [Fact]
    public async Task TryExecuteDefaultVsNoDefault()
    {
        // With default → true + fallback log
        await Given("a strategy with a default action", Build_Defaulted)
            .When("TryExecute(0) where no predicate matches", c => TryExec(c, 0))
            .Then("should return true", c => c.Ok == true)
            .And("should log zero", c => string.Join("|", c.Log) == "zero")
            .AssertPassed();

        // Without default → false + no logs
        await Given("a strategy without default", Build_NoDefault)
            .When("TryExecute(0) where no predicate matches", c => TryExec(c, 0))
            .Then("should return false", c => c.Ok == false)
            .And("should log nothing", c => c.Log.Count == 0)
            .AssertPassed();
    }

    [Scenario("Registration order is preserved; only the first matching action runs")]
    [Fact]
    public async Task OrderPreserved_FirstMatchOnly()
    {
        await Given("a strategy with overlapping predicates in order", () =>
            {
                var log = new List<string>();
                var s = ActionStrategy<int>.Create()
                    .When((in i) => i % 2 == 0).Then((in _) => log.Add("first"))
                    .When((in i) => i >= 0).Then((in _) => log.Add("second"))
                    .Default((in _) => log.Add("default"))
                    .Build();
                return new Ctx(s, log);
            })
            .When("executing with 2 (both predicates true)", c => Exec(c, 2))
            .Then("should only run the first action", c => string.Join("|", c.Log) == "first")
            .AssertPassed();
    }
}