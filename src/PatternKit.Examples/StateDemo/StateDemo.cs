using PatternKit.Behavioral.State;

namespace PatternKit.Examples.StateDemo;

public static class OrderStateDemo
{
    public enum OrderState { New, Paid, Shipped, Delivered, Cancelled, Refunded }
    public readonly record struct OrderEvent(string Kind);

    public static (OrderState Final, List<string> Log) Run(params string[] events)
    {
        var log = new List<string>();
        var machine = StateMachine<OrderState, OrderEvent>.Create()
            .InState(OrderState.New, s => s
                .OnExit((in _) => log.Add("audit:new->"))
                .When(static (in e) => e.Kind == "pay").Permit(OrderState.Paid).Do((in _) => log.Add("charge"))
                .When(static (in e) => e.Kind == "cancel").Permit(OrderState.Cancelled).Do((in _) => log.Add("cancel"))
            )
            .InState(OrderState.Paid, s => s
                .OnEnter((in _) => log.Add("notify:paid"))
                .OnExit((in _) => log.Add("audit:paid->"))
                .When(static (in e) => e.Kind == "ship").Permit(OrderState.Shipped).Do((in _) => log.Add("ship"))
                .When(static (in e) => e.Kind == "cancel").Permit(OrderState.Cancelled).Do((in _) => log.Add("cancel"))
            )
            .InState(OrderState.Shipped, s => s
                .OnEnter((in _) => log.Add("notify:shipped"))
                .OnExit((in _) => log.Add("audit:shipped->"))
                .When(static (in e) => e.Kind == "deliver").Permit(OrderState.Delivered).Do((in _) => log.Add("deliver"))
            )
            .InState(OrderState.Delivered, s => s
                .OnEnter((in _) => log.Add("notify:delivered"))
                .Otherwise().Stay().Do((in _) => log.Add("ignore"))
            )
            .InState(OrderState.Cancelled, s => s
                .OnEnter((in _) => log.Add("notify:cancelled"))
                .When(static (in e) => e.Kind == "refund").Permit(OrderState.Refunded).Do((in _) => log.Add("refund"))
                .Otherwise().Stay().Do((in _) => log.Add("ignore"))
            )
            .InState(OrderState.Refunded, s => s
                .OnEnter((in _) => log.Add("notify:refunded"))
                .Otherwise().Stay().Do((in _) => log.Add("ignore"))
            )
            .Build();

        var state = OrderState.New;
        foreach (var k in events)
        {
            var e = new OrderEvent(k);
            machine.TryTransition(ref state, in e);
        }
        return (state, log);
    }
}

