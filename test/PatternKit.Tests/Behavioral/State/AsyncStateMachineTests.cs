using PatternKit.Behavioral.State;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

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
                .OnExit(async (e, ct) => { await Task.Yield(); log.Add("exit:Idle"); })
                .When((e, ct) => new ValueTask<bool>(e.Kind == "go")).Permit(S.Active).Do(async (e, ct) => { await Task.Delay(1, ct); log.Add("effect:go"); })
                .When((e, ct) => new ValueTask<bool>(e.Kind == "panic")).Permit(S.Alarm).Do(async (e, ct) => { await Task.Yield(); log.Add("effect:panic"); })
            )
            .InState(S.Active, s => s
                .OnEnter(async (e, ct) => { await Task.Yield(); log.Add("enter:Active"); })
                .OnExit(async (e, ct) => { await Task.Yield(); log.Add("exit:Active"); })
                .When((e, ct) => new ValueTask<bool>(e.Kind == "ping")).Stay().Do(async (e, ct) => { await Task.Yield(); log.Add("effect:ping"); })
                .When((e, ct) => new ValueTask<bool>(e.Kind == "stop")).Permit(S.Idle).Do(async (e, ct) => { await Task.Yield(); log.Add("effect:stop"); })
            )
            .InState(S.Alarm, s => s
                .OnEnter(async (e, ct) => { await Task.Yield(); log.Add("enter:Alarm"); })
                .When((e, ct) => new ValueTask<bool>(e.Kind == "reset")).Permit(S.Idle).Do(async (e, ct) => { await Task.Yield(); log.Add("effect:reset"); })
                .Otherwise().Stay().Do(async (e, ct) => { await Task.Yield(); log.Add("effect:default"); })
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
