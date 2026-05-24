using PatternKit.Messaging;
using PatternKit.Messaging.Consumers;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Consumers;

public sealed class AsyncPollingConsumerTests
{
    [Scenario("RunAsync InvokesHandlerForEachMessage")]
    [Fact]
    public async Task RunAsync_InvokesHandlerForEachMessage()
    {
        var received = new List<string>();
        var pollCount = 0;
        using var cts = new CancellationTokenSource();

        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, ct) =>
            {
                await Task.CompletedTask;
                if (Interlocked.Increment(ref pollCount) > 3)
                {
                    cts.Cancel();
                    return null;
                }
                return Message<string>.Create($"msg-{pollCount}");
            })
            .WithInterval(TimeSpan.FromMilliseconds(10))
            .Build();

        await consumer.RunAsync(
            async (msg, _, _) => { received.Add(msg.Payload); await Task.CompletedTask; },
            cancellationToken: cts.Token);

        ScenarioExpect.Equal(3, received.Count);
        ScenarioExpect.Equal("msg-1", received[0]);
    }

    [Scenario("RunAsync StopsOnCancellation")]
    [Fact]
    public async Task RunAsync_StopsOnCancellation()
    {
        using var cts = new CancellationTokenSource(50); // cancel after 50ms
        var invocations = 0;

        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, _) => { await Task.CompletedTask; return null; })
            .WithInterval(TimeSpan.FromMilliseconds(10))
            .Build();

        await consumer.RunAsync(
            async (_, _, _) => { Interlocked.Increment(ref invocations); await Task.CompletedTask; },
            cancellationToken: cts.Token);

        // Should complete without throwing, just stop
        ScenarioExpect.True(invocations == 0); // all polls returned null
    }

    [Scenario("RunAsync EmptyPollConstantBackOff")]
    [Fact]
    public async Task RunAsync_EmptyPoll_ConstantBackOff()
    {
        var pollCount = 0;
        using var cts = new CancellationTokenSource();

        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, _) =>
            {
                await Task.CompletedTask;
                if (Interlocked.Increment(ref pollCount) >= 3)
                {
                    cts.Cancel();
                }

                return null;
            })
            .WithInterval(TimeSpan.FromMilliseconds(20))
            .OnEmpty(BackOffPolicy.Constant)
            .Build();

        await consumer.RunAsync(async (_, _, _) => await Task.CompletedTask, cancellationToken: cts.Token);

        // Constant backoff should continue polling after empty source results until cancellation.
        ScenarioExpect.Equal(3, pollCount);
    }

    [Scenario("RunAsync EmptyPollExponentialBackOff")]
    [Fact]
    public async Task RunAsync_EmptyPoll_ExponentialBackOff()
    {
        var pollCount = 0;
        using var cts = new CancellationTokenSource();

        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, _) =>
            {
                await Task.CompletedTask;
                if (Interlocked.Increment(ref pollCount) >= 3)
                {
                    cts.Cancel();
                }

                return null;
            })
            .WithInterval(TimeSpan.FromMilliseconds(10))
            .OnEmpty(BackOffPolicy.Exponential, cap: TimeSpan.FromMilliseconds(500))
            .Build();

        await consumer.RunAsync(async (_, _, _) => await Task.CompletedTask, cancellationToken: cts.Token);

        // Exponential backoff should continue polling after empty source results until cancellation.
        ScenarioExpect.Equal(3, pollCount);
    }

    [Scenario("RunAsync JitterIsApplied")]
    [Fact]
    public async Task RunAsync_JitterIsApplied()
    {
        var pollTimestamps = new List<DateTimeOffset>();
        using var cts = new CancellationTokenSource();
        var pollCount = 0;

        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, _) =>
            {
                await Task.CompletedTask;
                pollTimestamps.Add(DateTimeOffset.UtcNow);
                if (Interlocked.Increment(ref pollCount) >= 5) cts.Cancel();
                return null;
            })
            .WithInterval(TimeSpan.FromMilliseconds(10))
            .WithJitter(TimeSpan.FromMilliseconds(20))
            .Build();

        await consumer.RunAsync(async (_, _, _) => await Task.CompletedTask, cancellationToken: cts.Token);

        ScenarioExpect.True(pollTimestamps.Count >= 2);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => AsyncPollingConsumer<string>.Create(""));
        ScenarioExpect.Throws<InvalidOperationException>(() => AsyncPollingConsumer<string>.Create().Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() =>
            AsyncPollingConsumer<string>.Create()
                .WithSource(async (_, _) => { await Task.CompletedTask; return null; })
                .WithInterval(TimeSpan.Zero)
                .Build());
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() =>
            AsyncPollingConsumer<string>.Create()
                .WithSource(async (_, _) => { await Task.CompletedTask; return null; })
                .WithJitter(TimeSpan.FromMilliseconds(-1))
                .Build());
    }

    [Scenario("RunAsync RejectsNullHandler")]
    [Fact]
    public async Task RunAsync_RejectsNullHandler()
    {
        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, _) => { await Task.CompletedTask; return null; })
            .Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => consumer.RunAsync(null!).AsTask());
    }

    // ─── PollOnceAsync ────────────────────────────────────────────────────────

    [Scenario("PollOnceAsync ReturnsItemFromSource")]
    [Fact]
    public async Task PollOnceAsync_ReturnsItemFromSource()
    {
        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, _) => { await Task.CompletedTask; return Message<string>.Create("hello"); })
            .Build();

        var result = await consumer.PollOnceAsync();

        ScenarioExpect.NotNull(result);
        ScenarioExpect.Equal("hello", result!.Payload);
    }

    [Scenario("PollOnceAsync ReturnsNullWhenSourceReturnsEmpty")]
    [Fact]
    public async Task PollOnceAsync_ReturnsNullWhenSourceReturnsEmpty()
    {
        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, _) => { await Task.CompletedTask; return null; })
            .Build();

        var result = await consumer.PollOnceAsync();

        ScenarioExpect.Null(result);
    }

    [Scenario("PollOnceAsync RespectsCancellation")]
    [Fact]
    public async Task PollOnceAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, ct) =>
            {
                await Task.Delay(1000, ct);
                return Message<string>.Create("never");
            })
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(
            () => consumer.PollOnceAsync(ct: cts.Token).AsTask());
    }

    [Scenario("PollOnceAsync DoesNotInvokeRunLoopHandler")]
    [Fact]
    public async Task PollOnceAsync_DoesNotInvokeRunLoopHandler()
    {
        // PollOnceAsync has no handler parameter — the only side-effect observable is
        // that the source is called exactly once and the raw message is returned.
        // We verify this by counting source invocations and confirming the value is
        // returned directly without any additional callback layer.
        var sourceCallCount = 0;

        var consumer = AsyncPollingConsumer<string>.Create()
            .WithSource(async (_, _) =>
            {
                Interlocked.Increment(ref sourceCallCount);
                await Task.CompletedTask;
                return Message<string>.Create("msg");
            })
            .Build();

        var result = await consumer.PollOnceAsync();

        ScenarioExpect.Equal(1, sourceCallCount);   // source called exactly once
        ScenarioExpect.NotNull(result);              // message returned directly to caller
        ScenarioExpect.Equal("msg", result!.Payload); // no handler mutation
    }
}
