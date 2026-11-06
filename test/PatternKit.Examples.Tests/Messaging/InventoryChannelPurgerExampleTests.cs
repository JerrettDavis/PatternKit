using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class InventoryChannelPurgerExampleTests
{
    [Scenario("FluentInventoryChannelPurgerPurgesBacklog")]
    [Fact]
    public void FluentInventoryChannelPurgerPurgesBacklog()
    {
        var summary = InventoryChannelPurgerExampleRunner.RunFluent(CreateCommands());

        ScenarioExpect.Equal(3, summary.PurgedCount);
        ScenarioExpect.Equal(0, summary.RemainingCount);
        ScenarioExpect.Equal(["SKU-100", "SKU-200", "SKU-300"], summary.PurgedSkus);
        ScenarioExpect.Equal(3, summary.AuditTrail.Count);
        ScenarioExpect.Contains("inventory-maintenance:SKU-200:obsolete-cycle-count", summary.AuditTrail[1]);
    }

    [Scenario("GeneratedInventoryChannelPurgerWorksFromServiceCollection")]
    [Fact]
    public void GeneratedInventoryChannelPurgerWorksFromServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddInventoryChannelPurgerDemo()
            .BuildServiceProvider();

        var runner = provider.GetRequiredService<InventoryChannelPurgerExampleRunner>();
        var summary = runner.RunGenerated(CreateCommands());

        ScenarioExpect.Equal(3, summary.PurgedCount);
        ScenarioExpect.Equal(0, summary.RemainingCount);
        ScenarioExpect.Equal(["SKU-100", "SKU-200", "SKU-300"], summary.PurgedSkus);
    }

    private static InventoryMaintenanceCommand[] CreateCommands()
        =>
        [
            new("SKU-100", "stale-reservation", new DateTimeOffset(2026, 5, 25, 9, 0, 0, TimeSpan.Zero)),
            new("SKU-200", "obsolete-cycle-count", new DateTimeOffset(2026, 5, 25, 9, 5, 0, TimeSpan.Zero)),
            new("SKU-300", "cancelled-transfer", new DateTimeOffset(2026, 5, 25, 9, 10, 0, TimeSpan.Zero))
        ];
}
