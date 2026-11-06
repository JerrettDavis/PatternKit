using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.State;
using PatternKit.Generators.State;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "State")]
public class StateBenchmarks
{
    [Benchmark(Baseline = true, Description = "Fluent: create state machine")]
    [BenchmarkCategory("Fluent", "Construction")]
    public StateMachine<OrderWorkflowState, OrderWorkflowTrigger> Fluent_CreateStateMachine()
        => StateMachine<OrderWorkflowState, OrderWorkflowTrigger>.Create()
            .InState(OrderWorkflowState.Draft, state => state
                .When(static (in OrderWorkflowTrigger trigger) => trigger == OrderWorkflowTrigger.Submit)
                .Permit(OrderWorkflowState.Submitted)
                .End())
            .InState(OrderWorkflowState.Submitted, state => state
                .When(static (in OrderWorkflowTrigger trigger) => trigger == OrderWorkflowTrigger.Pay)
                .Permit(OrderWorkflowState.Paid)
                .End())
            .Build();

    [Benchmark(Description = "Generated: create state machine")]
    [BenchmarkCategory("Generated", "Construction")]
    public GeneratedOrderWorkflow Generated_CreateStateMachine()
        => new();

    [Benchmark(Description = "Fluent: transition order state")]
    [BenchmarkCategory("Fluent", "Execution")]
    public OrderWorkflowState Fluent_TransitionState()
    {
        var machine = Fluent_CreateStateMachine();
        var state = OrderWorkflowState.Draft;
        var submit = OrderWorkflowTrigger.Submit;
        machine.Transition(ref state, in submit);
        return state;
    }

    [Benchmark(Description = "Generated: transition order state")]
    [BenchmarkCategory("Generated", "Execution")]
    public OrderWorkflowState Generated_TransitionState()
    {
        var machine = new GeneratedOrderWorkflow();
        machine.Fire(OrderWorkflowTrigger.Submit);
        return machine.State;
    }
}

public enum OrderWorkflowState { Draft, Submitted, Paid }

public enum OrderWorkflowTrigger { Submit, Pay }

[StateMachine(typeof(OrderWorkflowState), typeof(OrderWorkflowTrigger))]
public partial class GeneratedOrderWorkflow
{
    [StateTransition(From = OrderWorkflowState.Draft, Trigger = OrderWorkflowTrigger.Submit, To = OrderWorkflowState.Submitted)]
    private void OnSubmit() { }

    [StateTransition(From = OrderWorkflowState.Submitted, Trigger = OrderWorkflowTrigger.Pay, To = OrderWorkflowState.Paid)]
    private void OnPay() { }
}
