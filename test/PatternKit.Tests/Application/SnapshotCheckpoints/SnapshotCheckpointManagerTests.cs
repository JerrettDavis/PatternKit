using PatternKit.Application.SnapshotCheckpoints;
using TinyBDD;

namespace PatternKit.Tests.Application.SnapshotCheckpoints;

public sealed class SnapshotCheckpointManagerTests
{
    [Scenario("Snapshot checkpoint manager saves and loads a usable checkpoint")]
    [Fact]
    public void Snapshot_Checkpoint_Manager_Saves_And_Loads_A_Usable_Checkpoint()
    {
        var now = new DateTimeOffset(2026, 5, 30, 9, 0, 0, TimeSpan.Zero);
        var manager = SnapshotCheckpointManager<string, OrderSnapshot>.Create("order-checkpoints")
            .WithClock(() => now)
            .Build();

        var save = manager.Save("ORDER-1", 5, new OrderSnapshot("ORDER-1", 125m), "REQ-1", new Dictionary<string, string> { ["source"] = "test" });
        var load = manager.Load("ORDER-1", 4);

        ScenarioExpect.True(save.IsSaved);
        ScenarioExpect.Equal(SnapshotCheckpointSaveStatus.Saved, save.Status);
        ScenarioExpect.Null(save.PreviousCheckpoint);
        ScenarioExpect.True(load.IsUsable);
        ScenarioExpect.Equal(SnapshotCheckpointLoadStatus.Found, load.Status);
        ScenarioExpect.Equal("order-checkpoints", manager.Name);
        ScenarioExpect.Equal("ORDER-1", load.Checkpoint!.Key);
        ScenarioExpect.Equal(5, load.Checkpoint.Version);
        ScenarioExpect.Equal(now, load.Checkpoint.SavedAt);
        ScenarioExpect.Equal("REQ-1", load.Checkpoint.CorrelationId);
        ScenarioExpect.Equal("test", load.Checkpoint.Metadata["source"]);
    }

    [Scenario("Snapshot checkpoint manager reports missing and stale checkpoints")]
    [Fact]
    public void Snapshot_Checkpoint_Manager_Reports_Missing_And_Stale_Checkpoints()
    {
        var manager = SnapshotCheckpointManager<string, OrderSnapshot>.Create().Build();
        manager.Save("ORDER-1", 3, new OrderSnapshot("ORDER-1", 75m));

        var missing = manager.Load("ORDER-2", 1);
        var stale = manager.Load("ORDER-1", 4);

        ScenarioExpect.Equal(SnapshotCheckpointLoadStatus.Missing, missing.Status);
        ScenarioExpect.Equal("ORDER-2", missing.Key);
        ScenarioExpect.Equal(1, missing.MinimumVersion);
        ScenarioExpect.Null(missing.Checkpoint);
        ScenarioExpect.False(missing.IsUsable);
        ScenarioExpect.Equal(SnapshotCheckpointLoadStatus.Stale, stale.Status);
        ScenarioExpect.Equal(3, stale.Checkpoint!.Version);
        ScenarioExpect.Equal(4, stale.MinimumVersion);
        ScenarioExpect.False(stale.IsUsable);
    }

    [Scenario("Snapshot checkpoint manager rejects stale writes by default")]
    [Fact]
    public void Snapshot_Checkpoint_Manager_Rejects_Stale_Writes_By_Default()
    {
        var manager = SnapshotCheckpointManager<string, OrderSnapshot>.Create().Build();
        manager.Save("ORDER-1", 10, new OrderSnapshot("ORDER-1", 100m));

        var save = manager.Save("ORDER-1", 9, new OrderSnapshot("ORDER-1", 90m));
        var load = manager.Load("ORDER-1");

        ScenarioExpect.False(save.IsSaved);
        ScenarioExpect.Equal(SnapshotCheckpointSaveStatus.RejectedStale, save.Status);
        ScenarioExpect.Equal(9, save.AttemptedVersion);
        ScenarioExpect.Equal(10, save.Checkpoint!.Version);
        ScenarioExpect.Equal(10, load.Checkpoint!.Version);
    }

    [Scenario("Snapshot checkpoint manager can overwrite stale writes when configured")]
    [Fact]
    public void Snapshot_Checkpoint_Manager_Can_Overwrite_Stale_Writes_When_Configured()
    {
        var manager = SnapshotCheckpointManager<string, OrderSnapshot>.Create()
            .WithStaleWritePolicy(SnapshotCheckpointStaleWritePolicy.Overwrite)
            .UseComparer(StringComparer.OrdinalIgnoreCase)
            .Build();
        manager.Save("ORDER-1", 10, new OrderSnapshot("ORDER-1", 100m));

        var save = manager.Save("order-1", 9, new OrderSnapshot("ORDER-1", 90m));
        var load = manager.Load("ORDER-1");

        ScenarioExpect.True(save.IsSaved);
        ScenarioExpect.NotNull(save.PreviousCheckpoint);
        ScenarioExpect.Equal(10, save.PreviousCheckpoint!.Version);
        ScenarioExpect.Equal(9, load.Checkpoint!.Version);
        ScenarioExpect.Equal(90m, load.Checkpoint.Snapshot.Total);
    }

    [Scenario("Snapshot checkpoint manager state snapshots and removals are stable")]
    [Fact]
    public void Snapshot_Checkpoint_Manager_State_Snapshots_And_Removals_Are_Stable()
    {
        var manager = SnapshotCheckpointManager<string, OrderSnapshot>.Create("stateful").Build();
        manager.Save("ORDER-2", 2, new OrderSnapshot("ORDER-2", 20m));
        manager.Save("ORDER-1", 1, new OrderSnapshot("ORDER-1", 10m));

        var state = manager.GetState();
        var removed = manager.Remove("ORDER-1");
        var missingRemove = manager.Remove("ORDER-1");

        ScenarioExpect.Equal("stateful", state.Name);
        ScenarioExpect.Equal(2, state.Count);
        ScenarioExpect.Equal(["ORDER-1", "ORDER-2"], state.Checkpoints.Select(static checkpoint => checkpoint.Key).ToArray());
        ScenarioExpect.True(removed);
        ScenarioExpect.False(missingRemove);
        ScenarioExpect.Equal(1, manager.Count);
    }

    [Scenario("Snapshot checkpoint manager rejects invalid configuration")]
    [Fact]
    public void Snapshot_Checkpoint_Manager_Rejects_Invalid_Configuration()
    {
        var manager = SnapshotCheckpointManager<string, OrderSnapshot>.Create().Build();
        var snapshot = new OrderSnapshot("ORDER-1", 10m);

        ScenarioExpect.Throws<ArgumentException>(() => SnapshotCheckpointManager<string, OrderSnapshot>.Create("").Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => SnapshotCheckpointManager<string, OrderSnapshot>.Create().UseComparer(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => SnapshotCheckpointManager<string, OrderSnapshot>.Create().WithClock(null!));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => SnapshotCheckpointManager<string, OrderSnapshot>.Create().WithStaleWritePolicy((SnapshotCheckpointStaleWritePolicy)99));
        ScenarioExpect.Throws<ArgumentNullException>(() => manager.Save(null!, 1, snapshot));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => manager.Save("ORDER-1", -1, snapshot));
        ScenarioExpect.Throws<ArgumentNullException>(() => manager.Save("ORDER-1", 1, null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => manager.Load(null!));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => manager.Load("ORDER-1", -1));
        ScenarioExpect.Throws<ArgumentNullException>(() => manager.Remove(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new SnapshotCheckpoint<string, OrderSnapshot>("ORDER-1", 1, snapshot, DateTimeOffset.UtcNow, null, null!));
        ScenarioExpect.Throws<ArgumentException>(() => new SnapshotCheckpointManagerState<string, OrderSnapshot>("", 0, []));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => new SnapshotCheckpointManagerState<string, OrderSnapshot>("state", -1, []));
        ScenarioExpect.Throws<ArgumentNullException>(() => new SnapshotCheckpointManagerState<string, OrderSnapshot>("state", 0, null!));
    }

    private sealed record OrderSnapshot(string OrderId, decimal Total);
}
