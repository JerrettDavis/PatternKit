using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;
using PatternKit.Messaging.Reliability;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates idempotent inbox processing with an in-process mailbox and outbox.
/// </summary>
public static class ReliabilityExample
{
    /// <summary>Runs an idempotent order inbox and returns dispatched outbox payloads.</summary>
    public static async ValueTask<IReadOnlyList<string>> RunAsync()
    {
        var store = new InMemoryIdempotencyStore();
        var outbox = new InMemoryOutbox<ReliabilityOrderAccepted>();
        var dispatched = new List<string>();

        var receiver = IdempotentReceiver<AcceptOrder, string>.Create(
                store,
                async (message, _, cancellationToken) =>
                {
                    await outbox.EnqueueAsync(
                        Message<ReliabilityOrderAccepted>.Create(new ReliabilityOrderAccepted(message.Payload.OrderId)),
                        id: $"accepted-{message.Payload.OrderId}",
                        cancellationToken: cancellationToken);
                    return message.Payload.OrderId;
                })
            .OnDuplicate(DuplicateMessagePolicy.ReplayCompleted)
            .Build();

        using var mailbox = Mailbox<AcceptOrder>.Create(async (message, context, cancellationToken) =>
            {
                await receiver.HandleAsync(message, context, cancellationToken);
            })
            .Build();

        await mailbox.StartAsync();

        var command = Message<AcceptOrder>
            .Create(new AcceptOrder("order-42"))
            .WithIdempotencyKey("accept-order-42");

        await mailbox.PostAsync(command);
        await mailbox.PostAsync(command);
        await mailbox.StopAsync();

        await outbox.DispatchPendingAsync(new DelegateOutboxDispatcher<ReliabilityOrderAccepted>(
            (record, _) =>
            {
                dispatched.Add(record.Message.Payload.OrderId);
                return default;
            }));

        return dispatched;
    }
}

/// <summary>Reliability example command payload.</summary>
public sealed record AcceptOrder(string OrderId);

/// <summary>Reliability example event payload.</summary>
public sealed record ReliabilityOrderAccepted(string OrderId);

internal sealed class DelegateOutboxDispatcher<TPayload> : IOutboxDispatcher<TPayload>
{
    private readonly Func<OutboxMessage<TPayload>, CancellationToken, ValueTask> _dispatch;

    internal DelegateOutboxDispatcher(Func<OutboxMessage<TPayload>, CancellationToken, ValueTask> dispatch)
    {
        _dispatch = dispatch;
    }

    public ValueTask DispatchAsync(OutboxMessage<TPayload> message, CancellationToken cancellationToken = default)
        => _dispatch(message, cancellationToken);
}
