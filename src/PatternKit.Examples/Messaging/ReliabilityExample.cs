using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;
using PatternKit.Messaging.Reliability;
using PatternKit.Generators.Messaging;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates idempotent inbox processing with an in-process mailbox and outbox.
/// </summary>
public static class ReliabilityExample
{
    /// <summary>Runs an idempotent order inbox and returns dispatched outbox payloads.</summary>
    public static ValueTask<IReadOnlyList<string>> RunAsync() => RunFluentAsync();

    /// <summary>Runs the fluent idempotent receiver and outbox path.</summary>
    public static async ValueTask<IReadOnlyList<string>> RunFluentAsync()
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

    /// <summary>Runs the generated idempotent receiver, inbox processor, and outbox path.</summary>
    public static async ValueTask<IReadOnlyList<string>> RunGeneratedAsync()
    {
        var store = new InMemoryIdempotencyStore();
        var inbox = GeneratedReliabilityOrderPipeline.CreateInbox(store);
        var outbox = GeneratedReliabilityOrderPipeline.CreateOutbox();
        var dispatched = new List<string>();

        var command = Message<AcceptOrder>
            .Create(new AcceptOrder("order-42"))
            .WithIdempotencyKey("accept-order-42");

        var first = await inbox.ProcessAsync(command);
        _ = await inbox.ProcessAsync(command);

        if (first.Processed)
        {
            await outbox.EnqueueAsync(
                Message<ReliabilityOrderAccepted>.Create(new ReliabilityOrderAccepted(first.Result!)),
                id: $"accepted-{first.Result}");
        }

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

/// <summary>DI-friendly runner exposing fluent and generated reliability paths.</summary>
public sealed record ReliabilityExampleRunner(
    Func<ValueTask<IReadOnlyList<string>>> RunFluentAsync,
    Func<ValueTask<IReadOnlyList<string>>> RunGeneratedAsync);

/// <summary>Source-generated reliability pipeline used by the production-shaped example.</summary>
[GenerateReliabilityPipeline(
    typeof(AcceptOrder),
    typeof(string),
    typeof(ReliabilityOrderAccepted),
    DuplicatePolicy = "ReplayCompleted",
    ReceiverFactoryName = "CreateOrderReceiver",
    InboxFactoryName = "CreateInbox",
    OutboxFactoryName = "CreateOutbox")]
public static partial class GeneratedReliabilityOrderPipeline
{
    [ReliabilityHandler]
    private static ValueTask<string> Handle(
        Message<AcceptOrder> message,
        MessageContext context,
        CancellationToken cancellationToken)
        => new(message.Payload.OrderId);
}

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
