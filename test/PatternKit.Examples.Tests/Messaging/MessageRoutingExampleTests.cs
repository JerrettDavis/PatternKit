using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class MessageRoutingExampleTests
{
    [Fact]
    public void Run_ComposesEnterpriseRoutingPrimitives()
    {
        var summary = MessageRoutingExample.Run();

        Assert.Equal("priority", summary.Route);
        Assert.Equal(["audit", "billing"], summary.Recipients);
        Assert.Equal(2, summary.SplitCount);
        Assert.Equal(100m, summary.AggregatedTotal);
        Assert.Equal("msg-order-42", summary.CausationId);
    }
}
