using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class ReliabilityExampleTests
{
    [Fact]
    public async Task RunAsync_DispatchesOneOutboxMessageForDuplicateInput()
    {
        var dispatched = await ReliabilityExample.RunAsync();

        Assert.Equal(["order-42"], dispatched);
    }
}
