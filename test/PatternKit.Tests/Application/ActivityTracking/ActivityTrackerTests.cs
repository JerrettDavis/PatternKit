using PatternKit.Application.ActivityTracking;
using TinyBDD;

namespace PatternKit.Tests.Application.ActivityTracking;

public sealed class ActivityTrackerTests
{
    [Scenario("Tracker blocks while activities are active")]
    [Fact]
    public void Tracker_Blocks_While_Activities_Are_Active()
    {
        var tracker = ActivityTracker.Create("dashboard").Build();

        using var customerLoad = tracker.Track("customers", "REQ-100");
        var inventoryLoad = tracker.Track("inventory", "REQ-101");

        ScenarioExpect.True(tracker.IsBlocked);
        ScenarioExpect.Equal(2, tracker.ActiveCount);
        ScenarioExpect.Equal(["customers", "inventory"], tracker.Snapshot().Select(static activity => activity.Name).ToArray());

        ScenarioExpect.True(inventoryLoad.Release());
        ScenarioExpect.True(tracker.IsBlocked);
        ScenarioExpect.Equal(1, tracker.ActiveCount);
    }

    [Scenario("Disposing last lease releases gate")]
    [Fact]
    public void Disposing_Last_Lease_Releases_Gate()
    {
        var tracker = ActivityTracker.Create().Build();

        var lease = tracker.Track("load-products");
        lease.Dispose();

        ScenarioExpect.False(tracker.IsBlocked);
        ScenarioExpect.Equal(0, tracker.ActiveCount);
        ScenarioExpect.False(lease.IsActive);
        ScenarioExpect.False(lease.Release());
    }

    [Scenario("Complete releases an activity by id")]
    [Fact]
    public void Complete_Releases_An_Activity_By_Id()
    {
        var tracker = ActivityTracker.Create("orders").Build();
        var lease = tracker.Track("order-import");

        var completed = tracker.Complete(lease.Activity.Id);

        ScenarioExpect.True(completed);
        ScenarioExpect.False(tracker.IsBlocked);
        ScenarioExpect.False(tracker.Complete(lease.Activity.Id));
    }

    [Scenario("Gate state reports active activity details")]
    [Fact]
    public void Gate_State_Reports_Active_Activity_Details()
    {
        var now = new DateTimeOffset(2026, 5, 26, 10, 0, 0, TimeSpan.Zero);
        var tracker = ActivityTracker.Create("loading-wheel")
            .WithClock(() => now)
            .Build();

        using var lease = tracker.Track("pricing", "REQ-200");
        var state = tracker.GetGateState();

        ScenarioExpect.Equal("loading-wheel", state.TrackerName);
        ScenarioExpect.True(state.IsBlocked);
        ScenarioExpect.Equal(1, state.ActiveCount);
        var activity = ScenarioExpect.Single(state.ActiveActivities);
        ScenarioExpect.Equal("pricing", activity.Name);
        ScenarioExpect.Equal("REQ-200", activity.CorrelationId);
        ScenarioExpect.Equal(now, activity.StartedAt);
    }

    [Scenario("Tracker rejects invalid configuration")]
    [Fact]
    public void Tracker_Rejects_Invalid_Configuration()
    {
        var tracker = ActivityTracker.Create().Build();

        ScenarioExpect.Throws<ArgumentException>(() => ActivityTracker.Create("").Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => ActivityTracker.Create().WithClock(null!));
        ScenarioExpect.Throws<ArgumentException>(() => tracker.Track(""));
        ScenarioExpect.Throws<ArgumentException>(() => tracker.Complete(""));
        ScenarioExpect.Throws<ArgumentException>(() => new ActivityRecord("", "load", null, DateTimeOffset.UtcNow));
        ScenarioExpect.Throws<ArgumentException>(() => new ActivityRecord("1", "", null, DateTimeOffset.UtcNow));
    }
}
