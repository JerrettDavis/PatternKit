# Async State Machine — Connection Lifecycle

A small, production‑shaped demo of AsyncStateMachine managing a connection lifecycle. It shows async entry/exit hooks, async effects, and default stay behavior.

What you’ll see
- Declarative states and transitions using When → Permit/Stay → Do.
- Exit → Effect → Enter order on cross‑state transitions.
- Default Stay that ignores unknown events while connected.

Code (excerpt)
```csharp
using PatternKit.Behavioral.State;

public static class ConnectionStateDemo
{
    public enum Mode { Disconnected, Connecting, Connected, Error }
    public readonly record struct NetEvent(string Kind);

    public static async ValueTask<(Mode Final, List<string> Log)> RunAsync(params string[] events)
    {
        var log = new List<string>();
        var m = AsyncStateMachine<Mode, NetEvent>.Create()
            .InState(Mode.Disconnected, s => s
                .OnExit(async (e, ct) => { await Task.Yield(); log.Add("exit:Disconnected"); })
                .When((e, ct) => new ValueTask<bool>(e.Kind == "connect")).Permit(Mode.Connecting).Do(async (e, ct) => { await Task.Delay(1, ct); log.Add("effect:dial"); })
            )
            .InState(Mode.Connecting, s => s
                .OnEnter(async (e, ct) => { await Task.Yield(); log.Add("enter:Connecting"); })
                .OnExit(async (e, ct) => { await Task.Yield(); log.Add("exit:Connecting"); })
                .When((e, ct) => new ValueTask<bool>(e.Kind == "ok")).Permit(Mode.Connected).Do(async (e, ct) => { await Task.Yield(); log.Add("effect:handshake"); })
                .When((e, ct) => new ValueTask<bool>(e.Kind == "fail")).Permit(Mode.Error).Do(async (e, ct) => { await Task.Yield(); log.Add("effect:cleanup"); })
            )
            .InState(Mode.Connected, s => s
                .OnEnter(async (e, ct) => { await Task.Yield(); log.Add("enter:Connected"); })
                .Otherwise().Stay().Do(async (e, ct) => { await Task.Yield(); log.Add("effect:noop"); })
            )
            .Build();

        var state = Mode.Disconnected;
        foreach (var k in events)
        {
            var (_, next) = await m.TryTransitionAsync(state, new NetEvent(k));
            state = next;
        }
        return (state, log);
    }
}
```

How to run
```bat
rem Build everything (Debug)
dotnet build PatternKit.slnx -c Debug

rem Run tests (includes this demo)
dotnet test PatternKit.slnx -c Debug --filter FullyQualifiedName~AsyncStateDemo
```

Relevant files
- src/PatternKit.Examples/AsyncStateDemo/AsyncStateDemo.cs
- test/PatternKit.Examples.Tests/AsyncStateDemo/AsyncStateDemoTests.cs
- docs/patterns/behavioral/state/state.md — pattern reference (sync + async)
# State Machine (Sync and Async)

A fluent, allocation‑light state machine with entry/exit hooks, optional default transitions, and a predictable first‑match rule order. Ships in two forms:
- StateMachine<TState, TEvent> — synchronous delegates
- AsyncStateMachine<TState, TEvent> — async predicates/effects/hooks

Design notes
- Immutable after Build(); share safely across threads.
- You hold the current state; pass it to the machine per call.
- First matching When rule in registration order wins; optional per‑state Otherwise default.
- Exit → Effect → Enter ordering for cross‑state transitions; Stay executes effect only.

Quick start (sync)
```csharp
var m = StateMachine<OrderState, OrderEvent>.Create()
    .InState(OrderState.New, s => s
        .OnExit((in OrderEvent _) => log.Add("audit:new->"))
        .When(static (in OrderEvent e) => e.Kind == "pay").Permit(OrderState.Paid).Do((in OrderEvent _) => log.Add("charge"))
        .When(static (in OrderEvent e) => e.Kind == "cancel").Permit(OrderState.Cancelled).Do((in OrderEvent _) => log.Add("cancel"))
    )
    .InState(OrderState.Paid, s => s
        .OnEnter((in OrderEvent _) => log.Add("notify:paid"))
        .When(static (in OrderEvent e) => e.Kind == "ship").Permit(OrderState.Shipped).Do((in OrderEvent _) => log.Add("ship"))
    )
    .Build();

var state = OrderState.New;
m.TryTransition(ref state, in new OrderEvent("pay"));
```

Quick start (async)
```csharp
var m = AsyncStateMachine<State, Ev>.Create()
    .InState(State.Idle, s => s
        .OnExit(async (e, ct) => { await Task.Yield(); log.Add("exit:Idle"); })
        .When((e, ct) => new ValueTask<bool>(e.Kind == "go")).Permit(State.Active).Do(async (e, ct) => { await Task.Delay(1, ct); log.Add("effect:go"); })
    )
    .InState(State.Active, s => s
        .OnEnter(async (e, ct) => { await Task.Yield(); log.Add("enter:Active"); })
    )
    .Build();

var (handled, next) = await m.TryTransitionAsync(State.Idle, new Ev("go"));
```

Semantics
- Cross‑state: exit hooks of current, then effect, then entry hooks of next.
- Stay/internal: effect only; entry/exit hooks do not run.
- Default per state: .Otherwise().Permit(...) or .Otherwise().Stay() when no predicate matches.
- Comparer: supply a custom equality comparer via .Comparer(...) in the builder when needed.

API summary
- StateMachine<TState, TEvent>
  - bool TryTransition(ref TState state, in TEvent @event)
  - void Transition(ref TState state, in TEvent @event)
- AsyncStateMachine<TState, TEvent>
  - ValueTask<(bool handled, TState state)> TryTransitionAsync(TState state, TEvent @event, CancellationToken ct = default)
  - ValueTask<TState> TransitionAsync(TState state, TEvent @event, CancellationToken ct = default)

Examples
- docs/examples/state-machine.md — order lifecycle (sync)
- docs/examples/async-state-machine.md — connection lifecycle (async)

Tips
- Prefer enums or interned strings for TState; or pass a comparer.
- Keep predicates cheap; effects are a good place for side‑effects.
- Async flavor avoids ref parameters by returning the updated state.

