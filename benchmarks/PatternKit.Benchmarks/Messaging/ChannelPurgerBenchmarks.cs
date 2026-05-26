using BenchmarkDotNet.Attributes;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Channels;

namespace PatternKit.Benchmarks.Messaging;

[BenchmarkCategory("EnterpriseIntegration", "Messaging", "ChannelPurger")]
public class ChannelPurgerBenchmarks
{
    private static readonly InventoryMaintenanceCommand[] Commands =
    [
        new("SKU-100", "stale-reservation", new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.Zero)),
        new("SKU-200", "obsolete-cycle-count", new DateTimeOffset(2026, 5, 25, 9, 5, 0, TimeSpan.Zero)),
        new("SKU-300", "cancelled-transfer", new DateTimeOffset(2026, 5, 25, 9, 10, 0, TimeSpan.Zero))
    ];

    [Benchmark(Baseline = true, Description = "Fluent: create channel purger")]
    [BenchmarkCategory("Fluent", "Construction")]
    public ChannelPurger<InventoryMaintenanceCommand> Fluent_CreateChannelPurger()
    {
        var channel = InventoryChannelPurgerExampleRunner.CreateChannel();
        return InventoryChannelPurgers.Create(channel, new InventoryChannelPurgeAudit());
    }

    [Benchmark(Description = "Generated: create channel purger")]
    [BenchmarkCategory("Generated", "Construction")]
    public ChannelPurger<InventoryMaintenanceCommand> Generated_CreateChannelPurger()
        => GeneratedInventoryChannelPurger.Create(InventoryChannelPurgerExampleRunner.CreateChannel());

    [Benchmark(Description = "Fluent: purge inventory maintenance backlog")]
    [BenchmarkCategory("Fluent", "Execution")]
    public InventoryChannelPurgeSummary Fluent_PurgeInventoryMaintenanceBacklog()
        => InventoryChannelPurgerExampleRunner.RunFluent(Commands);

    [Benchmark(Description = "Generated: purge inventory maintenance backlog")]
    [BenchmarkCategory("Generated", "Execution")]
    public InventoryChannelPurgeSummary Generated_PurgeInventoryMaintenanceBacklog()
    {
        var channel = InventoryChannelPurgerExampleRunner.CreateChannel();
        foreach (var command in Commands)
            channel.Send(Message<InventoryMaintenanceCommand>.Create(command).WithCorrelationId(command.Sku));

        var result = GeneratedInventoryChannelPurger.Create(channel).Purge();
        return new(result.PurgedCount, result.RemainingCount, result.PurgedMessages.Select(message => message.Payload.Sku).ToArray(), []);
    }
}
