using PatternKit.Messaging;
using PatternKit.Messaging.Routing;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Routing;

public sealed class AsyncScatterGatherTests
{
    [Scenario("DispatchAsync CollectsAllRecipientResponses")]
    [Fact]
    public async Task DispatchAsync_CollectsAllRecipientResponses()
    {
        var sg = AsyncScatterGather<string, int, int>.Create()
            .Recipient("a", async (m, _, _) => { await Task.Delay(10); return 1; })
            .Recipient("b", async (m, _, _) => { await Task.Delay(5); return 2; })
            .Recipient("c", async (m, _, _) => { await Task.Delay(1); return 3; })
            .CompleteWith(CompletionStrategy.All)
            .WithAggregator((envelopes, _, _) => envelopes.Where(e => e.Succeeded).Sum(e => e.Response))
            .Build();

        var result = await sg.DispatchAsync(Message<string>.Create("test"));

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal(6, result.Result);
        ScenarioExpect.Equal(3, result.Envelopes.Count);
    }

    [Scenario("DispatchAsync PerBranchErrorIsolation")]
    [Fact]
    public async Task DispatchAsync_PerBranchErrorIsolation()
    {
        var sg = AsyncScatterGather<string, int, int>.Create()
            .Recipient("good", async (m, _, _) => { await Task.CompletedTask; return 42; })
            .Recipient("bad", async (m, _, _) => { await Task.CompletedTask; throw new InvalidOperationException("branch error"); })
            .WithAggregator((envelopes, _, _) => envelopes.Where(e => e.Succeeded).Sum(e => e.Response))
            .Build();

        var result = await sg.DispatchAsync(Message<string>.Create("test"));

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal(42, result.Result);
        var failedEnvelope = ScenarioExpect.Single(result.Envelopes, e => !e.Succeeded);
        ScenarioExpect.NotNull(failedEnvelope.Exception);
    }

    [Scenario("DispatchAsync TimeoutStrategy PartialResults")]
    [Fact]
    public async Task DispatchAsync_TimeoutStrategy_PartialResults()
    {
        var sg = AsyncScatterGather<string, int, int>.Create()
            .Recipient("fast", (m, _, _) => ValueTask.FromResult(1))
            .Recipient("slow", async (m, _, ct) => { await Task.Delay(5000, ct); return 2; })
            .CompleteWith(CompletionStrategy.Timeout(TimeSpan.FromMilliseconds(200)))
            .WithAggregator((envelopes, _, _) => envelopes.Where(e => e.Succeeded).Sum(e => e.Response))
            .Build();

        var result = await sg.DispatchAsync(Message<string>.Create("test"));

        // At least one result came back (fast one)
        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.True(result.Result >= 1);
    }

    [Scenario("DispatchAsync FirstN Strategy StopsAfterN")]
    [Fact]
    public async Task DispatchAsync_FirstNStrategy_StopsAfterN()
    {
        var sg = AsyncScatterGather<string, int, int>.Create()
            .Recipient("a", async (m, _, _) => { await Task.Delay(10); return 1; })
            .Recipient("b", async (m, _, _) => { await Task.Delay(10); return 2; })
            .Recipient("c", async (m, _, _) => { await Task.Delay(10); return 3; })
            .CompleteWith(CompletionStrategy.FirstN(2))
            .WithAggregator((envelopes, _, _) => envelopes.Count(e => e.Succeeded))
            .Build();

        var result = await sg.DispatchAsync(Message<string>.Create("test"));

        ScenarioExpect.True(result.Succeeded);
        // Should have at least 2 successful results
        ScenarioExpect.True(result.Result >= 2);
    }

    [Scenario("DispatchAsync QuorumStrategy WaitsForQuorum")]
    [Fact]
    public async Task DispatchAsync_QuorumStrategy_WaitsForQuorum()
    {
        var sg = AsyncScatterGather<string, int, int>.Create()
            .Recipient("a", async (m, _, _) => { await Task.Delay(5); return 10; })
            .Recipient("b", async (m, _, _) => { await Task.Delay(10); return 20; })
            .Recipient("c", async (m, _, _) => { await Task.Delay(15); return 30; })
            .CompleteWith(CompletionStrategy.Quorum(2))
            .WithAggregator((envelopes, _, _) => envelopes.Where(e => e.Succeeded).Sum(e => e.Response))
            .Build();

        var result = await sg.DispatchAsync(Message<string>.Create("test"));

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.True(result.Result >= 30); // at least a and b responded
    }

    [Scenario("DispatchAsync QuorumStrategy CountsFailedRecipientsTowardQuorum")]
    [Fact]
    public async Task DispatchAsync_QuorumStrategy_CountsFailedRecipientsTowardQuorum()
    {
        // A failing recipient counts toward quorum — quorum means "any N responses", not "N successes".
        var completedCount = 0;
        var sg = AsyncScatterGather<string, int, int>.Create()
            .Recipient("failing", async (m, _, _) => { await Task.CompletedTask; throw new InvalidOperationException("recipient error"); })
            .Recipient("slow", async (m, _, ct) => { await Task.Delay(5000, ct); Interlocked.Increment(ref completedCount); return 99; })
            .CompleteWith(CompletionStrategy.Quorum(1))
            .WithAggregator((envelopes, _, _) => envelopes.Count())
            .Build();

        var result = await sg.DispatchAsync(Message<string>.Create("test"));

        // Quorum(1) satisfied by the failing recipient; slow recipient was cancelled before completing.
        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal(0, completedCount); // slow never completed
    }

    [Scenario("DispatchAsync AllFail AggregatorReceivesFailedEnvelopes")]
    [Fact]
    public async Task DispatchAsync_AllFail_AggregatorReceivesFailedEnvelopes()
    {
        var sg = AsyncScatterGather<string, int, int>.Create()
            .Recipient("bad", async (m, _, _) => { await Task.CompletedTask; throw new InvalidOperationException(); })
            .WithAggregator((envelopes, _, _) => envelopes.Count(e => !e.Succeeded))
            .Build();

        var result = await sg.DispatchAsync(Message<string>.Create("test"));

        // Aggregator runs even if all recipients failed; it receives the failed envelopes
        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal(1, result.Result); // one failed envelope
        ScenarioExpect.Equal(1, result.Envelopes.Count(e => !e.Succeeded));
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() =>
            AsyncScatterGather<string, int, int>.Create(""));
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncScatterGather<string, int, int>.Create().WithAggregator((e, _, _) => 0).Build());
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncScatterGather<string, int, int>.Create()
                .Recipient("a", async (m, _, _) => { await Task.CompletedTask; return 0; })
                .Build());
    }

    [Scenario("DispatchAsync RejectsNullMessage")]
    [Fact]
    public async Task DispatchAsync_RejectsNullMessage()
    {
        var sg = AsyncScatterGather<string, int, int>.Create()
            .Recipient("a", async (m, _, _) => { await Task.CompletedTask; return 0; })
            .WithAggregator((e, _, _) => 0)
            .Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => sg.DispatchAsync(null!).AsTask());
    }
}
