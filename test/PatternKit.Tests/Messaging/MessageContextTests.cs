using PatternKit.Messaging;

namespace PatternKit.Tests.Messaging;

public sealed class MessageContextTests
{
    [Fact]
    public void Empty_HasEmptyHeadersAndItems()
    {
        Assert.Same(MessageHeaders.Empty, MessageContext.Empty.Headers);
        Assert.Empty(MessageContext.Empty.Items);
    }

    [Fact]
    public void From_CopiesMessageHeaders()
    {
        var message = Message<string>.Create("hello").WithCorrelationId("corr-1");
        var context = MessageContext.From(message);

        Assert.Same(message.Headers, context.Headers);
        Assert.Equal("corr-1", context.Headers.CorrelationId);
    }

    [Fact]
    public void From_RejectsNullMessage()
    {
        Assert.Throws<ArgumentNullException>(() => MessageContext.From<string>(null!));
    }

    [Fact]
    public void Constructor_AcceptsHeadersAndCancellation()
    {
        using var cts = new CancellationTokenSource();
        var headers = MessageHeaders.Empty.WithCorrelationId("corr-1");

        var context = new MessageContext(headers, cts.Token);

        Assert.Same(headers, context.Headers);
        Assert.Equal(cts.Token, context.CancellationToken);
    }

    [Fact]
    public void WithHeader_ReturnsNewContext_WithoutMutatingOriginal()
    {
        var original = MessageContext.Empty;
        var updated = original.WithHeader("tenant", "north");

        Assert.False(original.Headers.ContainsKey("tenant"));
        Assert.Equal("north", updated.Headers.GetString("tenant"));
    }

    [Fact]
    public void WithHeaders_ReplacesHeaders()
    {
        var headers = MessageHeaders.Empty.WithCorrelationId("corr-1");
        var context = MessageContext.Empty.WithHeaders(headers);

        Assert.Same(headers, context.Headers);
    }

    [Fact]
    public void WithHeaders_RejectsNullHeaders()
    {
        Assert.Throws<ArgumentNullException>(() => MessageContext.Empty.WithHeaders(null!));
    }

    [Fact]
    public void WithItem_ReturnsNewContext_WithoutMutatingOriginal()
    {
        var original = MessageContext.Empty;
        var updated = original.WithItem("attempt", 2);

        Assert.Empty(original.Items);
        Assert.True(updated.TryGetItem<int>("attempt", out var attempt));
        Assert.Equal(2, attempt);
    }

    [Fact]
    public void WithoutItem_RemovesItem_WithoutMutatingOriginal()
    {
        var original = MessageContext.Empty.WithItem("attempt", 2);
        var updated = original.WithoutItem("attempt");

        Assert.True(original.TryGetItem<int>("attempt", out _));
        Assert.False(updated.TryGetItem<int>("attempt", out _));
    }

    [Fact]
    public void WithoutItem_MissingItem_ReturnsSameInstance()
    {
        var context = MessageContext.Empty.WithItem("attempt", 2);

        Assert.Same(context, context.WithoutItem("missing"));
    }

    [Fact]
    public void TryGetItem_ReturnsFalseForMissingOrWrongType()
    {
        var context = MessageContext.Empty.WithItem("attempt", 2);

        Assert.False(context.TryGetItem<int>("missing", out var missing));
        Assert.False(context.TryGetItem<string>("attempt", out var wrongType));
        Assert.Equal(0, missing);
        Assert.Null(wrongType);
    }

    [Fact]
    public void WithCancellation_ReturnsNewContextWithSameHeadersAndItems()
    {
        using var cts = new CancellationTokenSource();
        var context = MessageContext.Empty
            .WithHeader("tenant", "north")
            .WithItem("attempt", 2);

        var updated = context.WithCancellation(cts.Token);

        Assert.Equal(cts.Token, updated.CancellationToken);
        Assert.Same(context.Headers, updated.Headers);
        Assert.True(updated.TryGetItem<int>("attempt", out var attempt));
        Assert.Equal(2, attempt);
    }

    [Fact]
    public void Items_AreReadOnlySnapshot()
    {
        var context = MessageContext.Empty.WithItem("attempt", 2);

        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, object?>)context.Items).Add("other", 3));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WithItem_RejectsInvalidKeys(string key)
    {
        Assert.Throws<ArgumentException>(() => MessageContext.Empty.WithItem(key, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WithoutItem_RejectsInvalidKeys(string key)
    {
        Assert.Throws<ArgumentException>(() => MessageContext.Empty.WithoutItem(key));
    }
}
