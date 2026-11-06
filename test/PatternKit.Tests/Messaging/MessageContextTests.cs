using PatternKit.Messaging;
using TinyBDD;

namespace PatternKit.Tests.Messaging;

public sealed class MessageContextTests
{
    [Scenario("Empty HasEmptyHeadersAndItems")]
    [Fact]
    public void Empty_HasEmptyHeadersAndItems()
    {
        ScenarioExpect.Same(MessageHeaders.Empty, MessageContext.Empty.Headers);
        ScenarioExpect.Empty(MessageContext.Empty.Items);
    }

    [Scenario("From CopiesMessageHeaders")]
    [Fact]
    public void From_CopiesMessageHeaders()
    {
        var message = Message<string>.Create("hello").WithCorrelationId("corr-1");
        var context = MessageContext.From(message);

        ScenarioExpect.Same(message.Headers, context.Headers);
        ScenarioExpect.Equal("corr-1", context.Headers.CorrelationId);
    }

    [Scenario("From RejectsNullMessage")]
    [Fact]
    public void From_RejectsNullMessage()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageContext.From<string>(null!));
    }

    [Scenario("Constructor AcceptsHeadersAndCancellation")]
    [Fact]
    public void Constructor_AcceptsHeadersAndCancellation()
    {
        using var cts = new CancellationTokenSource();
        var headers = MessageHeaders.Empty.WithCorrelationId("corr-1");

        var context = new MessageContext(headers, cts.Token);

        ScenarioExpect.Same(headers, context.Headers);
        ScenarioExpect.Equal(cts.Token, context.CancellationToken);
    }

    [Scenario("WithHeader ReturnsNewContext WithoutMutatingOriginal")]
    [Fact]
    public void WithHeader_ReturnsNewContext_WithoutMutatingOriginal()
    {
        var original = MessageContext.Empty;
        var updated = original.WithHeader("tenant", "north");

        ScenarioExpect.False(original.Headers.ContainsKey("tenant"));
        ScenarioExpect.Equal("north", updated.Headers.GetString("tenant"));
    }

    [Scenario("WithHeaders ReplacesHeaders")]
    [Fact]
    public void WithHeaders_ReplacesHeaders()
    {
        var headers = MessageHeaders.Empty.WithCorrelationId("corr-1");
        var context = MessageContext.Empty.WithHeaders(headers);

        ScenarioExpect.Same(headers, context.Headers);
    }

    [Scenario("WithHeaders RejectsNullHeaders")]
    [Fact]
    public void WithHeaders_RejectsNullHeaders()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageContext.Empty.WithHeaders(null!));
    }

    [Scenario("WithItem ReturnsNewContext WithoutMutatingOriginal")]
    [Fact]
    public void WithItem_ReturnsNewContext_WithoutMutatingOriginal()
    {
        var original = MessageContext.Empty;
        var updated = original.WithItem("attempt", 2);

        ScenarioExpect.Empty(original.Items);
        ScenarioExpect.True(updated.TryGetItem<int>("attempt", out var attempt));
        ScenarioExpect.Equal(2, attempt);
    }

    [Scenario("WithoutItem RemovesItem WithoutMutatingOriginal")]
    [Fact]
    public void WithoutItem_RemovesItem_WithoutMutatingOriginal()
    {
        var original = MessageContext.Empty.WithItem("attempt", 2);
        var updated = original.WithoutItem("attempt");

        ScenarioExpect.True(original.TryGetItem<int>("attempt", out _));
        ScenarioExpect.False(updated.TryGetItem<int>("attempt", out _));
    }

    [Scenario("WithoutItem MissingItem ReturnsSameInstance")]
    [Fact]
    public void WithoutItem_MissingItem_ReturnsSameInstance()
    {
        var context = MessageContext.Empty.WithItem("attempt", 2);

        ScenarioExpect.Same(context, context.WithoutItem("missing"));
    }

    [Scenario("TryGetItem ReturnsFalseForMissingOrWrongType")]
    [Fact]
    public void TryGetItem_ReturnsFalseForMissingOrWrongType()
    {
        var context = MessageContext.Empty.WithItem("attempt", 2);

        ScenarioExpect.False(context.TryGetItem<int>("missing", out var missing));
        ScenarioExpect.False(context.TryGetItem<string>("attempt", out var wrongType));
        ScenarioExpect.Equal(0, missing);
        ScenarioExpect.Null(wrongType);
    }

    [Scenario("WithCancellation ReturnsNewContextWithSameHeadersAndItems")]
    [Fact]
    public void WithCancellation_ReturnsNewContextWithSameHeadersAndItems()
    {
        using var cts = new CancellationTokenSource();
        var context = MessageContext.Empty
            .WithHeader("tenant", "north")
            .WithItem("attempt", 2);

        var updated = context.WithCancellation(cts.Token);

        ScenarioExpect.Equal(cts.Token, updated.CancellationToken);
        ScenarioExpect.Same(context.Headers, updated.Headers);
        ScenarioExpect.True(updated.TryGetItem<int>("attempt", out var attempt));
        ScenarioExpect.Equal(2, attempt);
    }

    [Scenario("Items AreReadOnlySnapshot")]
    [Fact]
    public void Items_AreReadOnlySnapshot()
    {
        var context = MessageContext.Empty.WithItem("attempt", 2);

        ScenarioExpect.Throws<NotSupportedException>(() =>
            ((IDictionary<string, object?>)context.Items).Add("other", 3));
    }

    [Scenario("WithItem RejectsInvalidKeys")]
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WithItem_RejectsInvalidKeys(string key)
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageContext.Empty.WithItem(key, 1));
    }

    [Scenario("WithoutItem RejectsInvalidKeys")]
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WithoutItem_RejectsInvalidKeys(string key)
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageContext.Empty.WithoutItem(key));
    }
}
