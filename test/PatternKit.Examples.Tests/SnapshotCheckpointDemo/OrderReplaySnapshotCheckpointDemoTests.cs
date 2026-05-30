using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.EventSourcing;
using PatternKit.Application.SnapshotCheckpoints;
using PatternKit.Examples.SnapshotCheckpointDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.SnapshotCheckpointDemo;

[Feature("Order Replay Snapshot Checkpoint demo")]
public sealed partial class OrderReplaySnapshotCheckpointDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Order Replay Snapshot Checkpoint demo resumes replay through fluent and generated paths")]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Order_Replay_Snapshot_Checkpoint_Demo_Resumes_Replay_Through_Fluent_And_Generated_Paths(bool sourceGenerated)
        => Given("the order replay snapshot checkpoint demo", () => sourceGenerated)
        .When("the selected path runs", (Func<bool, ValueTask<OrderReplaySummary>>)(async generated =>
            generated
                ? await OrderReplaySnapshotCheckpointDemo.RunGeneratedAsync()
                : await OrderReplaySnapshotCheckpointDemo.RunFluentAsync()))
        .Then("the replay saves a checkpointed shipped order", summary =>
        {
            ScenarioExpect.Equal("order-replay-checkpoints", summary.ManagerName);
            ScenarioExpect.Equal(0, summary.StartingVersion);
            ScenarioExpect.Equal(3, summary.FinalVersion);
            ScenarioExpect.False(summary.UsedCheckpoint);
            ScenarioExpect.True(summary.RebuiltCheckpoint);
            ScenarioExpect.True(summary.Shipped);
            ScenarioExpect.Equal(125m, summary.PaidTotal);
            ScenarioExpect.False(string.IsNullOrWhiteSpace(summary.OrderId));
        })
        .AssertPassed();

    [Scenario("Order Replay Snapshot Checkpoint demo reuses a valid checkpoint")]
    [Fact]
    public Task Order_Replay_Snapshot_Checkpoint_Demo_Reuses_A_Valid_Checkpoint()
        => Given("a seeded event store and checkpoint manager", () =>
        {
            var store = OrderReplaySnapshotCheckpointDemo.CreateSeededStore();
            var manager = OrderReplaySnapshotCheckpointPolicies.CreateFluentManager();
            manager.Save("order-100", 2, new OrderReplaySnapshot("order-100", "customer-1", 125m, 125m, false, 2));
            return new OrderReplayService(store, manager);
        })
        .When("the replay only needs events after the checkpoint", (Func<OrderReplayService, ValueTask<OrderReplaySummary>>)(service =>
            service.ReplayAsync("order-100", 2)))
        .Then("the replay starts from the checkpoint and advances to the latest version", summary =>
        {
            ScenarioExpect.True(summary.UsedCheckpoint);
            ScenarioExpect.False(summary.RebuiltCheckpoint);
            ScenarioExpect.Equal(2, summary.StartingVersion);
            ScenarioExpect.Equal(3, summary.FinalVersion);
            ScenarioExpect.True(summary.Shipped);
        })
        .AssertPassed();

    [Scenario("Order Replay Snapshot Checkpoint demo is importable through IServiceCollection")]
    [Fact]
    public Task Order_Replay_Snapshot_Checkpoint_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service provider with the order replay snapshot checkpoint demo", () =>
        {
            var services = new ServiceCollection();
            services.AddOrderReplaySnapshotCheckpointDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("a scoped workflow replays an order", (Func<ServiceProvider, ValueTask<OrderReplaySummary>>)(async provider =>
        {
            using (provider)
            using (var scope = provider.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<OrderReplayService>();
                return await service.ReplayAsync("order-200", 1);
            }
        }))
        .Then("the imported manager and event store checkpoint the replay", summary =>
        {
            ScenarioExpect.Equal("order-replay-checkpoints", summary.ManagerName);
            ScenarioExpect.Equal("order-200", summary.OrderId);
            ScenarioExpect.Equal(3, summary.FinalVersion);
            ScenarioExpect.True(summary.RebuiltCheckpoint);
            ScenarioExpect.True(summary.Shipped);
        })
        .AssertPassed();

    [Scenario("Order Replay Snapshot Checkpoint generated factory creates the same manager")]
    [Fact]
    public void Order_Replay_Snapshot_Checkpoint_Generated_Factory_Creates_The_Same_Manager()
    {
        var manager = GeneratedOrderReplayCheckpoints.CreateManager();

        ScenarioExpect.Equal("order-replay-checkpoints", manager.Name);
        ScenarioExpect.IsType<SnapshotCheckpointManager<string, OrderReplaySnapshot>>(manager);
    }

    [Scenario("Order Replay Snapshot Checkpoint seeded store contains replay events")]
    [Fact]
    public Task Order_Replay_Snapshot_Checkpoint_Seeded_Store_Contains_Replay_Events()
        => Given("the seeded replay store", () => OrderReplaySnapshotCheckpointDemo.CreateSeededStore())
        .When("reading a seeded stream", (Func<IEventStore<OrderReplayEvent, string>, ValueTask<IReadOnlyList<StoredEvent<OrderReplayEvent, string>>>>)(store =>
            store.ReadStreamAsync("order-100")))
        .Then("the stream contains the placed paid and shipped events", stream =>
        {
            ScenarioExpect.Equal(3, stream.Count);
            ScenarioExpect.IsType<OrderReplayPlaced>(stream[0].Event);
            ScenarioExpect.IsType<OrderReplayPaid>(stream[1].Event);
            ScenarioExpect.IsType<OrderReplayShipped>(stream[2].Event);
        })
        .AssertPassed();
}
