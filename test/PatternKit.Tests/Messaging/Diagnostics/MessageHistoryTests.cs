using PatternKit.Messaging;
using PatternKit.Messaging.Diagnostics;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Diagnostics;

public sealed class MessageHistoryTests
{
    private static readonly DateTimeOffset FixedTime = new(2026, 5, 27, 0, 0, 0, TimeSpan.Zero);

    [Scenario("Recorder appends history entries without changing the payload")]
    [Fact]
    public void Recorder_Appends_History_Entries_Without_Changing_The_Payload()
    {
        var message = Message<Order>.Create(new("O-100", 125m)).WithCorrelationId("corr-1");
        var received = MessageHistory<Order>.Create("checkout-api")
            .Action("received")
            .Clock(static () => FixedTime)
            .Details(static message => message.Payload.Id)
            .Build();
        var routed = MessageHistory<Order>.Create("fulfillment-router")
            .Action("routed")
            .Clock(static () => FixedTime.AddSeconds(1))
            .Build();

        var result = routed.Record(received.Record(message));
        var entries = MessageHistory<Order>.Read(result);

        ScenarioExpect.Equal("O-100", result.Payload.Id);
        ScenarioExpect.Equal("corr-1", result.Headers.CorrelationId);
        ScenarioExpect.Equal(2, entries.Count);
        ScenarioExpect.Equal("checkout-api", entries[0].Component);
        ScenarioExpect.Equal("received", entries[0].Action);
        ScenarioExpect.Equal("O-100", entries[0].Details);
        ScenarioExpect.Equal("fulfillment-router", entries[1].Component);
        ScenarioExpect.Equal(FixedTime.AddSeconds(1), entries[1].Timestamp);
    }

    [Scenario("Recorder supports custom history headers")]
    [Fact]
    public void Recorder_Supports_Custom_History_Headers()
    {
        var recorder = MessageHistory<Order>.Create("partner-import")
            .Header("X-Audit-History")
            .Clock(static () => FixedTime)
            .Build();

        var result = recorder.Record(Message<Order>.Create(new("O-101", 90m)));

        ScenarioExpect.Empty(MessageHistory<Order>.Read(result));
        ScenarioExpect.Single(MessageHistory<Order>.Read(result, "X-Audit-History"));
    }

    [Scenario("Reader returns empty history when no entries exist")]
    [Fact]
    public void Reader_Returns_Empty_History_When_No_Entries_Exist()
    {
        var message = Message<Order>.Create(new("O-102", 45m));

        var entries = MessageHistory<Order>.Read(message);

        ScenarioExpect.Empty(entries);
    }

    [Scenario("Builder rejects invalid message history configuration")]
    [Fact]
    public void Builder_Rejects_Invalid_Message_History_Configuration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageHistory<Order>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() => MessageHistory<Order>.Create("api").Action(""));
        ScenarioExpect.Throws<ArgumentException>(() => MessageHistory<Order>.Create("api").Header(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageHistory<Order>.Create("api").Clock(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageHistory<Order>.Create("api").Details(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageHistory<Order>.Create("api").Build().Record(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageHistory<Order>.Read(null!));
        ScenarioExpect.Throws<ArgumentException>(() => MessageHistory<Order>.Read(Message<Order>.Create(new("O-1", 1m)), ""));
    }

    private sealed record Order(string Id, decimal Total);
}
