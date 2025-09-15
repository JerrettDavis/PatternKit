using PatternKit.Behavioral.Chain;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.Chain;

[Feature("ActionChain<TCtx> (middleware-style pipeline)")]
public sealed class ActionChainTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // Context we thread through the chain
    private readonly record struct Ctx(List<string> Log, bool Flag = false);

    private sealed record State(ActionChain<Ctx> Chain, List<string> Log);

    // ---------------- Helpers ----------------

    private static State Build_Order_With_Tail()
    {
        var log = new List<string>();
        var chain = ActionChain<Ctx>.Create()
            .Use(static (in c, next) =>
            {
                c.Log.Add("A");
                next(in c);
            })
            .Use(static (in c, next) =>
            {
                c.Log.Add("B");
                next(in c);
            })
            .Finally(static (in c, next) =>
            {
                c.Log.Add("TAIL");
                next(in c); // terminal no-op; safe
            })
            .Build();

        return new State(chain, log);
    }

    private static State Build_Stop_ShortCircuits()
    {
        var log = new List<string>();
        var chain = ActionChain<Ctx>.Create()
            .Use(static (in c, next) =>
            {
                c.Log.Add("PRE");
                next(in c);
            })
            .When(static (in _) => true)
            .ThenStop(static c => c.Log.Add("STOP"))
            .Use(static (in c, next) =>
            {
                c.Log.Add("POST"); // should NOT run
                next(in c);
            })
            .Finally(static (in c, next) =>
            {
                c.Log.Add("TAIL"); // should NOT run
                next(in c);
            })
            .Build();

        return new State(chain, log);
    }

    private static State Build_Continue_Then_Tail()
    {
        var log = new List<string>();
        var chain = ActionChain<Ctx>.Create()
            .When(static (in _) => true)
            .ThenContinue(static c => c.Log.Add("CONT"))
            .Finally(static (in c, next) =>
            {
                c.Log.Add("TAIL");
                next(in c);
            })
            .Build();

        return new State(chain, log);
    }

    private static State Build_When_False_AutoContinue()
    {
        var log = new List<string>();
        var chain = ActionChain<Ctx>.Create()
            .When(static (in c) => c.Flag) // false by default
            .ThenStop(static c => c.Log.Add("NEVER"))
            .Finally(static (in c, next) =>
            {
                c.Log.Add("TAIL");
                next(in c);
            })
            .Build();

        return new State(chain, log);
    }

    private static (ActionChain<Ctx> First, ActionChain<Ctx> Second, List<string> Log) Build_Immutability_TwoChains()
    {
        var log = new List<string>();
        var b = ActionChain<Ctx>.Create()
            .Use(static (in c, next) =>
            {
                c.Log.Add("A");
                next(in c);
            });

        var first = b.Build(); // freezes A only

        // mutate builder after first build
        b.Use(static (in c, next) =>
            {
                c.Log.Add("B");
                next(in c);
            })
            .Finally(static (in c, next) =>
            {
                c.Log.Add("TAIL");
                next(in c);
            });

        var second = b.Build(); // A then B then TAIL
        return (first, second, log);
    }

    private static State Build_Handler_ShortCircuits_Skips_Tail()
    {
        var log = new List<string>();
        var chain = ActionChain<Ctx>.Create()
            .Use(static (in c, _) =>
            {
                c.Log.Add("ONLY");
                // do NOT call next → short-circuit
            })
            .Finally(static (in c, next) =>
            {
                c.Log.Add("TAIL"); // should NOT run
                next(in c);
            })
            .Build();

        return new State(chain, log);
    }

    private static State Exec(State s, bool flag = false)
    {
        var ctx = new Ctx(s.Log, Flag: flag);
        s.Chain.Execute(in ctx);
        return s;
    }

    // ---------------- Scenarios ----------------

    [Scenario("Order: Use handlers run in registration order; tail runs last when not short-circuited")]
    [Fact]
    public async Task OrderAndTail()
    {
        await Given("A, then B, then TAIL", Build_Order_With_Tail)
            .When("executing the chain", s => Exec(s))
            .Then("log is A|B|TAIL", s => string.Join('|', s.Log) == "A|B|TAIL")
            .AssertPassed();
    }

    [Scenario("ThenStop short-circuits: subsequent handlers and tail do not run")]
    [Fact]
    public async Task ThenStopShortCircuits()
    {
        await Given("PRE, ThenStop(true), POST, TAIL", Build_Stop_ShortCircuits)
            .When("executing the chain", s => Exec(s))
            .Then("log is PRE|STOP", s => string.Join('|', s.Log) == "PRE|STOP")
            .AssertPassed();
    }

    [Scenario("ThenContinue runs action and still proceeds to later handlers/tail")]
    [Fact]
    public async Task ThenContinueProceeds()
    {
        await Given("ThenContinue(true), TAIL", Build_Continue_Then_Tail)
            .When("executing the chain", s => Exec(s))
            .Then("log is CONT|TAIL", s => string.Join('|', s.Log) == "CONT|TAIL")
            .AssertPassed();
    }

    [Scenario("When(false) auto-continues without invoking the guarded action")]
    [Fact]
    public async Task WhenFalseAutoContinues()
    {
        await Given("When(c.Flag) ThenStop(NEVER) + TAIL", Build_When_False_AutoContinue)
            .When("executing with Flag=false", s => Exec(s, flag: false))
            .Then("log is TAIL", s => string.Join('|', s.Log) == "TAIL")
            .AssertPassed();
    }

    [Scenario("Tail is skipped when an earlier handler returns without calling next")]
    [Fact]
    public async Task TailSkippedOnShortCircuit()
    {
        await Given("first handler logs and short-circuits; tail exists", Build_Handler_ShortCircuits_Skips_Tail)
            .When("executing the chain", s => Exec(s))
            .Then("log is ONLY", s => string.Join('|', s.Log) == "ONLY")
            .AssertPassed();
    }

    [Scenario("Built chains are immutable; builder can continue to be mutated to produce new chains")]
    [Fact]
    public async Task ImmutabilityAfterBuild()
    {
        await Given("one builder, then build first and mutate for second", Build_Immutability_TwoChains)
            .When("executing first chain", t =>
            {
                var (first, _, log) = t;
                var ctx = new Ctx(log);
                first.Execute(in ctx);
                return t;
            })
            .And("executing second chain", t =>
            {
                var (_, second, log) = t;
                var ctx = new Ctx(log);
                second.Execute(in ctx);
                return t;
            })
            .Then("first execution logged A only", t =>
            {
                var (_, _, log) = t;
                // logs so far: first run (A)
                return log[0] == "A";
            })
            .And("second execution added A|B|TAIL", t =>
            {
                var (_, _, log) = t;
                // full log list: ["A", "A", "B", "TAIL"]
                return string.Join('|', log) == "A|A|B|TAIL";
            })
            .AssertPassed();
    }
}