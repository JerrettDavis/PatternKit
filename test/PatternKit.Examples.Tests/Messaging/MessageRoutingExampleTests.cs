using PatternKit.Examples.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class MessageRoutingExampleTests
{
    [Scenario("Run ComposesEnterpriseRoutingPrimitives")]
    [Fact]
    public void Run_ComposesEnterpriseRoutingPrimitives()
    {
        var summary = MessageRoutingExample.Run();

        ScenarioExpect.Equal("priority", summary.Route);
        ScenarioExpect.Equal(["audit", "billing"], summary.Recipients);
        ScenarioExpect.Equal(2, summary.SplitCount);
        ScenarioExpect.Equal(100m, summary.AggregatedTotal);
        ScenarioExpect.Equal("msg-order-42", summary.CausationId);
    }
}
