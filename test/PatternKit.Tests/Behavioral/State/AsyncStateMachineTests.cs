using PatternKit.Behavioral.State;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Behavioral.State;

public sealed class AsyncStateMachineTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private enum S { Idle, Active, Alarm }
    private readonly record struct Ev(string Kind);

    private sealed record Ctx(AsyncStateMachine<S, Ev> M, List<string> Log, S State, bool LastOk = false, Exception? Ex = null);

    private static Ctx Build()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, Ev>.Create()
            .InState(S.Idle, s => s
                .OnExit(async (_, _) => { await Task.Yield(); log.Add("exit:Idle"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "go")).Permit(S.Active).Do(async (_, ct) => { await Task.Delay(1, ct); log.Add("effect:go"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "panic")).Permit(S.Alarm).Do(async (_, _) => { await Task.Yield(); log.Add("effect:panic"); })
            )
            .InState(S.Active, s => s
                .OnEnter(async (_, _) => { await Task.Yield(); log.Add("enter:Active"); })
                .OnExit(async (_, _) => { await Task.Yield(); log.Add("exit:Active"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "ping")).Stay().Do(async (_, _) => { await Task.Yield(); log.Add("effect:ping"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "stop")).Permit(S.Idle).Do(async (_, _) => { await Task.Yield(); log.Add("effect:stop"); })
            )
            .InState(S.Alarm, s => s
                .OnEnter(async (_, _) => { await Task.Yield(); log.Add("enter:Alarm"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "reset")).Permit(S.Idle).Do(async (_, _) => { await Task.Yield(); log.Add("effect:reset"); })
                .Otherwise().Stay().Do(async (_, _) => { await Task.Yield(); log.Add("effect:default"); })
            )
            .Build();
        return new Ctx(m, log, S.Idle);
    }

    private static async ValueTask<Ctx> Fire(Ctx c, string k)
    {
        var (ok, s) = await c.M.TryTransitionAsync(c.State, new Ev(k));
        return c with { State = s, LastOk = ok };
    }

    private static async ValueTask<Ctx> FireThrow(Ctx c, string k)
    {
        try
        {
            var s = await c.M.TransitionAsync(c.State, new Ev(k));
            return c with { State = s, Ex = null };
        }
        catch (Exception ex)
        {
            return c with { Ex = ex };
        }
    }

    [Scenario("Exit/Effect/Enter order on async state change; stay executes effect only")]
    [Fact]
    public async Task Order_And_Stay_Async()
    {
        await Given("async machine", Build)
            .When("go to Active", c => Fire(c, "go"))
            .Then("moved to Active and order ok", c => c.LastOk && c.State == S.Active && string.Join(',', c.Log.ToArray()) == "exit:Idle,effect:go,enter:Active")
            .When("stay with ping", c => { c.Log.Clear(); return Fire(c with { State = S.Active }, "ping"); })
            .Then("only effect ran", c => c.LastOk && c.State == S.Active && string.Join(',', c.Log.ToArray()) == "effect:ping")
            .AssertPassed();
    }

    [Scenario("Default fires async when nothing matches")]
    [Fact]
    public async Task Default_Fires_Async()
    {
        await Given("in Alarm state", Build)
            .When("to Alarm", c => Fire(c, "panic"))
            .When("unknown event", c => { c.Log.Clear(); return Fire(c, "unknown"); })
            .Then("handled by default", c => c.LastOk && c.State == S.Alarm && c.Log.SequenceEqual(["effect:default"]))
            .AssertPassed();
    }

    [Scenario("Unhandled path throws in async TransitionAsync")]
    [Fact]
    public async Task Unhandled_Async()
    {
        await Given("async machine", Build)
            .When("unknown via TryTransitionAsync", c => Fire(c, "nope"))
            .Then("returns false", c => !c.LastOk && c.State == S.Idle)
            .When("unknown via TransitionAsync", c => FireThrow(c, "nope"))
            .Then("throws InvalidOperationException", c => c.Ex is InvalidOperationException)
            .AssertPassed();
    }
}

#region Additional AsyncStateMachine Tests

public sealed class AsyncStateMachineBuilderTests
{
    private enum S { A, B, C, D }
    private readonly record struct E(string Kind);

    [Fact]
    public async Task CustomComparer_Works()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<string, E>.Create()
            .Comparer(StringComparer.OrdinalIgnoreCase)
            .InState("idle", s => s
                .When((e, _) => new ValueTask<bool>(e.Kind == "go")).Permit("ACTIVE").End()
            )
            .InState("ACTIVE", s => s
                .OnEnter(async (_, _) => log.Add("entered"))
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync("IDLE", new E("go"));

        Assert.True(handled);
        Assert.Equal("ACTIVE", state);
        Assert.Contains("entered", log);
    }

    [Fact]
    public async Task ThenBuilder_End_Without_Effect()
    {
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((e, _) => new ValueTask<bool>(e.Kind == "go")).Permit(S.B).End()
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("go"));

        Assert.True(handled);
        Assert.Equal(S.B, state);
    }

    [Fact]
    public async Task ThenBuilder_Stay_End()
    {
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((e, _) => new ValueTask<bool>(e.Kind == "stay")).Stay().End()
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("stay"));

        Assert.True(handled);
        Assert.Equal(S.A, state);
    }

    [Fact]
    public async Task ThenBuilder_AsDefault_Stay()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .Otherwise().Stay().Do(async (_, _) => log.Add("default"))
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("anything"));

        Assert.True(handled);
        Assert.Equal(S.A, state);
        Assert.Equal("default", log[0]);
    }

    [Fact]
    public async Task ThenBuilder_AsDefault_Permit()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit(async (_, _) => log.Add("exit:A"))
                .Otherwise().Permit(S.C).Do(async (_, _) => log.Add("default:go-to-C"))
            )
            .InState(S.C, s => s
                .OnEnter(async (_, _) => log.Add("enter:C"))
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("anything"));

        Assert.True(handled);
        Assert.Equal(S.C, state);
        Assert.Equal("exit:A", log[0]);
        Assert.Equal("default:go-to-C", log[1]);
        Assert.Equal("enter:C", log[2]);
    }

    [Fact]
    public async Task MultipleOnEnterOnExit_Hooks()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit(async (_, _) => log.Add("exit:A:1"))
                .OnExit(async (_, _) => log.Add("exit:A:2"))
                .When((e, _) => new ValueTask<bool>(e.Kind == "go")).Permit(S.B).End()
            )
            .InState(S.B, s => s
                .OnEnter(async (_, _) => log.Add("enter:B:1"))
                .OnEnter(async (_, _) => log.Add("enter:B:2"))
            )
            .Build();

        await m.TryTransitionAsync(S.A, new E("go"));

        Assert.Equal(4, log.Count);
        Assert.Equal("exit:A:1", log[0]);
        Assert.Equal("exit:A:2", log[1]);
        Assert.Equal("enter:B:1", log[2]);
        Assert.Equal("enter:B:2", log[3]);
    }

    [Fact]
    public async Task StateBuilder_Direct_Access()
    {
        var log = new List<string>();
        var builder = AsyncStateMachine<S, E>.Create();

        var stateBuilder = builder.State(S.A);
        stateBuilder.OnEnter(async (_, _) => log.Add("enter:A"));
        stateBuilder.When((e, _) => new ValueTask<bool>(e.Kind == "go")).Permit(S.B).End();
        stateBuilder.End();

        builder.InState(S.B, s => s
            .OnEnter(async (_, _) => log.Add("enter:B"))
        );

        var m = builder.Build();

        await m.TryTransitionAsync(S.A, new E("go"));

        Assert.Contains("enter:B", log);
    }

    [Fact]
    public async Task UnknownState_Returns_False()
    {
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((e, _) => new ValueTask<bool>(e.Kind == "go")).Permit(S.B).End()
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.C, new E("go"));

        Assert.False(handled);
        Assert.Equal(S.C, state);
    }

    [Fact]
    public async Task Transition_To_Self_State_No_Exit_Enter()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnEnter(async (_, _) => log.Add("enter:A"))
                .OnExit(async (_, _) => log.Add("exit:A"))
                .When((e, _) => new ValueTask<bool>(e.Kind == "self")).Permit(S.A).Do(async (_, _) => log.Add("effect"))
            )
            .Build();

        await m.TryTransitionAsync(S.A, new E("self"));

        Assert.Single(log);
        Assert.Equal("effect", log[0]);
    }

    [Fact]
    public async Task Cancellation_Token_Propagates()
    {
        var cts = new CancellationTokenSource();
        var tokenReceived = false;

        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When(async (e, ct) =>
                {
                    tokenReceived = ct == cts.Token;
                    return e.Kind == "go";
                }).Permit(S.B).End()
            )
            .Build();

        await m.TryTransitionAsync(S.A, new E("go"), cts.Token);

        Assert.True(tokenReceived);
    }

    [Fact]
    public async Task WhenBuilder_Permit_Then_Stay_Override()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((e, _) => new ValueTask<bool>(e.Kind == "test")).Permit(S.B).Stay().Do(async (_, _) => log.Add("stayed"))
            )
            .Build();

        var (_, state) = await m.TryTransitionAsync(S.A, new E("test"));

        Assert.Equal(S.A, state);
        Assert.Equal("stayed", log[0]);
    }

    [Fact]
    public async Task Default_Effect_Only_On_Stay()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit(async (_, _) => log.Add("exit"))
                .Otherwise().Stay().Do(async (_, _) => log.Add("default-effect"))
            )
            .Build();

        await m.TryTransitionAsync(S.A, new E("any"));

        Assert.Single(log);
        Assert.Equal("default-effect", log[0]);
    }

    [Fact]
    public async Task Null_Hook_Is_Ignored()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnEnter(null!)
                .OnExit(null!)
                .When((e, _) => new ValueTask<bool>(e.Kind == "go")).Permit(S.B).End()
            )
            .InState(S.B, s => s
                .OnEnter(async (_, _) => log.Add("enter:B"))
            )
            .Build();

        await m.TryTransitionAsync(S.A, new E("go"));

        Assert.Single(log);
    }

    [Fact]
    public async Task NoDefault_NoMatch_Returns_False()
    {
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((e, _) => new ValueTask<bool>(e.Kind == "specific")).Permit(S.B).End()
            )
            .Build();

        var (handled, _) = await m.TryTransitionAsync(S.A, new E("other"));

        Assert.False(handled);
    }

    [Fact]
    public async Task AsDefault_Without_Effect()
    {
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .Otherwise().Permit(S.B).AsDefault()
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("any"));

        Assert.True(handled);
        Assert.Equal(S.B, state);
    }

    [Fact]
    public async Task Transition_To_Unconfigured_State_Works()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit(async (_, _) => log.Add("exit:A"))
                .When((e, _) => new ValueTask<bool>(e.Kind == "go")).Permit(S.D).Do(async (_, _) => log.Add("effect:go"))
            )
            // S.D is NOT configured - tests the path where nextCfg is null
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("go"));

        Assert.True(handled);
        Assert.Equal(S.D, state);
        Assert.Equal(2, log.Count);
        Assert.Equal("exit:A", log[0]);
        Assert.Equal("effect:go", log[1]);
        // No OnEnter for S.D since it's not configured
    }

    [Fact]
    public async Task Default_Transition_To_Unconfigured_State()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit(async (_, _) => log.Add("exit:A"))
                .Otherwise().Permit(S.D).Do(async (_, _) => log.Add("default:go-to-D"))
            )
            // S.D is NOT configured
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("anything"));

        Assert.True(handled);
        Assert.Equal(S.D, state);
        Assert.Equal(2, log.Count);
        Assert.Equal("exit:A", log[0]);
        Assert.Equal("default:go-to-D", log[1]);
    }

    [Fact]
    public async Task Default_Transition_Without_Effect()
    {
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .Otherwise().Permit(S.B).AsDefault()
            )
            .InState(S.B, s => s
                .OnEnter(async (_, _) => { }) // Has OnEnter but no effect in transition
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("any"));

        Assert.True(handled);
        Assert.Equal(S.B, state);
    }

    [Fact]
    public async Task Default_Stay_Without_Effect()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .OnExit(async (_, _) => log.Add("exit"))
                .Otherwise().Stay().AsDefault()
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("any"));

        Assert.True(handled);
        Assert.Equal(S.A, state);
        Assert.Empty(log); // No exit because we stayed
    }

    [Fact]
    public async Task Edge_Effect_Only_On_Stay()
    {
        var log = new List<string>();
        var m = AsyncStateMachine<S, E>.Create()
            .InState(S.A, s => s
                .When((e, _) => new ValueTask<bool>(e.Kind == "stay")).Stay().Do(async (_, _) => log.Add("stayed"))
            )
            .Build();

        var (handled, state) = await m.TryTransitionAsync(S.A, new E("stay"));

        Assert.True(handled);
        Assert.Equal(S.A, state);
        Assert.Single(log);
        Assert.Equal("stayed", log[0]);
    }
}

#endregion
