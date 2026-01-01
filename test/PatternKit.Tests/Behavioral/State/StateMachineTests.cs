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

#region Additional StateMachine Tests

public sealed class StateMachineBuilderTests
{
    private enum S { A, B, C, D }
    private readonly record struct E(string Kind);

    [Fact]
    public void CustomComparer_Works()
    {
        var log = new List<string>();
        var m = StateMachine<string, E>.Create()
            .Comparer(StringComparer.OrdinalIgnoreCase)
            .InState("idle", s => s
                .When((in e) => e.Kind == "go").Permit("ACTIVE").End()
            )
            .InState("ACTIVE", s => s
                .OnEnter((in _) => log.Add("entered"))
            )
            .Build();

        var state = "IDLE"; // Different case
        var handled = m.TryTransition(ref state, new E("go"));

        Assert.True(handled);
        Assert.Equal("ACTIVE", state);
        Assert.Contains("entered", log);
    }

    [Fact]
    public void ThenBuilder_End_Without_Effect()
    {
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((in e) => e.Kind == "go").Permit(S.B).End()
            )
            .Build();

        var state = S.A;
        var handled = m.TryTransition(ref state, new E("go"));

        Assert.True(handled);
        Assert.Equal(S.B, state);
    }

    [Fact]
    public void ThenBuilder_Stay_End()
    {
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((in e) => e.Kind == "stay").Stay().End()
            )
            .Build();

        var state = S.A;
        var handled = m.TryTransition(ref state, new E("stay"));

        Assert.True(handled);
        Assert.Equal(S.A, state);
    }

    [Fact]
    public void ThenBuilder_AsDefault_Stay()
    {
        var log = new List<string>();
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((in e) => e.Kind == "specific").Permit(S.B).Do((in _) => log.Add("specific"))
                .Otherwise().Stay().Do((in _) => log.Add("default"))
            )
            .Build();

        var state = S.A;
        var handled = m.TryTransition(ref state, new E("anything"));

        Assert.True(handled);
        Assert.Equal(S.A, state);
        Assert.Equal("default", log[0]);
    }

    [Fact]
    public void ThenBuilder_AsDefault_Permit()
    {
        var log = new List<string>();
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit((in _) => log.Add("exit:A"))
                .Otherwise().Permit(S.C).Do((in _) => log.Add("default:go-to-C"))
            )
            .InState(S.C, s => s
                .OnEnter((in _) => log.Add("enter:C"))
            )
            .Build();

        var state = S.A;
        var handled = m.TryTransition(ref state, new E("anything"));

        Assert.True(handled);
        Assert.Equal(S.C, state);
        Assert.Equal("exit:A", log[0]);
        Assert.Equal("default:go-to-C", log[1]);
        Assert.Equal("enter:C", log[2]);
    }

    [Fact]
    public void MultipleOnEnterOnExit_Hooks()
    {
        var log = new List<string>();
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit((in _) => log.Add("exit:A:1"))
                .OnExit((in _) => log.Add("exit:A:2"))
                .When((in e) => e.Kind == "go").Permit(S.B).End()
            )
            .InState(S.B, s => s
                .OnEnter((in _) => log.Add("enter:B:1"))
                .OnEnter((in _) => log.Add("enter:B:2"))
            )
            .Build();

        var state = S.A;
        m.TryTransition(ref state, new E("go"));

        Assert.Equal(4, log.Count);
        Assert.Equal("exit:A:1", log[0]);
        Assert.Equal("exit:A:2", log[1]);
        Assert.Equal("enter:B:1", log[2]);
        Assert.Equal("enter:B:2", log[3]);
    }

    [Fact]
    public void StateBuilder_Direct_Access()
    {
        var log = new List<string>();
        var builder = StateMachine<S, E>.Create();

        var stateBuilder = builder.State(S.A);
        stateBuilder.OnEnter((in _) => log.Add("enter:A"));
        stateBuilder.When((in e) => e.Kind == "go").Permit(S.B).End();
        stateBuilder.End(); // Return to builder

        builder.InState(S.B, s => s
            .OnEnter((in _) => log.Add("enter:B"))
        );

        var m = builder.Build();

        var state = S.A;
        m.TryTransition(ref state, new E("go"));

        Assert.Equal(S.B, state);
        Assert.Contains("enter:B", log);
    }

    [Fact]
    public void UnknownState_Returns_False()
    {
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((in e) => e.Kind == "go").Permit(S.B).End()
            )
            .Build();

        var state = S.C; // Not configured
        var handled = m.TryTransition(ref state, new E("go"));

        Assert.False(handled);
        Assert.Equal(S.C, state);
    }

    [Fact]
    public void Transition_To_Self_State_No_Exit_Enter()
    {
        var log = new List<string>();
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnEnter((in _) => log.Add("enter:A"))
                .OnExit((in _) => log.Add("exit:A"))
                .When((in e) => e.Kind == "self").Permit(S.A).Do((in _) => log.Add("effect"))
            )
            .Build();

        var state = S.A;
        m.TryTransition(ref state, new E("self"));

        // Should only run effect, not exit/enter since state stays the same
        Assert.Single(log);
        Assert.Equal("effect", log[0]);
    }

    [Fact]
    public void WhenBuilder_Permit_Then_Stay_Override()
    {
        // Test that Stay() can override Permit()
        var log = new List<string>();
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((in e) => e.Kind == "test").Permit(S.B).Stay().Do((in _) => log.Add("stayed"))
            )
            .Build();

        var state = S.A;
        m.TryTransition(ref state, new E("test"));

        Assert.Equal(S.A, state); // Should stay
        Assert.Equal("stayed", log[0]);
    }

    [Fact]
    public void Default_Effect_Only_On_Stay()
    {
        var log = new List<string>();
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit((in _) => log.Add("exit"))
                .Otherwise().Stay().Do((in _) => log.Add("default-effect"))
            )
            .Build();

        var state = S.A;
        m.TryTransition(ref state, new E("any"));

        // No exit should be called for stay
        Assert.Single(log);
        Assert.Equal("default-effect", log[0]);
    }

    [Fact]
    public void Null_Hook_Is_Ignored()
    {
        var log = new List<string>();
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnEnter(null!)
                .OnExit(null!)
                .When((in e) => e.Kind == "go").Permit(S.B).End()
            )
            .InState(S.B, s => s
                .OnEnter((in _) => log.Add("enter:B"))
            )
            .Build();

        var state = S.A;
        m.TryTransition(ref state, new E("go"));

        Assert.Equal(S.B, state);
        Assert.Single(log);
    }

    [Fact]
    public void NoDefault_NoMatch_Returns_False()
    {
        var m = StateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((in e) => e.Kind == "specific").Permit(S.B).End()
            )
            .Build();

        var state = S.A;
        var handled = m.TryTransition(ref state, new E("other"));

        Assert.False(handled);
    }
}

#endregion
