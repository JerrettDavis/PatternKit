using PatternKit.Messaging;
using PatternKit.Messaging.Channels;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Channels;

public sealed class AsyncWireTapTests
{
    [Scenario("PublishAsync InvokesAllTapsAndReturnsOriginalMessage")]
    [Fact]
    public async Task PublishAsync_InvokesAllTapsAndReturnsOriginalMessage()
    {
        var observed = new List<string>();
        var tap = AsyncWireTap<Order>.Create("order-observer")
            .Tap("audit", async (m, _, _) => { observed.Add($"audit:{m.Payload.Id}"); await Task.CompletedTask; })
            .Tap("metrics", async (m, _, _) => { observed.Add($"metrics:{m.Payload.Total}"); await Task.CompletedTask; })
            .Build();
        var message = Message<Order>.Create(new Order("o-1", 125m));

        var result = await tap.PublishAsync(message);

        ScenarioExpect.Equal(message, result.Message);
        ScenarioExpect.Equal("order-observer", result.TapName);
        ScenarioExpect.Equal(2, result.TapResults.Count);
        ScenarioExpect.True(result.TapResults.All(r => r.Succeeded));
        ScenarioExpect.Equal(["audit:o-1", "metrics:125"], observed);
    }

    [Scenario("PublishAsync PassesContextToTapHandlers")]
    [Fact]
    public async Task PublishAsync_PassesContextToTapHandlers()
    {
        string? seenCorrelationId = null;
        var tap = AsyncWireTap<Order>.Create()
            .Tap("audit", async (_, ctx, _) => { seenCorrelationId = ctx.Headers.CorrelationId; await Task.CompletedTask; })
            .Build();
        var context = new MessageContext(MessageHeaders.Empty.WithCorrelationId("corr-1"));

        _ = await tap.PublishAsync(Message<Order>.Create(new Order("o-1", 125m)), context);

        ScenarioExpect.Equal("corr-1", seenCorrelationId);
    }

    [Scenario("PublishAsync SwallowPolicy DoesNotPropagateTapException")]
    [Fact]
    public async Task PublishAsync_SwallowPolicy_DoesNotPropagateTapException()
    {
        var tap = AsyncWireTap<Order>.Create()
            .Tap("failing", async (_, _, _) => { await Task.CompletedTask; throw new InvalidOperationException("tap error"); }, TapErrorPolicy.Swallow)
            .Build();
        var message = Message<Order>.Create(new Order("o-1", 100m));

        var result = await tap.PublishAsync(message);

        ScenarioExpect.Equal(message, result.Message);
        ScenarioExpect.Equal(1, result.TapResults.Count);
        ScenarioExpect.False(result.TapResults[0].Succeeded);
        ScenarioExpect.NotNull(result.TapResults[0].Exception);
    }

    [Scenario("PublishAsync LogPolicy InvokesSinkOnError")]
    [Fact]
    public async Task PublishAsync_LogPolicy_InvokesSinkOnError()
    {
        Exception? logged = null;
        var tap = AsyncWireTap<Order>.Create()
            .Tap("failing", async (_, _, _) => { await Task.CompletedTask; throw new InvalidOperationException("tap error"); },
                TapErrorPolicy.Log, ex => logged = ex)
            .Build();
        var message = Message<Order>.Create(new Order("o-1", 100m));

        var result = await tap.PublishAsync(message);

        ScenarioExpect.Equal(message, result.Message);
        ScenarioExpect.NotNull(logged);
        ScenarioExpect.False(result.TapResults[0].Succeeded);
    }

    [Scenario("PublishAsync PropagatePolicy ThrowsOnTapException")]
    [Fact]
    public async Task PublishAsync_PropagatePolicy_ThrowsOnTapException()
    {
        var tap = AsyncWireTap<Order>.Create()
            .Tap("failing", async (_, _, _) => { await Task.CompletedTask; throw new InvalidOperationException("tap error"); },
                TapErrorPolicy.Propagate)
            .Build();
        var message = Message<Order>.Create(new Order("o-1", 100m));

        await ScenarioExpect.ThrowsAsync<InvalidOperationException>(() => tap.PublishAsync(message).AsTask());
    }

    [Scenario("PublishAsync MainFlowUnaffectedBySwallowedTapFailure")]
    [Fact]
    public async Task PublishAsync_MainFlowUnaffectedBySwallowedTapFailure()
    {
        var secondTapInvoked = false;
        var tap = AsyncWireTap<Order>.Create()
            .Tap("failing", async (_, _, _) => { await Task.CompletedTask; throw new InvalidOperationException(); }, TapErrorPolicy.Swallow)
            .Tap("succeeding", async (_, _, _) => { secondTapInvoked = true; await Task.CompletedTask; })
            .Build();

        var result = await tap.PublishAsync(Message<Order>.Create(new Order("o-1", 100m)));

        ScenarioExpect.True(secondTapInvoked);
        ScenarioExpect.Equal(2, result.TapResults.Count);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => AsyncWireTap<Order>.Create(""));
        ScenarioExpect.Throws<ArgumentException>(() =>
            AsyncWireTap<Order>.Create().Tap("", async (_, _, _) => await Task.CompletedTask));
        ScenarioExpect.Throws<ArgumentNullException>(() =>
            AsyncWireTap<Order>.Create().Tap("audit", null!));
        ScenarioExpect.Throws<InvalidOperationException>(() =>
            AsyncWireTap<Order>.Create().Build());
    }

    [Scenario("Builder LogPolicy RequiresSink")]
    [Fact]
    public void Builder_LogPolicy_RequiresSink()
    {
        ScenarioExpect.Throws<ArgumentException>(() =>
            AsyncWireTap<Order>.Create()
                .Tap("audit", async (_, _, _) => await Task.CompletedTask, TapErrorPolicy.Log, null));
    }

    [Scenario("PublishAsync RejectsNullMessage")]
    [Fact]
    public async Task PublishAsync_RejectsNullMessage()
    {
        var tap = AsyncWireTap<Order>.Create()
            .Tap("audit", async (_, _, _) => await Task.CompletedTask)
            .Build();

        await ScenarioExpect.ThrowsAsync<ArgumentNullException>(() => tap.PublishAsync(null!).AsTask());
    }

    [Scenario("PublishAsync RespectsCancellationToken")]
    [Fact]
    public async Task PublishAsync_RespectsCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Propagate policy will propagate OperationCanceledException
        var tap2 = AsyncWireTap<Order>.Create()
            .Tap("audit", async (_, _, ct) => { await Task.Delay(100, ct); }, TapErrorPolicy.Propagate)
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(
            () => tap2.PublishAsync(Message<Order>.Create(new Order("o-1", 100m)), cancellationToken: cts.Token).AsTask());
    }

    [Scenario("PublishAsync SwallowPolicy ReThrowsOCEOnCallerCancellation")]
    [Fact]
    public async Task PublishAsync_SwallowPolicy_ReThrowsOCEOnCallerCancellation()
    {
        // Swallow policy must NOT suppress OperationCanceledException when the caller's CT is cancelled.
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var tap = AsyncWireTap<Order>.Create()
            .Tap("audit", async (_, _, ct) => { await Task.Delay(100, ct); }, TapErrorPolicy.Swallow)
            .Build();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(
            () => tap.PublishAsync(Message<Order>.Create(new Order("o-1", 100m)), cancellationToken: cts.Token).AsTask());
    }

    private sealed record Order(string Id, decimal Total);
}
