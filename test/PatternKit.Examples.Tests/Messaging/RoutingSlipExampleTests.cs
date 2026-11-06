using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class RoutingSlipExampleTests
{
    [Scenario("Run UsesGeneratedRoutingSlipFactory")]
    [Fact]
    public void Run_UsesGeneratedRoutingSlipFactory()
    {
        var summary = RoutingSlipExample.Run();

        ScenarioExpect.Equal("validated,reserved,shipped", summary.Status);
        ScenarioExpect.Equal(["validate", "reserve-inventory", "ship"], summary.Steps);
        ScenarioExpect.Equal("3", summary.RoutingIndex);
    }
}
