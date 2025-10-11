# State Machine — Order Lifecycle

A small, production‑shaped demo of a fluent State Machine managing an order’s lifecycle. It shows entry/exit hooks, transition effects, and default per‑state behavior.

What you’ll see
- Declarative states and transitions using When → Permit/Stay → Do.
- Entry/exit hooks for notifications and audits.
- Default stay transitions that “ignore” unknown events in terminal states.
- Immutable machine shared across calls; you pass state by ref.

---
## Code
```csharp
using PatternKit.Behavioral.State;

public static class OrderStateDemo
{
    public enum OrderState { New, Paid, Shipped, Delivered, Cancelled, Refunded }
    public readonly record struct OrderEvent(string Kind);

    public static (OrderState Final, List<string> Log) Run(params string[] events)
    {
        var log = new List<string>();
        var machine = StateMachine<OrderState, OrderEvent>.Create()
            .InState(OrderState.New, s => s
                .OnExit((in OrderEvent _) => log.Add("audit:new->"))
                .When(static (in OrderEvent e) => e.Kind == "pay").Permit(OrderState.Paid).Do((in OrderEvent _) => log.Add("charge"))
                .When(static (in OrderEvent e) => e.Kind == "cancel").Permit(OrderState.Cancelled).Do((in OrderEvent _) => log.Add("cancel"))
            )
            .InState(OrderState.Paid, s => s
                .OnEnter((in OrderEvent _) => log.Add("notify:paid"))
                .OnExit((in OrderEvent _) => log.Add("audit:paid->"))
                .When(static (in OrderEvent e) => e.Kind == "ship").Permit(OrderState.Shipped).Do((in OrderEvent _) => log.Add("ship"))
            )
            .InState(OrderState.Shipped, s => s
                .OnEnter((in OrderEvent _) => log.Add("notify:shipped"))
                .OnExit((in OrderEvent _) => log.Add("audit:shipped->"))
                .When(static (in OrderEvent e) => e.Kind == "deliver").Permit(OrderState.Delivered).Do((in OrderEvent _) => log.Add("deliver"))
            )
            .InState(OrderState.Delivered, s => s
                .OnEnter((in OrderEvent _) => log.Add("notify:delivered"))
                .Otherwise().Stay().Do((in OrderEvent _) => log.Add("ignore"))
            )
            .InState(OrderState.Cancelled, s => s
                .OnEnter((in OrderEvent _) => log.Add("notify:cancelled"))
                .When(static (in OrderEvent e) => e.Kind == "refund").Permit(OrderState.Refunded).Do((in OrderEvent _) => log.Add("refund"))
                .Otherwise().Stay().Do((in OrderEvent _) => log.Add("ignore"))
            )
            .InState(OrderState.Refunded, s => s
                .OnEnter((in OrderEvent _) => log.Add("notify:refunded"))
                .Otherwise().Stay().Do((in OrderEvent _) => log.Add("ignore"))
            )
            .Build();

        var state = OrderState.New;
        foreach (var k in events)
            machine.TryTransition(ref state, in new OrderEvent(k));

        return (state, log);
    }
}
```

---
## Behavior
- pay → ship → deliver takes you New → Paid → Shipped → Delivered.
- cancel → refund takes you New → Cancelled → Refunded.
- Delivered ignores further events using a default Stay() rule.

---
## How to run
From the repo root:

```bash
rem Build everything (Release)
dotnet build PatternKit.slnx -c Release

rem Run tests for examples (includes this demo)
dotnet test PatternKit.slnx -c Release --filter FullyQualifiedName~StateDemo
```

Relevant files
- src/PatternKit.Examples/StateDemo/StateDemo.cs — the demo machine.
- test/PatternKit.Examples.Tests/StateDemo/StateDemoTests.cs — specs for paths and logs.
- docs/patterns/behavioral/state/state.md — the pattern reference.

---
## Tips
- Entry/exit hooks run on cross‑state transitions only (not Stay()).
- Effects run between exit and entry; useful for audit/side‑effects.
- Prefer enums or interned strings for TState when identity matters; you can also supply a custom comparer via .Comparer(...).

