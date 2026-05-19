using System.Collections;
using PatternKit.Messaging;
using TinyBDD;

namespace PatternKit.Tests.Messaging;

public sealed class MessageHeadersTests
{
    [Scenario("Empty HasNoValues")]
    [Fact]
    public void Empty_HasNoValues()
    {
        ScenarioExpect.Empty(MessageHeaders.Empty);
        ScenarioExpect.Null(MessageHeaders.Empty.MessageId);
    }

    [Scenario("Constructor CreatesEmptyCollection")]
    [Fact]
    public void Constructor_CreatesEmptyCollection()
    {
        var headers = new MessageHeaders();

        ScenarioExpect.True(headers.Count == 0);
        ScenarioExpect.Empty(headers.Keys);
        ScenarioExpect.Empty(headers.Values);
    }

    [Scenario("With ReturnsNewCollection WithoutMutatingOriginal")]
    [Fact]
    public void With_ReturnsNewCollection_WithoutMutatingOriginal()
    {
        var original = MessageHeaders.Empty;
        var updated = original.With("tenant", "north");

        ScenarioExpect.False(original.ContainsKey("tenant"));
        ScenarioExpect.True(updated.ContainsKey("tenant"));
        ScenarioExpect.Equal("north", updated.GetString("tenant"));
    }

    [Scenario("Headers AreCaseInsensitive")]
    [Fact]
    public void Headers_AreCaseInsensitive()
    {
        var headers = MessageHeaders.Empty.With("Correlation-Id", "corr-1");

        ScenarioExpect.True(headers.ContainsKey("correlation-id"));
        ScenarioExpect.Equal("corr-1", headers.CorrelationId);
    }

    [Scenario("WellKnownHeaders AreExposedAsProperties")]
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

        ScenarioExpect.Equal("msg-1", headers.MessageId);
        ScenarioExpect.Equal("corr-1", headers.CorrelationId);
        ScenarioExpect.Equal("cause-1", headers.CausationId);
        ScenarioExpect.Equal("idem-1", headers.IdempotencyKey);
        ScenarioExpect.Equal("application/json", headers.ContentType);
        ScenarioExpect.Equal("queue:reply", headers.ReplyTo);
        ScenarioExpect.Equal(timestamp, headers.Timestamp);
    }

    [Scenario("Indexer And TryGetValue ReadExistingValue")]
    [Fact]
    public void Indexer_And_TryGetValue_ReadExistingValue()
    {
        var headers = MessageHeaders.Empty.With("tenant", "north");

        ScenarioExpect.Equal("north", headers["tenant"]);
        ScenarioExpect.True(headers.TryGetValue("tenant", out var value));
        ScenarioExpect.Equal("north", value);
    }

    [Scenario("Without RemovesExistingValue WithoutMutatingOriginal")]
    [Fact]
    public void Without_RemovesExistingValue_WithoutMutatingOriginal()
    {
        var original = MessageHeaders.Empty.With("tenant", "north");
        var updated = original.Without("tenant");

        ScenarioExpect.True(original.ContainsKey("tenant"));
        ScenarioExpect.False(updated.ContainsKey("tenant"));
        ScenarioExpect.Same(MessageHeaders.Empty, updated);
    }

    [Scenario("Without MissingValue ReturnsSameInstance")]
    [Fact]
    public void Without_MissingValue_ReturnsSameInstance()
    {
        var headers = MessageHeaders.Empty.With("tenant", "north");

        ScenarioExpect.Same(headers, headers.Without("missing"));
    }

    [Scenario("Constructor CopiesInputDictionary")]
    [Fact]
    public void Constructor_CopiesInputDictionary()
    {
        var source = new Dictionary<string, object?> { ["tenant"] = "north" };
        var headers = new MessageHeaders(source);

        source["tenant"] = "south";

        ScenarioExpect.Equal("north", headers.GetString("tenant"));
    }

    [Scenario("Constructor RejectsNullInput")]
    [Fact]
    public void Constructor_RejectsNullInput()
    {
        ScenarioExpect.Throws<ArgumentNullException>(() => new MessageHeaders(null!));
    }

    [Scenario("Constructor RejectsInvalidHeaderNames")]
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_RejectsInvalidHeaderNames(string name)
    {
        var values = new[] { new KeyValuePair<string, object?>(name, "value") };

        ScenarioExpect.Throws<ArgumentException>(() => new MessageHeaders(values));
    }

    [Scenario("TryGet ReadsTypedValues")]
    [Fact]
    public void TryGet_ReadsTypedValues()
    {
        var headers = MessageHeaders.Empty.With("attempt", 3);

        ScenarioExpect.True(headers.TryGet<int>("attempt", out var attempt));
        ScenarioExpect.Equal(3, attempt);
        ScenarioExpect.False(headers.TryGet<string>("attempt", out _));
    }

    [Scenario("GetString ConvertsNonStringValues")]
    [Fact]
    public void GetString_ConvertsNonStringValues()
    {
        var headers = MessageHeaders.Empty.With("attempt", 3);

        ScenarioExpect.Equal("3", headers.GetString("attempt"));
    }

    [Scenario("GetString ReturnsNullForMissingOrNullValues")]
    [Fact]
    public void GetString_ReturnsNullForMissingOrNullValues()
    {
        var headers = MessageHeaders.Empty.With("empty", null);

        ScenarioExpect.Null(headers.GetString("missing"));
        ScenarioExpect.Null(headers.GetString("empty"));
    }

    [Scenario("TryGetGuid ReadsGuidAndStringValues")]
    [Fact]
    public void TryGetGuid_ReadsGuidAndStringValues()
    {
        var id = Guid.NewGuid();
        var headers = MessageHeaders.Empty
            .With("typed", id)
            .With("text", id.ToString("D"));

        ScenarioExpect.True(headers.TryGetGuid("typed", out var typed));
        ScenarioExpect.True(headers.TryGetGuid("text", out var text));
        ScenarioExpect.Equal(id, typed);
        ScenarioExpect.Equal(id, text);
    }

    [Scenario("TryGetGuid ReturnsFalseForMissingOrInvalidValues")]
    [Fact]
    public void TryGetGuid_ReturnsFalseForMissingOrInvalidValues()
    {
        var headers = MessageHeaders.Empty.With("operation-id", "not-a-guid");

        ScenarioExpect.False(headers.TryGetGuid("missing", out var missing));
        ScenarioExpect.False(headers.TryGetGuid("operation-id", out var invalid));
        ScenarioExpect.Equal(Guid.Empty, missing);
        ScenarioExpect.Equal(Guid.Empty, invalid);
    }

    [Scenario("TryGetDateTimeOffset ReadsDateTimeOffsetDateTimeAndStringValues")]
    [Fact]
    public void TryGetDateTimeOffset_ReadsDateTimeOffsetDateTimeAndStringValues()
    {
        var timestamp = DateTimeOffset.Parse("2026-05-13T08:00:00Z");
        var dateTime = timestamp.UtcDateTime;
        var headers = MessageHeaders.Empty
            .With("dto", timestamp)
            .With("date", dateTime)
            .With("text", timestamp.ToString("O"));

        ScenarioExpect.True(headers.TryGetDateTimeOffset("dto", out var dto));
        ScenarioExpect.True(headers.TryGetDateTimeOffset("date", out var date));
        ScenarioExpect.True(headers.TryGetDateTimeOffset("text", out var text));
        ScenarioExpect.Equal(timestamp, dto);
        ScenarioExpect.Equal(new DateTimeOffset(dateTime), date);
        ScenarioExpect.Equal(timestamp, text);
    }

    [Scenario("Timestamp ReturnsNullWhenHeaderIsMissingOrInvalid")]
    [Fact]
    public void Timestamp_ReturnsNullWhenHeaderIsMissingOrInvalid()
    {
        ScenarioExpect.Null(MessageHeaders.Empty.Timestamp);
        ScenarioExpect.Null(MessageHeaders.Empty.With(MessageHeaderNames.Timestamp, "not-a-date").Timestamp);
    }

    [Scenario("TryGetDateTimeOffset ReturnsFalseForMissingOrInvalidValues")]
    [Fact]
    public void TryGetDateTimeOffset_ReturnsFalseForMissingOrInvalidValues()
    {
        var headers = MessageHeaders.Empty.With("accepted-at", "not-a-date");

        ScenarioExpect.False(headers.TryGetDateTimeOffset("missing", out var missing));
        ScenarioExpect.False(headers.TryGetDateTimeOffset("accepted-at", out var invalid));
        ScenarioExpect.Equal(default, missing);
        ScenarioExpect.Equal(default, invalid);
    }

    [Scenario("Enumerators ReturnHeaderPairs")]
    [Fact]
    public void Enumerators_ReturnHeaderPairs()
    {
        var headers = MessageHeaders.Empty.With("tenant", "north");

        ScenarioExpect.Contains(headers, pair => pair.Key == "tenant" && (string?)pair.Value == "north");

        var enumerator = ((IEnumerable)headers).GetEnumerator();
        ScenarioExpect.True(enumerator.MoveNext());
        var pair = ScenarioExpect.IsType<KeyValuePair<string, object?>>(enumerator.Current);
        ScenarioExpect.Equal("tenant", pair.Key);
        ScenarioExpect.Equal("north", pair.Value);
    }

    [Scenario("With RejectsInvalidHeaderNames")]
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void With_RejectsInvalidHeaderNames(string name)
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageHeaders.Empty.With(name, "value"));
    }

    [Scenario("WellKnownStringHeaders RejectInvalidValues")]
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void WellKnownStringHeaders_RejectInvalidValues(string value)
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageHeaders.Empty.WithCorrelationId(value));
    }
}
