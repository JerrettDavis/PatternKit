using PatternKit.Behavioral.State;

namespace PatternKit.Examples.AsyncStateDemo;

public static class ConnectionStateDemo
{
    public enum Mode { Disconnected, Connecting, Connected, Error }
    public readonly record struct NetEvent(string Kind);

    public static async ValueTask<(Mode Final, List<string> Log)> RunAsync(params string[] events)
    {
        var log = new List<string>();
        var m = AsyncStateMachine<Mode, NetEvent>.Create()
            .InState(Mode.Disconnected, s => s
                .OnExit(async (_, _) => { await Task.Yield(); log.Add("exit:Disconnected"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "connect")).Permit(Mode.Connecting).Do(async (_, ct) => { await Task.Delay(1, ct); log.Add("effect:dial"); })
            )
            .InState(Mode.Connecting, s => s
                .OnEnter(async (_, _) => { await Task.Yield(); log.Add("enter:Connecting"); })
                .OnExit(async (_, _) => { await Task.Yield(); log.Add("exit:Connecting"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "ok")).Permit(Mode.Connected).Do(async (_, _) => { await Task.Yield(); log.Add("effect:handshake"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "fail")).Permit(Mode.Error).Do(async (_, _) => { await Task.Yield(); log.Add("effect:cleanup"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "cancel")).Permit(Mode.Disconnected).Do(async (_, _) => { await Task.Yield(); log.Add("effect:cancel"); })
            )
            .InState(Mode.Connected, s => s
                .OnEnter(async (_, _) => { await Task.Yield(); log.Add("enter:Connected"); })
                .When((e, _) => new ValueTask<bool>(e.Kind == "drop")).Permit(Mode.Connecting).Do(async (_, _) => { await Task.Yield(); log.Add("effect:reconnect"); })
                .Otherwise().Stay().Do(async (_, _) => { await Task.Yield(); log.Add("effect:noop"); })
            )
            .InState(Mode.Error, s => s
                .OnEnter(async (_, _) => { await Task.Yield(); log.Add("enter:Error"); })
                .Otherwise().Stay().Do(async (_, _) => { await Task.Yield(); log.Add("effect:noop"); })
            )
            .Build();

        var state = Mode.Disconnected;
        foreach (var k in events)
        {
            var e = new NetEvent(k);
            var (_, next) = await m.TryTransitionAsync(state, e);
            state = next;
        }

        return (state, log);
    }
}

