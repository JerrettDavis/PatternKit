using System.Collections;
using PatternKit.Messaging;

namespace PatternKit.Tests.Messaging;

public sealed class MessageHeadersTests
{
    [Fact]
    public void Empty_HasNoValues()
    {
        Assert.Empty(MessageHeaders.Empty);
        Assert.Null(MessageHeaders.Empty.MessageId);
    }

    [Fact]
    public void Constructor_CreatesEmptyCollection()
    {
        var headers = new MessageHeaders();

        Assert.True(headers.Count == 0);
        Assert.Empty(headers.Keys);
        Assert.Empty(headers.Values);
    }

    [Fact]
    public void With_ReturnsNewCollection_WithoutMutatingOriginal()
    {
        var original = MessageHeaders.Empty;
        var updated = original.With("tenant", "north");

        Assert.False(original.ContainsKey("tenant"));
        Assert.True(updated.ContainsKey("tenant"));
        Assert.Equal("north", updated.GetString("tenant"));
    }

    [Fact]
    public void Headers_AreCaseInsensitive()
    {
        var headers = MessageHeaders.Empty.With("Correlation-Id", "corr-1");

        Assert.True(headers.ContainsKey("correlation-id"));
        Assert.Equal("corr-1", headers.CorrelationId);
    }

    [Fact]
    public void WellKnownHeaders_AreExposedAsProperties()
    {
        var timestamp = DateTimeOffset.Parse("2026-05-13T08:00:00Z");

        var headers = MessageHeaders.Empty
            .WithMessageId("msg-1")
            .WithCorrelationId("corr-1")
            .WithCausationId("cause-1")
            .WithIdempotencyKey("idem-1")
            .WithContentType("application/json")
            .WithReplyTo("queue:reply")
            .WithTimestamp(timestamp);

        Assert.Equal("msg-1", headers.MessageId);
        Assert.Equal("corr-1", headers.CorrelationId);
        Assert.Equal("cause-1", headers.CausationId);
        Assert.Equal("idem-1", headers.IdempotencyKey);
        Assert.Equal("application/json", headers.ContentType);
        Assert.Equal("queue:reply", headers.ReplyTo);
        Assert.Equal(timestamp, headers.Timestamp);
    }

    [Fact]
    public void Indexer_And_TryGetValue_ReadExistingValue()
    {
        var headers = MessageHeaders.Empty.With("tenant", "north");

        Assert.Equal("north", headers["tenant"]);
        Assert.True(headers.TryGetValue("tenant", out var value));
        Assert.Equal("north", value);
    }

    [Fact]
    public void Without_RemovesExistingValue_WithoutMutatingOriginal()
    {
        var original = MessageHeaders.Empty.With("tenant", "north");
        var updated = original.Without("tenant");

        Assert.True(original.ContainsKey("tenant"));
        Assert.False(updated.ContainsKey("tenant"));
        Assert.Same(MessageHeaders.Empty, updated);
    }

    [Fact]
    public void Without_MissingValue_ReturnsSameInstance()
    {
        var headers = MessageHeaders.Empty.With("tenant", "north");

        Assert.Same(headers, headers.Without("missing"));
    }

    [Fact]
    public void Constructor_CopiesInputDictionary()
    {
        var source = new Dictionary<string, object?> { ["tenant"] = "north" };
        var headers = new MessageHeaders(source);

        source["tenant"] = "south";

        Assert.Equal("north", headers.GetString("tenant"));
    }

    [Fact]
    public void Constructor_RejectsNullInput()
    {
        Assert.Throws<ArgumentNullException>(() => new MessageHeaders(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsInvalidHeaderNames(string name)
    {
        var values = new[] { new KeyValuePair<string, object?>(name, "value") };

        Assert.Throws<ArgumentException>(() => new MessageHeaders(values));
    }

    [Fact]
    public void TryGet_ReadsTypedValues()
    {
        var headers = MessageHeaders.Empty.With("attempt", 3);

        Assert.True(headers.TryGet<int>("attempt", out var attempt));
        Assert.Equal(3, attempt);
        Assert.False(headers.TryGet<string>("attempt", out _));
    }

    [Fact]
    public void GetString_ConvertsNonStringValues()
    {
        var headers = MessageHeaders.Empty.With("attempt", 3);

        Assert.Equal("3", headers.GetString("attempt"));
    }

    [Fact]
    public void GetString_ReturnsNullForMissingOrNullValues()
    {
        var headers = MessageHeaders.Empty.With("empty", null);

        Assert.Null(headers.GetString("missing"));
        Assert.Null(headers.GetString("empty"));
    }

    [Fact]
    public void TryGetGuid_ReadsGuidAndStringValues()
    {
        var id = Guid.NewGuid();
        var headers = MessageHeaders.Empty
            .With("typed", id)
            .With("text", id.ToString("D"));

        Assert.True(headers.TryGetGuid("typed", out var typed));
        Assert.True(headers.TryGetGuid("text", out var text));
        Assert.Equal(id, typed);
        Assert.Equal(id, text);
    }

    [Fact]
    public void TryGetGuid_ReturnsFalseForMissingOrInvalidValues()
    {
        var headers = MessageHeaders.Empty.With("operation-id", "not-a-guid");

        Assert.False(headers.TryGetGuid("missing", out var missing));
        Assert.False(headers.TryGetGuid("operation-id", out var invalid));
        Assert.Equal(Guid.Empty, missing);
        Assert.Equal(Guid.Empty, invalid);
    }

    [Fact]
    public void TryGetDateTimeOffset_ReadsDateTimeOffsetDateTimeAndStringValues()
    {
        var timestamp = DateTimeOffset.Parse("2026-05-13T08:00:00Z");
        var dateTime = timestamp.UtcDateTime;
        var headers = MessageHeaders.Empty
            .With("dto", timestamp)
            .With("date", dateTime)
            .With("text", timestamp.ToString("O"));

        Assert.True(headers.TryGetDateTimeOffset("dto", out var dto));
        Assert.True(headers.TryGetDateTimeOffset("date", out var date));
        Assert.True(headers.TryGetDateTimeOffset("text", out var text));
        Assert.Equal(timestamp, dto);
        Assert.Equal(new DateTimeOffset(dateTime), date);
        Assert.Equal(timestamp, text);
    }

    [Fact]
    public void Timestamp_ReturnsNullWhenHeaderIsMissingOrInvalid()
    {
        Assert.Null(MessageHeaders.Empty.Timestamp);
        Assert.Null(MessageHeaders.Empty.With(MessageHeaderNames.Timestamp, "not-a-date").Timestamp);
    }

    [Fact]
    public void TryGetDateTimeOffset_ReturnsFalseForMissingOrInvalidValues()
    {
        var headers = MessageHeaders.Empty.With("accepted-at", "not-a-date");

        Assert.False(headers.TryGetDateTimeOffset("missing", out var missing));
        Assert.False(headers.TryGetDateTimeOffset("accepted-at", out var invalid));
        Assert.Equal(default, missing);
        Assert.Equal(default, invalid);
    }

    [Fact]
    public void Enumerators_ReturnHeaderPairs()
    {
        var headers = MessageHeaders.Empty.With("tenant", "north");

        Assert.Contains(headers, pair => pair.Key == "tenant" && (string?)pair.Value == "north");

        var enumerator = ((IEnumerable)headers).GetEnumerator();
        Assert.True(enumerator.MoveNext());
        var pair = Assert.IsType<KeyValuePair<string, object?>>(enumerator.Current);
        Assert.Equal("tenant", pair.Key);
        Assert.Equal("north", pair.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void With_RejectsInvalidHeaderNames(string name)
    {
        Assert.Throws<ArgumentException>(() => MessageHeaders.Empty.With(name, "value"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WellKnownStringHeaders_RejectInvalidValues(string value)
    {
        Assert.Throws<ArgumentException>(() => MessageHeaders.Empty.WithCorrelationId(value));
    }
}
