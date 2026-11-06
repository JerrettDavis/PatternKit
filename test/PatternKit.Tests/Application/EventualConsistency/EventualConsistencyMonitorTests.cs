using PatternKit.Application.EventualConsistency;
using TinyBDD;

namespace PatternKit.Tests.Application.EventualConsistency;

public sealed class EventualConsistencyMonitorTests
{
    [Scenario("Eventual consistency monitor reports lagging and converged watermarks")]
    [Fact]
    public void Eventual_Consistency_Monitor_Reports_Lagging_And_Converged_Watermarks()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var monitor = EventualConsistencyMonitor<string>.Create("orders")
            .WithClock(() => now)
            .WithMaxAllowedLag(1)
            .Build();

        monitor.RecordSource("ORDER-1", 10, "source");
        var lagging = monitor.RecordTarget("ORDER-1", 8, "target");
        var converged = monitor.RecordTarget("ORDER-1", 9, "target");

        ScenarioExpect.Equal(EventualConsistencyStatus.Lagging, lagging.Status);
        ScenarioExpect.Equal(2, lagging.Lag);
        ScenarioExpect.False(lagging.IsConverged);
        ScenarioExpect.Equal(EventualConsistencyStatus.Converged, converged.Status);
        ScenarioExpect.Equal(1, converged.Lag);
        ScenarioExpect.True(converged.IsConverged);
        ScenarioExpect.Equal(now, converged.Watermarks!.TargetObservedAt);
        ScenarioExpect.Equal("target", converged.Watermarks.CorrelationId);
    }

    [Scenario("Eventual consistency monitor reports missing sides and unknown streams")]
    [Fact]
    public void Eventual_Consistency_Monitor_Reports_Missing_Sides_And_Unknown_Streams()
    {
        var monitor = EventualConsistencyMonitor<string>.Create().Build();

        var unknown = monitor.Evaluate("missing");
        var missingTarget = monitor.RecordSource("ORDER-1", 3);
        var missingSource = monitor.RecordTarget("ORDER-2", 2);

        ScenarioExpect.Equal(EventualConsistencyStatus.Unknown, unknown.Status);
        ScenarioExpect.Null(unknown.Watermarks);
        ScenarioExpect.Equal(EventualConsistencyStatus.MissingTarget, missingTarget.Status);
        ScenarioExpect.Equal(3, missingTarget.Watermarks!.SourceWatermark);
        ScenarioExpect.Equal(EventualConsistencyStatus.MissingSource, missingSource.Status);
        ScenarioExpect.Equal(2, missingSource.Watermarks!.TargetWatermark);
    }

    [Scenario("Eventual consistency monitor records source and target in one call")]
    [Fact]
    public void Eventual_Consistency_Monitor_Records_Source_And_Target_In_One_Call()
    {
        var monitor = EventualConsistencyMonitor<string>.Create("orders").WithMaxAllowedLag(0).Build();

        var evaluation = monitor.Record("ORDER-1", 5, 5, "sync");

        ScenarioExpect.Equal("orders", evaluation.MonitorName);
        ScenarioExpect.Equal(EventualConsistencyStatus.Converged, evaluation.Status);
        ScenarioExpect.Equal(0, evaluation.Lag);
        ScenarioExpect.Equal(5, evaluation.Watermarks!.SourceWatermark);
        ScenarioExpect.Equal(5, evaluation.Watermarks.TargetWatermark);
    }

    [Scenario("Eventual consistency monitor snapshots state")]
    [Fact]
    public void Eventual_Consistency_Monitor_Snapshots_State()
    {
        var monitor = EventualConsistencyMonitor<string>.Create("state").UseComparer(StringComparer.OrdinalIgnoreCase).Build();
        monitor.Record("ORDER-1", 5, 4);
        monitor.Record("order-2", 9, 1);

        var state = monitor.GetState();

        ScenarioExpect.Equal("state", state.Name);
        ScenarioExpect.Equal(0, state.MaxAllowedLag);
        ScenarioExpect.Equal(2, state.Count);
        ScenarioExpect.Equal([1, 8], state.Evaluations.Select(static evaluation => evaluation.Lag).ToArray());
        ScenarioExpect.Equal(2, monitor.Count);
    }

    [Scenario("Eventual consistency monitor rejects invalid configuration")]
    [Fact]
    public void Eventual_Consistency_Monitor_Rejects_Invalid_Configuration()
    {
        var monitor = EventualConsistencyMonitor<string>.Create().Build();

        ScenarioExpect.Throws<ArgumentException>(() => EventualConsistencyMonitor<string>.Create("").Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => EventualConsistencyMonitor<string>.Create().WithMaxAllowedLag(-1));
        ScenarioExpect.Throws<ArgumentNullException>(() => EventualConsistencyMonitor<string>.Create().UseComparer(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => EventualConsistencyMonitor<string>.Create().WithClock(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => monitor.RecordSource(null!, 1));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => monitor.RecordSource("ORDER-1", -1));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => monitor.RecordTarget("ORDER-1", -1));
        ScenarioExpect.Throws<ArgumentNullException>(() => monitor.Evaluate(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new EventualConsistencyWatermarks<string>(null!, null, null, null, null, null, null));
        ScenarioExpect.Throws<ArgumentException>(() => new EventualConsistencyMonitorState<string>("", 0, 0, []));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => new EventualConsistencyMonitorState<string>("state", -1, 0, []));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => new EventualConsistencyMonitorState<string>("state", 0, -1, []));
        ScenarioExpect.Throws<ArgumentNullException>(() => new EventualConsistencyMonitorState<string>("state", 0, 0, null!));
    }
}
