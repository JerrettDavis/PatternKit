using PatternKit.Application.ManualTaskGates;
using TinyBDD;

namespace PatternKit.Tests.Application.ManualTaskGates;

public sealed class ManualTaskGateTests
{
    [Scenario("Manual task gate opens and approves blocking work")]
    [Fact]
    public void Manual_Task_Gate_Opens_And_Approves_Blocking_Work()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var gate = ManualTaskGate<string>.Create("checkout-approvals")
            .WithClock(() => now)
            .Build();

        var task = gate.Open("ORDER-1", "Approve high value order", "fraud-team", "REQ-1");

        ScenarioExpect.True(gate.IsBlocked);
        ScenarioExpect.Equal("ORDER-1", task.Key);
        ScenarioExpect.Equal("fraud-team", task.Assignee);
        ScenarioExpect.Equal("REQ-1", task.CorrelationId);
        ScenarioExpect.Equal(ManualTaskStatus.Pending, task.Status);
        ScenarioExpect.True(task.IsBlocking);

        var approved = gate.Approve("ORDER-1", "case-manager", "Looks valid.");

        ScenarioExpect.NotNull(approved);
        ScenarioExpect.Equal(ManualTaskStatus.Approved, approved.Status);
        ScenarioExpect.Equal("case-manager", approved.DecidedBy);
        ScenarioExpect.Equal("Looks valid.", approved.DecisionNote);
        ScenarioExpect.False(gate.IsBlocked);
    }

    [Scenario("Manual task gate tracks rejected canceled and completed work")]
    [Fact]
    public void Manual_Task_Gate_Tracks_Rejected_Canceled_And_Completed_Work()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var gate = ManualTaskGate<string>.Create()
            .WithClock(() => now)
            .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
            .Build();

        gate.Open("ORDER-1", "Review address");
        gate.Open("ORDER-2", "Review payment");

        var rejected = gate.Reject("order-1", "risk-agent");
        var canceled = gate.Cancel("ORDER-2", "scheduler", "expired");

        ScenarioExpect.NotNull(rejected);
        ScenarioExpect.NotNull(canceled);
        ScenarioExpect.Equal(ManualTaskStatus.Rejected, rejected.Status);
        ScenarioExpect.Equal(ManualTaskStatus.Canceled, canceled.Status);
        ScenarioExpect.False(gate.IsBlocked);
        ScenarioExpect.True(gate.Complete("ORDER-1"));
        ScenarioExpect.False(gate.Complete("ORDER-1"));
        ScenarioExpect.Single(gate.Snapshot());
    }

    [Scenario("Manual task gate state reports pending and all tasks")]
    [Fact]
    public void Manual_Task_Gate_State_Reports_Pending_And_All_Tasks()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var gate = ManualTaskGate<string>.Create("review-gate").WithClock(() => now).Build();

        gate.Open("late", "Review late order");
        gate.Open("early", "Review early order");
        gate.Approve("late", "manager");

        var state = gate.GetGateState();

        ScenarioExpect.Equal("review-gate", state.GateName);
        ScenarioExpect.True(state.IsBlocked);
        ScenarioExpect.Equal(1, state.PendingCount);
        ScenarioExpect.Equal("early", ScenarioExpect.Single(state.PendingTasks).Key);
        ScenarioExpect.Equal(["early", "late"], state.AllTasks.Select(static task => task.Key).ToArray());
    }

    [Scenario("Manual task gate rejects invalid configuration")]
    [Fact]
    public void Manual_Task_Gate_Rejects_Invalid_Configuration()
    {
        var gate = ManualTaskGate<string>.Create().Build();
        var record = new ManualTaskRecord<string>("ORDER-1", "Review", null, null, ManualTaskStatus.Pending, DateTimeOffset.UtcNow, null, null, null);

        ScenarioExpect.Throws<ArgumentException>(() => ManualTaskGate<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => ManualTaskGate<string>.Create().WithClock(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => ManualTaskGate<string>.Create().WithKeyComparer(null!));
        ScenarioExpect.Throws<ArgumentException>(() => gate.Open("ORDER-1", ""));
        ScenarioExpect.Throws<ArgumentException>(() => gate.Approve("ORDER-1", ""));
        ScenarioExpect.Throws<ArgumentException>(() => new ManualTaskRecord<string>("ORDER-1", "", null, null, ManualTaskStatus.Pending, DateTimeOffset.UtcNow, null, null, null));
        ScenarioExpect.Throws<ArgumentNullException>(() => new ManualTaskGateState<string>("gate", false, 0, null!, []));
        ScenarioExpect.Throws<ArgumentNullException>(() => new ManualTaskGateState<string>("gate", false, 0, [], null!));
        ScenarioExpect.True(record.IsBlocking);
        ScenarioExpect.Null(gate.Reject("missing", "manager"));
    }
}
