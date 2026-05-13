using PatternKit.Messaging;

namespace PatternKit.Tests.Messaging;

public sealed class MessageTests
{
    [Fact]
    public void Create_UsesEmptyHeaders()
    {
        var message = Message<string>.Create("hello");

        Assert.Equal("hello", message.Payload);
        Assert.Same(MessageHeaders.Empty, message.Headers);
    }

    [Fact]
    public void WithPayload_PreservesHeaders()
    {
        var message = Message<string>.Create("hello").WithCorrelationId("corr-1");
        var mapped = message.WithPayload(42);

        Assert.Equal(42, mapped.Payload);
        Assert.Same(message.Headers, mapped.Headers);
        Assert.Equal("corr-1", mapped.Headers.CorrelationId);
    }

    [Fact]
    public void WithHeaders_ReplacesHeaders()
    {
        var headers = MessageHeaders.Empty.WithMessageId("msg-1");
        var message = Message<string>.Create("hello").WithHeaders(headers);

        Assert.Same(headers, message.Headers);
        Assert.Equal("msg-1", message.Headers.MessageId);
    }

    [Fact]
    public void WithHeaders_RejectsNullHeaders()
    {
        var message = Message<string>.Create("hello");

        Assert.Throws<ArgumentNullException>(() => message.WithHeaders(null!));
    }

    [Fact]
    public void Enrich_AppliesHeaderTransformation()
    {
        var message = Message<string>.Create("hello")
            .Enrich(headers => headers
                .WithCorrelationId("corr-1")
                .WithCausationId("cause-1"));

        Assert.Equal("corr-1", message.Headers.CorrelationId);
        Assert.Equal("cause-1", message.Headers.CausationId);
    }

    [Fact]
    public void Enrich_RejectsNullDelegate()
    {
        var message = Message<string>.Create("hello");

        Assert.Throws<ArgumentNullException>(() => message.Enrich(null!));
    }

    [Fact]
    public void Enrich_RejectsNullHeaders()
    {
        var message = Message<string>.Create("hello");

        Assert.Throws<InvalidOperationException>(() => message.Enrich(_ => null!));
    }

    [Fact]
    public void WellKnownHeaderHelpers_ReturnNewMessage()
    {
        var original = Message<string>.Create("hello");
        var updated = original
            .WithMessageId("msg-1")
            .WithCorrelationId("corr-1")
            .WithCausationId("cause-1")
            .WithIdempotencyKey("idem-1")
            .WithContentType("application/json")
            .WithReplyTo("queue:reply");

        Assert.Null(original.Headers.MessageId);
        Assert.Equal("msg-1", updated.Headers.MessageId);
        Assert.Equal("corr-1", updated.Headers.CorrelationId);
        Assert.Equal("cause-1", updated.Headers.CausationId);
        Assert.Equal("idem-1", updated.Headers.IdempotencyKey);
        Assert.Equal("application/json", updated.Headers.ContentType);
        Assert.Equal("queue:reply", updated.Headers.ReplyTo);
    }

    [Fact]
    public void WithHeader_AddsCustomHeader()
    {
        var message = Message<string>.Create("hello").WithHeader("tenant", "north");

        Assert.Equal("north", message.Headers.GetString("tenant"));
    }

    [Fact]
    public void WithReplyTo_AddsReplyAddress()
    {
        var message = Message<string>.Create("hello").WithReplyTo("queue:reply");

        Assert.Equal("queue:reply", message.Headers.ReplyTo);
    }
}
