using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class OrderInvalidMessageChannelExampleTests
{
    [Scenario("FluentOrderInvalidMessageChannelRoutesInvalidImports")]
    [Fact]
    public void FluentOrderInvalidMessageChannelRoutesInvalidImports()
    {
        var summary = OrderInvalidMessageChannelExampleRunner.RunFluent(CreateCommands());

        ScenarioExpect.Equal(1, summary.AcceptedCount);
        ScenarioExpect.Equal(2, summary.InvalidCount);
        ScenarioExpect.Equal(["SKU is required.", "Quantity must be positive."], summary.InvalidReasons);
        ScenarioExpect.Equal(["ORD-2", "ORD-3"], summary.InvalidOrderIds);
    }

    [Scenario("GeneratedOrderInvalidMessageChannelWorksFromServiceCollection")]
    [Fact]
    public void GeneratedOrderInvalidMessageChannelWorksFromServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddOrderInvalidMessageChannelDemo()
            .BuildServiceProvider();

        var runner = provider.GetRequiredService<OrderInvalidMessageChannelExampleRunner>();
        var summary = runner.RunGenerated(CreateCommands());

        ScenarioExpect.Equal(1, summary.AcceptedCount);
        ScenarioExpect.Equal(2, summary.InvalidCount);
        ScenarioExpect.Equal(["ORD-2", "ORD-3"], summary.InvalidOrderIds);
    }

    private static OrderImportCommand[] CreateCommands()
        =>
        [
            new("ORD-1", "SKU-100", 2),
            new("ORD-2", "", 1),
            new("ORD-3", "SKU-300", 0)
        ];
}
