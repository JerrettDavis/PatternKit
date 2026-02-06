using PatternKit.Behavioral.Strategy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Strategy;

[Feature("Strategy<TIn,TOut> (first-match-wins, returns value)")]
public sealed class StrategyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // ---------- Shared predicate/handler method pointers ----------
    private static bool IsPositive(in int x) => x > 0;
    private static bool IsNegative(in int x) => x < 0;
    private static bool IsNonNegative(in int x) => x >= 0;
    private static bool IsEven(in int x) => (x & 1) == 0;

    private static string LabelPositive(in int x) => $"pos:{x}";
    private static string LabelNegative(in int x) => $"neg:{x}";
    private static string LabelFirst(in int _) => "first";
    private static string LabelSecond(in int _) => "second";
    private static string LabelZero(in int _) => "zero";
    private static string LabelOther(in int _) => "other";
    private static string LabelEven(in int _) => "even";

    // Context keeps the strategy + last result/exception so TinyBDD type stays stable
    private sealed record Ctx(Strategy<int, string> S, string? Last = null, Exception? Ex = null);

    // ---------- Builders ----------
    private static Ctx Build_PosNeg_DefaultZero()
        => new(
            Strategy<int, string>.Create()
                .When(IsPositive).Then(LabelPositive)
                .When(IsNegative).Then(LabelNegative)
                .Default(LabelZero)
                .Build());

    private static Ctx Build_PosOnly_NoDefault()
        => new(
            Strategy<int, string>.Create()
                .When(IsPositive).Then(LabelPositive)
                .Build());

    private static Ctx Build_Order_NonNeg_Then_Even()
        => new(
            Strategy<int, string>.Create()
                .When(IsNonNegative).Then(LabelFirst)
                .When(IsEven).Then(LabelSecond)
                .Default(LabelOther)
                .Build());

    private static Ctx Build_Order_Even_Then_NonNeg()
        => new(
            Strategy<int, string>.Create()
                .When(IsEven).Then(LabelFirst)
                .When(IsNonNegative).Then(LabelSecond)
                .Default(LabelOther)
                .Build());

    private static Ctx Build_Even_With_DefaultOther()
        => new(
            Strategy<int, string>.Create()
                .When(IsEven).Then(LabelEven)
                .Default(LabelOther)
                .Build());

    // ---------- Helpers ----------
    private static Ctx Exec(Ctx c, int x)
    {
        var v = x; // need a variable for 'in'
        var r = c.S.Execute(in v);
        return c with { Last = r, Ex = null };
    }

    private static Ctx ExecCatch(Ctx c, int x)
    {
        try
        {
            var v = x; // need a variable for 'in'
            var r = c.S.Execute(in v);
            return c with { Last = r, Ex = null };
        }
        catch (Exception ex)
        {
            return c with { Ex = ex };
        }
    }

    // ---------- Scenarios ----------

    [Scenario("First matching branch wins; default runs when none match")]
    [Fact]
    public async Task FirstMatch_And_Default()
    {
        await Given("a strategy with >0, <0, and default 'zero'", Build_PosNeg_DefaultZero)
            .When("executing with 5", c => Exec(c, 5))
            .Then("returns 'pos:5'", c => c.Last == "pos:5")
            .When("executing with -3", c => Exec(c, -3))
            .Then("returns 'neg:-3'", c => c.Last == "neg:-3")
            .When("executing with 0", c => Exec(c, 0))
            .Then("returns 'zero'", c => c.Last == "zero")
            .AssertPassed();
    }

    [Scenario("Execute throws when nothing matches and no default is configured")]
    [Fact]
    public async Task Throws_Without_Default()
    {
        await Given("a strategy with only >0 branch (no default)", Build_PosOnly_NoDefault)
            .When("executing with 0", c => ExecCatch(c, 0))
            .Then("captures InvalidOperationException", c => c.Ex is InvalidOperationException)
            .AssertPassed();
    }

    [Scenario("Registration order is preserved; only the first matching handler executes")]
    [Fact]
    public async Task Order_Preserved_First_Match_Only()
    {
        await Given("NonNegative before Even", Build_Order_NonNeg_Then_Even)
            .When("executing with 2 (matches both)", c => Exec(c, 2))
            .Then("result is from the first branch", c => c.Last == "first")
            .AssertPassed();

        await Given("Even before NonNegative", Build_Order_Even_Then_NonNeg)
            .When("executing with 2 (matches both)", c => Exec(c, 2))
            .Then("result is from the first (even) branch", c => c.Last == "first")
            .AssertPassed();
    }

    [Scenario("Default returns a value when all predicates fail")]
    [Fact]
    public async Task Default_Returns_Value()
    {
        await Given("a strategy with only Even branch plus default 'other'", Build_Even_With_DefaultOther)
            .When("executing with 3 (no predicate match)", c => Exec(c, 3))
            .Then("returns 'other'", c => c.Last == "other")
            .AssertPassed();
    }
}
