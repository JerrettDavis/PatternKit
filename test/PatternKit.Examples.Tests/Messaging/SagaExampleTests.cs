using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class SagaExampleTests
{
    [Fact]
    public void Run_UsesGeneratedSagaFactory()
    {
        var summary = SagaExample.Run();

        Assert.Equal("order-42", summary.OrderId);
        Assert.True(summary.Submitted);
        Assert.True(summary.Paid);
        Assert.True(summary.Completed);
    }
}
