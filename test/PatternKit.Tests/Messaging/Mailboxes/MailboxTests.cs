using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;

namespace PatternKit.Tests.Messaging.Mailboxes;

public sealed class MailboxTests
{
    [Fact]
    public async Task PostAsync_ProcessesMessagesInOrder()
    {
        var processed = new List<int>();
        using var mailbox = Mailbox<int>.Create((message, _, _) =>
            {
                processed.Add(message.Payload);
                return default;
            })
            .Build();

        await mailbox.StartAsync();
        var first = await mailbox.PostAsync(Message<int>.Create(1));
        var second = await mailbox.PostAsync(Message<int>.Create(2));
        await mailbox.StopAsync();

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.Equal([1, 2], processed);
        Assert.Equal(1, first.Sequence);
        Assert.Equal(2, second.Sequence);
    }

    [Fact]
    public async Task PostAsync_SerializesConcurrentPosts()
    {
        var active = 0;
        var maxActive = 0;
        var processed = 0;
        using var mailbox = Mailbox<int>.Create(async (_, _, _) =>
            {
                var nowActive = Interlocked.Increment(ref active);
                maxActive = Math.Max(maxActive, nowActive);
                await Task.Delay(2);
                Interlocked.Increment(ref processed);
                Interlocked.Decrement(ref active);
            })
            .Build();

        await mailbox.StartAsync();
        var posts = Enumerable.Range(0, 50)
            .Select(value => mailbox.PostAsync(Message<int>.Create(value)).AsTask())
            .ToArray();
        var results = await Task.WhenAll(posts);
        await mailbox.StopAsync();

        Assert.All(results, result => Assert.True(result.Accepted));
        Assert.Equal(50, processed);
        Assert.Equal(1, maxActive);
    }

    [Fact]
    public async Task BoundedRejectPolicy_RejectsWhenFull()
    {
        var started = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var mailbox = Mailbox<int>.Create(async (message, _, _) =>
            {
                if (message.Payload == 1)
                {
                    started.SetResult(null);
                    await release.Task;
                }
            })
            .Bounded(1, MailboxBackpressurePolicy.Reject)
            .Build();

        await mailbox.StartAsync();
        var first = await mailbox.PostAsync(Message<int>.Create(1));
        await started.Task;
        var second = await mailbox.PostAsync(Message<int>.Create(2));
        var third = await mailbox.PostAsync(Message<int>.Create(3));
        release.SetResult(null);
        await mailbox.StopAsync();

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.Equal(MailboxPostStatus.Rejected, third.Status);
        Assert.Equal("mailbox-full", third.Reason);
    }

    [Fact]
    public async Task BoundedDropNewestPolicy_DropsIncomingWhenFull()
    {
        var started = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = new List<int>();
        using var mailbox = Mailbox<int>.Create(async (message, _, _) =>
            {
                processed.Add(message.Payload);
                if (message.Payload == 1)
                {
                    started.SetResult(null);
                    await release.Task;
                }
            })
            .Bounded(1, MailboxBackpressurePolicy.DropNewest)
            .Build();

        await mailbox.StartAsync();
        var first = await mailbox.PostAsync(Message<int>.Create(1));
        await started.Task;
        var second = await mailbox.PostAsync(Message<int>.Create(2));
        var third = await mailbox.PostAsync(Message<int>.Create(3));
        release.SetResult(null);
        await mailbox.StopAsync();

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.Equal(MailboxPostStatus.Dropped, third.Status);
        Assert.Equal([1, 2], processed);
    }

    [Fact]
    public async Task BoundedDropOldestPolicy_DropsOldestQueuedMessage()
    {
        var started = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = new List<int>();
        using var mailbox = Mailbox<int>.Create(async (message, _, _) =>
            {
                processed.Add(message.Payload);
                if (message.Payload == 1)
                {
                    started.SetResult(null);
                    await release.Task;
                }
            })
            .Bounded(1, MailboxBackpressurePolicy.DropOldest)
            .Build();

        await mailbox.StartAsync();
        var first = await mailbox.PostAsync(Message<int>.Create(1));
        await started.Task;
        var second = await mailbox.PostAsync(Message<int>.Create(2));
        var third = await mailbox.PostAsync(Message<int>.Create(3));
        release.SetResult(null);
        await mailbox.StopAsync();

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
        Assert.True(third.Accepted);
        Assert.Equal([1, 3], processed);
    }

    [Fact]
    public async Task BoundedWaitPolicy_WaitsForQueueSpace()
    {
        var started = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var mailbox = Mailbox<int>.Create(async (message, _, _) =>
            {
                if (message.Payload == 1)
                {
                    started.SetResult(null);
                    await release.Task;
                }
            })
            .Bounded(1)
            .Build();

        await mailbox.StartAsync();
        await mailbox.PostAsync(Message<int>.Create(1));
        await started.Task;
        await mailbox.PostAsync(Message<int>.Create(2));
        var third = mailbox.PostAsync(Message<int>.Create(3)).AsTask();

        Assert.False(third.IsCompleted);

        release.SetResult(null);
        var result = await third;
        await mailbox.StopAsync();

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task StopAsync_WhenDrainFalse_DropsQueuedMessages()
    {
        var started = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processed = new List<int>();
        using var mailbox = Mailbox<int>.Create(async (message, _, cancellationToken) =>
            {
                processed.Add(message.Payload);
                if (message.Payload == 1)
                {
                    started.SetResult(null);
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
            })
            .Bounded(2, MailboxBackpressurePolicy.Reject)
            .Build();

        await mailbox.StartAsync();
        await mailbox.PostAsync(Message<int>.Create(1));
        await started.Task;
        await mailbox.PostAsync(Message<int>.Create(2));
        await mailbox.PostAsync(Message<int>.Create(3));
        await mailbox.StopAsync(drain: false);

        Assert.Equal([1], processed);
    }

    [Fact]
    public async Task ErrorPolicyContinue_RoutesFailureAndContinues()
    {
        var errors = new List<string>();
        var processed = new List<int>();
        using var mailbox = Mailbox<int>.Create((message, _, _) =>
            {
                processed.Add(message.Payload);
                if (message.Payload == 1)
                    throw new InvalidOperationException("boom");

                return default;
            })
            .OnError(
                MailboxErrorPolicy.Continue,
                (exception, _, _, _) =>
                {
                    errors.Add(exception.Message);
                    return default;
                })
            .Build();

        await mailbox.StartAsync();
        await mailbox.PostAsync(Message<int>.Create(1));
        await mailbox.PostAsync(Message<int>.Create(2));
        await mailbox.StopAsync();

        Assert.Equal(["boom"], errors);
        Assert.Equal([1, 2], processed);
    }

    [Fact]
    public async Task ErrorPolicyStop_StopsAcceptingAndDropsQueuedMessages()
    {
        using var mailbox = Mailbox<int>.Create((message, _, _) =>
            {
                if (message.Payload == 1)
                    throw new InvalidOperationException("boom");

                return default;
            })
            .Bounded(4, MailboxBackpressurePolicy.Reject)
            .OnError(MailboxErrorPolicy.Stop)
            .Build();

        await mailbox.StartAsync();
        await mailbox.PostAsync(Message<int>.Create(1));
        await mailbox.PostAsync(Message<int>.Create(2));
        await mailbox.StopAsync();

        var afterFailure = await mailbox.PostAsync(Message<int>.Create(3));

        Assert.Equal(MailboxPostStatus.Rejected, afterFailure.Status);
        Assert.False(mailbox.IsAccepting);
    }

    [Fact]
    public async Task PostAsync_PassesMessageContextAndCancellation()
    {
        using var source = new CancellationTokenSource();
        var observed = CancellationToken.None;
        var context = new MessageContext().WithItem("tenant", "north").WithCancellation(source.Token);
        var tenant = string.Empty;
        using var mailbox = Mailbox<int>.Create((_, ctx, cancellationToken) =>
            {
                observed = cancellationToken;
                tenant = ctx.TryGetItem<string>("tenant", out var value) ? value! : string.Empty;
                return default;
            })
            .Build();

        await mailbox.StartAsync();
        await mailbox.PostAsync(Message<int>.Create(1), context);
        await mailbox.StopAsync();

        Assert.Equal("north", tenant);
        Assert.True(observed.CanBeCanceled);
    }

    [Fact]
    public async Task OnEvent_ReceivesLifecycleAndProcessingEvents()
    {
        var events = new List<MailboxEvent>();
        using var mailbox = Mailbox<int>.Create((_, _, _) => default)
            .OnEvent(events.Add)
            .Build();

        await mailbox.StartAsync();
        await mailbox.PostAsync(Message<int>.Create(1));
        await mailbox.StopAsync();

        Assert.Contains(events, evt => evt.Kind == MailboxEventKind.Started);
        Assert.Contains(events, evt => evt.Kind == MailboxEventKind.Accepted && evt.Sequence == 1);
        Assert.Contains(events, evt => evt.Kind == MailboxEventKind.ProcessingStarted && evt.QueuedCount >= 0);
        Assert.Contains(events, evt => evt.Kind == MailboxEventKind.ProcessingCompleted && evt.Exception is null);
        Assert.Contains(events, evt => evt.Kind == MailboxEventKind.Stopped);
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyStarted_IsNoOp()
    {
        using var mailbox = Mailbox<int>.Create((_, _, _) => default).Build();

        await mailbox.StartAsync();
        await mailbox.StartAsync();
        var result = await mailbox.PostAsync(Message<int>.Create(1));
        await mailbox.StopAsync();

        Assert.True(result.Accepted);
    }

    [Fact]
    public async Task OnEvent_WhenSinkThrows_DoesNotAffectProcessing()
    {
        var acceptedEvents = 0;
        using var mailbox = Mailbox<int>.Create((_, _, _) => default)
            .Bounded(1)
            .OnEvent(evt =>
            {
                if (evt.Kind == MailboxEventKind.Accepted && Interlocked.Increment(ref acceptedEvents) == 1)
                {
                    throw new InvalidOperationException("event failed");
                }
            })
            .Build();

        await mailbox.StartAsync();
        var first = await mailbox.PostAsync(Message<int>.Create(1));
        var second = await mailbox.PostAsync(Message<int>.Create(2));
        await mailbox.StopAsync();

        Assert.True(first.Accepted);
        Assert.True(second.Accepted);
    }

    [Fact]
    public async Task BoundedWaitPolicy_RejectsAfterStop()
    {
        using var mailbox = Mailbox<int>.Create((_, _, _) => default)
            .Bounded(1)
            .Build();

        await mailbox.StartAsync();
        await mailbox.StopAsync();
        var result = await mailbox.PostAsync(Message<int>.Create(1));

        Assert.Equal(MailboxPostStatus.Rejected, result.Status);
        Assert.Equal("mailbox-not-accepting", result.Reason);
    }

    [Fact]
    public async Task StopAsync_ObservesStopCancellation()
    {
        var started = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var source = new CancellationTokenSource();
        using var mailbox = Mailbox<int>.Create(async (_, _, _) =>
            {
                started.SetResult(null);
                await release.Task;
            })
            .Build();

        await mailbox.StartAsync();
        await mailbox.PostAsync(Message<int>.Create(1));
        await started.Task;
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await mailbox.StopAsync(cancellationToken: source.Token));

        release.SetResult(null);
        await mailbox.StopAsync();
    }

    [Fact]
    public async Task Properties_ReportCapacityAndQueuedCount()
    {
        var started = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var mailbox = Mailbox<int>.Create(async (message, _, _) =>
            {
                if (message.Payload == 1)
                {
                    started.SetResult(null);
                    await release.Task;
                }
            })
            .Bounded(2)
            .Build();

        await mailbox.StartAsync();
        await mailbox.PostAsync(Message<int>.Create(1));
        await started.Task;
        await mailbox.PostAsync(Message<int>.Create(2));

        Assert.Equal(2, mailbox.Capacity);
        Assert.Equal(1, mailbox.QueuedCount);

        release.SetResult(null);
        await mailbox.StopAsync();
    }

    [Fact]
    public void Dispose_IsIdempotentAndRejectsLaterUse()
    {
        var mailbox = Mailbox<int>.Create((_, _, _) => default).Build();

        mailbox.Dispose();
        mailbox.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { _ = mailbox.StartAsync(); });
    }

    [Fact]
    public async Task PostAsync_ObservesCanceledEnqueueToken()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        using var mailbox = Mailbox<int>.Create((_, _, _) => default).Build();

        await mailbox.StartAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await mailbox.PostAsync(Message<int>.Create(1), cancellationToken: source.Token));

        await mailbox.StopAsync();
    }

    [Fact]
    public void Builder_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentNullException>(() => Mailbox<int>.Create(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Mailbox<int>.Create((_, _, _) => default).Bounded(0));
        Assert.Throws<ArgumentNullException>(() => Mailbox<int>.Create((_, _, _) => default).OnEvent(null!));

        var mailbox = Mailbox<int>.Create((_, _, _) => default).Unbounded().Build();
        Assert.Null(mailbox.Capacity);
    }

    [Fact]
    public async Task PostAsync_RejectsNullMessage()
    {
        using var mailbox = Mailbox<int>.Create((_, _, _) => default).Build();

        await Assert.ThrowsAsync<ArgumentNullException>(async () => await mailbox.PostAsync(null!));
    }
}
