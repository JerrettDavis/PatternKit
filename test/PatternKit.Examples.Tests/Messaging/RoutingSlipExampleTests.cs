using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class RoutingSlipExampleTests
{
    [Fact]
    public void Run_UsesGeneratedRoutingSlipFactory()
    {
        var summary = RoutingSlipExample.Run();

        Assert.Equal("validated,reserved,shipped", summary.Status);
        Assert.Equal(["validate", "reserve-inventory", "ship"], summary.Steps);
        Assert.Equal("3", summary.RoutingIndex);
    }
}
