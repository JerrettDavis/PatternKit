using PatternKit.Examples.Messaging;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class MessageEnvelopeExampleTests
{
    [Fact]
    public void Run_ReturnsExpectedEnvelopeAndContextMetadata()
    {
        var summary = MessageEnvelopeExample.Run();

        Assert.Equal("order-42", summary.OrderId);
        Assert.Equal("msg-100", summary.MessageId);
        Assert.Equal("order-42", summary.CorrelationId);
        Assert.Equal("checkout-7", summary.CausationId);
        Assert.Equal("order-42:accepted", summary.IdempotencyKey);
        Assert.Equal("application/vnd.patternkit.order+json", summary.ContentType);
        Assert.Equal("billing", summary.Route);
        Assert.Equal(1, summary.Attempt);
    }
}
