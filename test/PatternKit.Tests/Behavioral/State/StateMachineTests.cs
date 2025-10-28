using PatternKit.Behavioral.State;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.State;

public sealed class StateMachineTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private enum S { Idle, Active, Alarm }
    private readonly record struct Ev(string Kind);

    private static bool Is(string k, in Ev e) => e.Kind == k;

    private sealed record Ctx(StateMachine<S, Ev> M, List<string> Log, S State, bool LastOk = false, Exception? Ex = null);

    private static Ctx BuildCtx()
    {
        var log = new List<string>();
        var m = StateMachine<S, Ev>.Create()
            .InState(S.Idle, s => s
                .OnExit((in _) => log.Add("exit:Idle"))
                .When(static (in e) => Is("go", in e)).Permit(S.Active).Do((in _) => log.Add("effect:go"))
                .When(static (in e) => Is("panic", in e)).Permit(S.Alarm).Do((in _) => log.Add("effect:panic"))
            )
            .InState(S.Active, s => s
                .OnEnter((in _) => log.Add("enter:Active"))
                .OnExit((in _) => log.Add("exit:Active"))
                .When(static (in e) => Is("ping", in e)).Stay().Do((in _) => log.Add("effect:ping"))
                .When(static (in e) => Is("stop", in e)).Permit(S.Idle).Do((in _) => log.Add("effect:stop"))
            )
            .InState(S.Alarm, s => s
                .OnEnter((in _) => log.Add("enter:Alarm"))
                .When(static (in e) => Is("reset", in e)).Permit(S.Idle).Do((in _) => log.Add("effect:reset"))
                .Otherwise().Stay().Do((in _) => log.Add("effect:default"))
            )
            .Build();
        return new Ctx(m, log, S.Idle);
    }

    private static Ctx Fire(Ctx c, string kind)
    {
        var s = c.State;
        var e = new Ev(kind);
        var ok = c.M.TryTransition(ref s, in e);
        return c with { State = s, LastOk = ok };
    }

    private static Ctx FireThrow(Ctx c, string kind)
    {
        try
        {
            var s = c.State;
            var e = new Ev(kind);
            c.M.Transition(ref s, in e);
            return c with { State = s, Ex = null };
        }
        catch (Exception ex)
        {
            return c with { Ex = ex };
        }
    }

    [Scenario("Exit/Effect/Enter order on state change; stay executes effect only")]
    [Fact]
    public async Task Order_And_Stay()
    {
        await Given("a simple 3-state machine context", BuildCtx)
            .When("go from Idle -> Active", c => Fire(c, "go"))
            .Then("moved to Active and exit/effect/enter order recorded", c =>
                c.LastOk && c.State == S.Active && string.Join(",", c.Log.ToArray()) == "exit:Idle,effect:go,enter:Active")
            .When("ping stays in Active with effect only", c => { c.Log.Clear(); return (Fire(c with { State = S.Active }, "ping")); })
            .Then("remains Active and only effect logged", c => c.LastOk && c.State == S.Active && string.Join(",", c.Log.ToArray()) == "effect:ping")
            .AssertPassed();
    }

    [Scenario("Default transition fires when nothing matches")]
    [Fact]
    public async Task Default_Fires()
    {
        await Given("in Alarm with default stay", BuildCtx)
            .When("go to Alarm first", c => Fire(c, "panic"))
            .When("unknown event handled by default", c => { c.Log.Clear(); return Fire(c, "unknown"); })
            .Then("handled, remained Alarm, effect ran", c => c.LastOk && c.State == S.Alarm && string.Join(",", c.Log.ToArray()) == "effect:default")
            .AssertPassed();
    }

    [Scenario("Unhandled event yields false for TryTransition and throws for Transition")]
    [Fact]
    public async Task Unhandled_Behavior()
    {
        await Given("machine ctx", BuildCtx)
            .When("unknown via TryTransition", c => Fire(c, "nope"))
            .Then("returns false and state unchanged", c => !c.LastOk && c.State == S.Idle)
            .When("unknown via Transition throws", c => FireThrow(c, "nope"))
            .Then("throws InvalidOperationException", c => c.Ex is InvalidOperationException)
            .AssertPassed();
    }

    [Scenario("Registration order: first matching transition wins")]
    [Fact]
    public async Task First_Match_Wins()
    {
        var log = new List<string>();
        var m = StateMachine<S, Ev>.Create()
            .InState(S.Idle, s => s
                .When(static (in e) => e.Kind.Length > 0).Stay().Do((in _) => log.Add("first"))
                .When(static (in e) => e.Kind == "x").Permit(S.Active).Do((in _) => log.Add("second"))
            )
            .Build();

        await Given("ctx", () => new Ctx(m, log, S.Idle))
            .When("'x' should match first rule", c =>
            {
                var s = c.State; var e = new Ev("x"); var ok = c.M.TryTransition(ref s, in e);
                return c with { State = s, LastOk = ok };
            })
            .Then("consumed by first rule; no state change and first logged", c => c.LastOk && c.State == S.Idle && log.First() == "first")
            .AssertPassed();
    }
}
