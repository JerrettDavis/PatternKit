using PatternKit.Messaging;
using TinyBDD;

namespace PatternKit.Tests.Messaging;

public sealed class MessageTests
{
    [Scenario("Create UsesEmptyHeaders")]
    [Fact]
    public void Create_UsesEmptyHeaders()
    {
        var message = Message<string>.Create("hello");

        ScenarioExpect.Equal("hello", message.Payload);
        ScenarioExpect.Same(MessageHeaders.Empty, message.Headers);
    }

    [Scenario("WithPayload PreservesHeaders")]
    [Fact]
    public void WithPayload_PreservesHeaders()
    {
        var message = Message<string>.Create("hello").WithCorrelationId("corr-1");
        var mapped = message.WithPayload(42);

        ScenarioExpect.Equal(42, mapped.Payload);
        ScenarioExpect.Same(message.Headers, mapped.Headers);
        ScenarioExpect.Equal("corr-1", mapped.Headers.CorrelationId);
    }

    [Scenario("WithHeaders ReplacesHeaders")]
    [Fact]
    public void WithHeaders_ReplacesHeaders()
    {
        var headers = MessageHeaders.Empty.WithMessageId("msg-1");
        var message = Message<string>.Create("hello").WithHeaders(headers);

        ScenarioExpect.Same(headers, message.Headers);
        ScenarioExpect.Equal("msg-1", message.Headers.MessageId);
    }

    [Scenario("WithHeaders RejectsNullHeaders")]
    [Fact]
    public void WithHeaders_RejectsNullHeaders()
    {
        var message = Message<string>.Create("hello");

        ScenarioExpect.Throws<ArgumentNullException>(() => message.WithHeaders(null!));
    }

    [Scenario("Enrich AppliesHeaderTransformation")]
    [Fact]
    public void Enrich_AppliesHeaderTransformation()
    {
        var message = Message<string>.Create("hello")
            .Enrich(headers => headers
                .WithCorrelationId("corr-1")
                .WithCausationId("cause-1"));

        ScenarioExpect.Equal("corr-1", message.Headers.CorrelationId);
        ScenarioExpect.Equal("cause-1", message.Headers.CausationId);
    }

    [Scenario("Enrich RejectsNullDelegate")]
    [Fact]
    public void Enrich_RejectsNullDelegate()
    {
        var message = Message<string>.Create("hello");

        ScenarioExpect.Throws<ArgumentNullException>(() => message.Enrich(null!));
    }

    [Scenario("Enrich RejectsNullHeaders")]
    [Fact]
    public void Enrich_RejectsNullHeaders()
    {
        var message = Message<string>.Create("hello");

        ScenarioExpect.Throws<InvalidOperationException>(() => message.Enrich(_ => null!));
    }

    [Scenario("WellKnownHeaderHelpers ReturnNewMessage")]
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

        ScenarioExpect.Null(original.Headers.MessageId);
        ScenarioExpect.Equal("msg-1", updated.Headers.MessageId);
        ScenarioExpect.Equal("corr-1", updated.Headers.CorrelationId);
        ScenarioExpect.Equal("cause-1", updated.Headers.CausationId);
        ScenarioExpect.Equal("idem-1", updated.Headers.IdempotencyKey);
        ScenarioExpect.Equal("application/json", updated.Headers.ContentType);
        ScenarioExpect.Equal("queue:reply", updated.Headers.ReplyTo);
    }

    [Scenario("WithHeader AddsCustomHeader")]
    [Fact]
    public void WithHeader_AddsCustomHeader()
    {
        var message = Message<string>.Create("hello").WithHeader("tenant", "north");

        ScenarioExpect.Equal("north", message.Headers.GetString("tenant"));
    }

    [Scenario("WithReplyTo AddsReplyAddress")]
    [Fact]
    public void WithReplyTo_AddsReplyAddress()
    {
        var message = Message<string>.Create("hello").WithReplyTo("queue:reply");

        ScenarioExpect.Equal("queue:reply", message.Headers.ReplyTo);
    }
}
