using PatternKit.Messaging;
using ResequencerUnderTest = global::PatternKit.Messaging.Routing.Resequencer<PatternKit.Tests.Messaging.Routing.ResequencerTests.Event>;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class ResequencerTests
{
    [Scenario("Accept ReleasesContiguousMessagesInOrder")]
    [Fact]
    public void Accept_ReleasesContiguousMessagesInOrder()
    {
        var resequencer = ResequencerUnderTest.Create("orders")
            .SelectSequence(static (message, _) => message.Payload.Sequence)
            .Build();

        var third = resequencer.Accept(Message<Event>.Create(new(3, "third")));
        var first = resequencer.Accept(Message<Event>.Create(new(1, "first")));
        var second = resequencer.Accept(Message<Event>.Create(new(2, "second")));

        ScenarioExpect.Empty(third.Released);
        ScenarioExpect.Equal([1L], first.Released.Select(static message => message.Sequence).ToArray());
        ScenarioExpect.Equal([2L, 3L], second.Released.Select(static message => message.Sequence).ToArray());
        ScenarioExpect.Equal("second", second.Released[0].Message.Payload.Name);
        ScenarioExpect.Equal("third", second.Released[1].Message.Payload.Name);
    }

    [Scenario("Accept RejectsDuplicateOrOldSequences")]
    [Fact]
    public void Accept_RejectsDuplicateOrOldSequences()
    {
        var resequencer = ResequencerUnderTest.Create()
            .SelectSequence(static (message, _) => message.Payload.Sequence)
            .Build();

        var buffered = resequencer.Accept(Message<Event>.Create(new(2, "second")));
        var duplicate = resequencer.Accept(Message<Event>.Create(new(2, "duplicate")));
        var first = resequencer.Accept(Message<Event>.Create(new(1, "first")));
        var old = resequencer.Accept(Message<Event>.Create(new(1, "old")));

        ScenarioExpect.True(buffered.Accepted);
        ScenarioExpect.False(duplicate.Accepted);
        ScenarioExpect.Equal("Message sequence is already buffered.", duplicate.RejectionReason);
        ScenarioExpect.True(first.Accepted);
        ScenarioExpect.False(old.Accepted);
        ScenarioExpect.Equal("Message sequence has already been released.", old.RejectionReason);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => ResequencerUnderTest.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => ResequencerUnderTest.Create().SelectSequence(null!));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => ResequencerUnderTest.Create().StartsAt(-1));
        ScenarioExpect.Throws<InvalidOperationException>(() => ResequencerUnderTest.Create().Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => ResequencerUnderTest.Create()
            .SelectSequence(static (message, _) => message.Payload.Sequence)
            .Build()
            .Accept(null!));
    }

    public sealed record Event(long Sequence, string Name);
}
