using System.Collections.Concurrent;
using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Production-shaped mailbox demo where inventory, payment, shipping, and notification services collaborate.
/// </summary>
public static class ServiceCollaborationMailboxDemo
{
    /// <summary>Runs two checkout messages through collaborating service mailboxes.</summary>
    public static async ValueTask<ServiceCollaborationSummary> RunAsync()
    {
        var audit = new ConcurrentQueue<string>();
        var reservations = new ConcurrentDictionary<string, string>();
        var notifications = new ConcurrentQueue<OrderNotification>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Mailbox<InventoryCommand>? inventory = null;
        Mailbox<PaymentCommand>? payments = null;
        Mailbox<ShippingCommand>? shipping = null;
        Mailbox<NotificationCommand>? notification = null;

        notification = Mailbox<NotificationCommand>.Create((message, context, _) =>
            {
                audit.Enqueue($"notification:{message.Payload.OrderId}:{message.Payload.Status}");
                notifications.Enqueue(new OrderNotification(
                    message.Payload.OrderId,
                    message.Payload.Status,
                    context.Headers.GetString(MessageHeaderNames.CorrelationId)!));

                if (notifications.Count == 2)
                    completed.TrySetResult();

                return default;
            })
            .Bounded(16, MailboxBackpressurePolicy.Wait)
            .OnError(MailboxErrorPolicy.Continue)
            .Build();

        shipping = Mailbox<ShippingCommand>.Create(async (message, context, cancellationToken) =>
            {
                audit.Enqueue($"shipping:scheduled:{message.Payload.OrderId}");
                await notification!.PostAsync(
                    Message<NotificationCommand>.Create(new NotificationCommand(message.Payload.OrderId, "fulfilled")),
                    context,
                    cancellationToken);
            })
            .Bounded(16, MailboxBackpressurePolicy.Wait)
            .OnError(MailboxErrorPolicy.Continue)
            .Build();

        payments = Mailbox<PaymentCommand>.Create(async (message, context, cancellationToken) =>
            {
                if (message.Payload.Amount > 100m)
                {
                    audit.Enqueue($"payment:declined:{message.Payload.OrderId}");
                    await inventory!.PostAsync(
                        Message<InventoryCommand>.Create(InventoryCommand.Release(message.Payload.OrderId)),
                        context,
                        cancellationToken);
                    await notification!.PostAsync(
                        Message<NotificationCommand>.Create(new NotificationCommand(message.Payload.OrderId, "payment-declined")),
                        context,
                        cancellationToken);
                    return;
                }

                audit.Enqueue($"payment:captured:{message.Payload.OrderId}");
                await shipping!.PostAsync(
                    Message<ShippingCommand>.Create(new ShippingCommand(message.Payload.OrderId)),
                    context,
                    cancellationToken);
            })
            .Bounded(16, MailboxBackpressurePolicy.Wait)
            .OnError(MailboxErrorPolicy.Continue)
            .Build();

        inventory = Mailbox<InventoryCommand>.Create(async (message, context, cancellationToken) =>
            {
                if (message.Payload.Kind == InventoryCommandKind.Release)
                {
                    if (reservations.TryRemove(message.Payload.OrderId, out var reservationId))
                        audit.Enqueue($"inventory:released:{message.Payload.OrderId}:{reservationId}");
                    return;
                }

                var reserved = $"res-{message.Payload.OrderId}";
                reservations[message.Payload.OrderId] = reserved;
                audit.Enqueue($"inventory:reserved:{message.Payload.OrderId}:{reserved}");
                await payments!.PostAsync(
                    Message<PaymentCommand>.Create(new PaymentCommand(message.Payload.OrderId, message.Payload.Amount)),
                    context,
                    cancellationToken);
            })
            .Bounded(16, MailboxBackpressurePolicy.Wait)
            .OnError(MailboxErrorPolicy.Continue)
            .Build();

        await notification.StartAsync();
        await shipping.StartAsync();
        await payments.StartAsync();
        await inventory.StartAsync();

        var approvedContext = new MessageContext(MessageHeaders.Empty.WithCorrelationId("checkout-ok"));
        var declinedContext = new MessageContext(MessageHeaders.Empty.WithCorrelationId("checkout-declined"));

        await inventory.PostAsync(
            Message<InventoryCommand>.Create(InventoryCommand.Reserve("order-ok", 75m)),
            approvedContext);
        await inventory.PostAsync(
            Message<InventoryCommand>.Create(InventoryCommand.Reserve("order-declined", 125m)),
            declinedContext);

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await inventory.StopAsync();
        await payments.StopAsync();
        await shipping.StopAsync();
        await notification.StopAsync();

        return new ServiceCollaborationSummary(
            audit.ToArray(),
            notifications.ToArray(),
            reservations.Keys.Order(StringComparer.Ordinal).ToArray());
    }
}

/// <summary>Inventory service command for the mailbox collaboration demo.</summary>
public sealed record InventoryCommand(string OrderId, decimal Amount, InventoryCommandKind Kind)
{
    /// <summary>Creates a reserve command.</summary>
    public static InventoryCommand Reserve(string orderId, decimal amount) => new(orderId, amount, InventoryCommandKind.Reserve);

    /// <summary>Creates a release command.</summary>
    public static InventoryCommand Release(string orderId) => new(orderId, 0m, InventoryCommandKind.Release);
}

/// <summary>Inventory command kind.</summary>
public enum InventoryCommandKind { Reserve, Release }

/// <summary>Payment service command for the mailbox collaboration demo.</summary>
public sealed record PaymentCommand(string OrderId, decimal Amount);

/// <summary>Shipping service command for the mailbox collaboration demo.</summary>
public sealed record ShippingCommand(string OrderId);

/// <summary>Notification service command for the mailbox collaboration demo.</summary>
public sealed record NotificationCommand(string OrderId, string Status);

/// <summary>Notification emitted by the collaboration demo.</summary>
public sealed record OrderNotification(string OrderId, string Status, string CorrelationId);

/// <summary>Summary returned by the collaboration demo.</summary>
public sealed record ServiceCollaborationSummary(
    IReadOnlyList<string> Audit,
    IReadOnlyList<OrderNotification> Notifications,
    IReadOnlyList<string> OpenReservations);
