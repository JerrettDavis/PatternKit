using PatternKit.Application.Timeouts;
using TinyBDD;

namespace PatternKit.Tests.Application.Timeouts;

public sealed class TimeoutManagerTests
{
    [Scenario("Timeout manager schedules completes and expires deadlines")]
    [Fact]
    public void Timeout_Manager_Schedules_Completes_And_Expires_Deadlines()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var manager = TimeoutManager<string>.Create("reservations")
            .WithClock(() => now)
            .Build();

        manager.ScheduleAfter("ORDER-1", TimeSpan.FromMinutes(5), "REQ-1");
        manager.ScheduleAfter("ORDER-2", TimeSpan.FromMinutes(10), "REQ-2");

        ScenarioExpect.Equal(2, manager.PendingCount);
        ScenarioExpect.True(manager.Complete("ORDER-2"));
        ScenarioExpect.False(manager.Complete("ORDER-2"));

        var expired = manager.ExpireDue(now.AddMinutes(6));

        var timeout = ScenarioExpect.Single(expired);
        ScenarioExpect.Equal("ORDER-1", timeout.Key);
        ScenarioExpect.Equal("REQ-1", timeout.CorrelationId);
        ScenarioExpect.Equal(now, timeout.ScheduledAt);
        ScenarioExpect.Equal(now.AddMinutes(5), timeout.Deadline);
        ScenarioExpect.Equal(0, manager.PendingCount);
    }

    [Scenario("Timeout manager supports immediate deadlines")]
    [Fact]
    public void Timeout_Manager_Supports_Immediate_Deadlines()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var manager = TimeoutManager<string>.Create().WithClock(() => now).Build();

        var timeout = manager.ScheduleAfter("ORDER-1", TimeSpan.Zero, "REQ-1");
        var expired = manager.ExpireDue(now);

        ScenarioExpect.Equal(now, timeout.ScheduledAt);
        ScenarioExpect.Equal(now, timeout.Deadline);
        ScenarioExpect.Equal("ORDER-1", ScenarioExpect.Single(expired).Key);
        ScenarioExpect.Equal(0, manager.PendingCount);
    }

    [Scenario("Timeout manager replaces existing deadlines by key")]
    [Fact]
    public void Timeout_Manager_Replaces_Existing_Deadlines_By_Key()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var manager = TimeoutManager<string>.Create()
            .WithClock(() => now)
            .WithKeyComparer(StringComparer.OrdinalIgnoreCase)
            .Build();

        manager.ScheduleAfter("ORDER-1", TimeSpan.FromMinutes(5), "first");
        manager.Schedule("order-1", now.AddMinutes(15), "second");

        var timeout = ScenarioExpect.Single(manager.Snapshot());
        ScenarioExpect.Equal("second", timeout.CorrelationId);
        ScenarioExpect.Equal(now.AddMinutes(15), timeout.Deadline);
    }

    [Scenario("Timeout manager state orders pending deadlines")]
    [Fact]
    public void Timeout_Manager_State_Orders_Pending_Deadlines()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var manager = TimeoutManager<string>.Create("reservation-timeouts")
            .WithClock(() => now)
            .Build();

        manager.ScheduleAfter("late", TimeSpan.FromMinutes(30));
        manager.ScheduleAfter("early", TimeSpan.FromMinutes(2));

        var state = manager.GetState();

        ScenarioExpect.Equal("reservation-timeouts", state.ManagerName);
        ScenarioExpect.Equal(2, state.PendingCount);
        ScenarioExpect.Equal(["early", "late"], state.PendingTimeouts.Select(static timeout => timeout.Key).ToArray());
        ScenarioExpect.True(state.PendingTimeouts[0].IsDue(now.AddMinutes(3)));
    }

    [Scenario("Timeout manager cancels pending deadlines")]
    [Fact]
    public void Timeout_Manager_Cancels_Pending_Deadlines()
    {
        var manager = TimeoutManager<string>.Create().Build();

        manager.ScheduleAfter("ORDER-1", TimeSpan.FromMinutes(5));

        ScenarioExpect.True(manager.Cancel("ORDER-1"));
        ScenarioExpect.False(manager.Cancel("ORDER-1"));
        ScenarioExpect.Empty(manager.Snapshot());
    }

    [Scenario("Timeout manager rejects invalid configuration")]
    [Fact]
    public void Timeout_Manager_Rejects_Invalid_Configuration()
    {
        var manager = TimeoutManager<string>.Create().Build();

        ScenarioExpect.Throws<ArgumentException>(() => TimeoutManager<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => TimeoutManager<string>.Create().WithClock(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => TimeoutManager<string>.Create().WithKeyComparer(null!));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => manager.ScheduleAfter("ORDER-1", TimeSpan.FromMilliseconds(-1)));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => manager.Schedule("ORDER-1", DateTimeOffset.UtcNow.AddDays(-1)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new TimeoutManagerState<string>("name", 0, null!));
    }
}
