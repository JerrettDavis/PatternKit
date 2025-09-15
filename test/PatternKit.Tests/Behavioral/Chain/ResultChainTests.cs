using PatternKit.Behavioral.Chain;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Chain;

[Feature("ResultChain<TIn, TOut> (first-match-wins with return value)")]
public sealed class ResultChainTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Context threaded through steps so we can accumulate logs and last result
    private sealed record Ctx(
        ResultChain<int, string> Chain,
        List<string> Log,
        bool? Ok = null,
        string? Result = null
    );

    // ---------- Helpers ----------
    private static Ctx Build_Defaulted()
    {
        var log = new List<string>();
        var chain = ResultChain<int, string>.Create()
            .When(static (in i) => i > 0).Then(i =>
            {
                log.Add("pos");
                return $"+{i}";
            })
            .When(static (in i) => i < 0).Then(i =>
            {
                log.Add("neg");
                return i.ToString();
            })
            .Finally(static (in _, out r, _) =>
            {
                // default / fallback
                r = "zero";
                return true;
            })
            .Build();

        return new Ctx(chain, log);
    }

    private static Ctx Build_NoTail()
    {
        var log = new List<string>();
        var chain = ResultChain<int, string>.Create()
            .When(static (in i) => i > 0).Then(i => $"+{i}")
            .Build();
        return new Ctx(chain, log);
    }

    private static Ctx Build_Do_Then_Tail()
    {
        var log = new List<string>();
        var chain = ResultChain<int, string>.Create()
            // A conditional TryHandler that sometimes produces, sometimes delegates
            .When(static (in i) => i % 2 == 0).Do((in i, out r, next) =>
            {
                log.Add("even?");
                if (i != 42)
                    return next(in i, out r); // delegate to next
                r = "forty-two";
                return true; // short-circuit

            })
            // Next even handler will run if previous delegated
            .When(static (in i) => i % 2 == 0).Then(_ =>
            {
                log.Add("even");
                return "even";
            })
            // Fallback when nothing produced
            .Finally((in _, out r, _) =>
            {
                log.Add("tail");
                r = "odd";
                return true;
            })
            .Build();

        return new Ctx(chain, log);
    }

    private static Ctx Exec(Ctx c, int x)
    {
        var ok = c.Chain.Execute(in x, out var r);
        return c with { Ok = ok, Result = r };
    }

    // ---------- Scenarios ----------

    [Scenario("First match wins; Finally acts as default when nothing matched")]
    [Fact]
    public async Task FirstMatch_And_Fallback()
    {
        await Given("a chain with >0, <0, and Finally fallback", Build_Defaulted)
            .When("executing with 5", c => Exec(c, 5))
            .Then("returns true and +5", c => c.Ok == true && c.Result == "+5")
            .And("log recorded 'pos' and not 'neg'", c =>
                string.Join("|", c.Log) == "pos")
            // run negative
            .When("executing with -3", c =>
            {
                c.Log.Clear();
                return Exec(c, -3);
            })
            .Then("returns true and -3", c => c.Ok == true && c.Result == "-3")
            .And("log recorded 'neg' and not 'pos'", c =>
                string.Join("|", c.Log) == "neg")
            // run zero -> fallback
            .When("executing with 0", c =>
            {
                c.Log.Clear();
                return Exec(c, 0);
            })
            .Then("returns true and 'zero'", c => c.Ok == true && c.Result == "zero")
            .AssertPassed();
    }

    [Scenario("No tail: Execute returns false and result is null when nothing matched")]
    [Fact]
    public async Task NoTail_NoMatch_ReturnsFalse()
    {
        await Given("a chain with only >0 branch and no Finally", Build_NoTail)
            .When("executing with 0 (no predicate matches)", c => Exec(c, 0))
            .Then("Execute returns false", c => c.Ok == false)
            .And("result is null", c => c.Result is null)
            .AssertPassed();
    }

    [Scenario("When.Do can produce or delegate; Then handles delegated; Finally covers leftovers")]
    [Fact]
    public async Task Do_Then_Tail_Composition()
    {
        await Given("a chain with Do (sometimes produce), Then (even), and tail (odd)", Build_Do_Then_Tail)
            .When("executing with 42 (Do produces)", c => Exec(c, 42))
            .Then("returns true and 'forty-two'", c => c.Ok == true && c.Result == "forty-two")
            .And("log contains only 'even?'", c => string.Join("|", c.Log) == "even?")
            // delegated case
            .When("executing with 4 (Do delegates to Then)", c =>
            {
                c.Log.Clear();
                return Exec(c, 4);
            })
            .Then("returns true and 'even'", c => c.Ok == true && c.Result == "even")
            .And("log contains 'even?|even'", c => string.Join("|", c.Log) == "even?|even")
            // odd -> tail
            .When("executing with 3 (nothing matched before tail)", c =>
            {
                c.Log.Clear();
                return Exec(c, 3);
            })
            .Then("returns true and 'odd'", c => c.Ok == true && c.Result == "odd")
            .And("log ends with 'tail'", c => c.Log.LastOrDefault() == "tail")
            .AssertPassed();
    }

    [Scenario("Registration order preserved; only first matching producer runs")]
    [Fact]
    public async Task OrderPreserved_FirstProducerOnly()
    {
        await Given("a chain with two overlapping predicates in registration order", () =>
            {
                var log = new List<string>();
                var chain = ResultChain<int, string>.Create()
                    .When(static (in i) => i >= 0).Then(_ =>
                    {
                        log.Add("first");
                        return "first";
                    })
                    .When(static (in i) => i >= 0).Then(_ =>
                    {
                        log.Add("second");
                        return "second";
                    })
                    .Finally((in _, out r, _) =>
                    {
                        r = "tail";
                        return true;
                    })
                    .Build();
                return new Ctx(chain, log);
            })
            .When("executing with 2 (both predicates true)", c => Exec(c, 2))
            .Then("result is from the first producer", c => c.Result == "first")
            .And("log recorded only 'first'", c => string.Join("|", c.Log) == "first")
            .AssertPassed();
    }

    [Scenario("Finally runs only when chain reaches the tail (no earlier producer)")]
    [Fact]
    public async Task Tail_Runs_Only_When_Not_ShortCircuited()
    {
        await Given("a chain with a producing head and a logging tail", () =>
            {
                var log = new List<string>();
                var chain = ResultChain<int, string>.Create()
                    .When(static (in i) => i > 0).Then(i =>
                    {
                        log.Add("head");
                        return $"+{i}";
                    })
                    .Finally((in _, out r, _) =>
                    {
                        log.Add("tail");
                        r = "zero-or-neg";
                        return true;
                    })
                    .Build();
                return new Ctx(chain, log);
            })
            // head produces -> tail should not log
            .When("executing with 7", c => Exec(c, 7))
            .Then("result is '+7'", c => c.Result == "+7")
            .And("tail did not run", c => !c.Log.Contains("tail"))
            // no head match -> tail runs
            .When("executing with 0", c =>
            {
                c.Log.Clear();
                return Exec(c, 0);
            })
            .Then("result is from tail", c => c.Result == "zero-or-neg")
            .And("tail logged", c => c.Log.Contains("tail"))
            .AssertPassed();
    }
}