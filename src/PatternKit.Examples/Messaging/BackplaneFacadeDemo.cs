using System.Collections.Concurrent;
using PatternKit.Messaging;
using PatternKit.Messaging.Mailboxes;
using PatternKit.Messaging.Reliability;
using PatternKit.Messaging.Routing;

namespace PatternKit.Examples.Messaging;

/// <summary>
/// Demonstrates a MassTransit/MediatR-style application facade backed by PatternKit messaging patterns.
/// </summary>
public static class BackplaneFacadeDemo
{
    /// <summary>
    /// Runs an in-memory e-commerce workflow through a pluggable backplane facade.
    /// </summary>
    public static async ValueTask<BackplaneDemoSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        await using var transport = new InMemoryBackplaneTransport();
        var outbox = new BackplaneOutbox();
        var bus = new BackplaneBus(transport, outbox);
        var idempotency = new InMemoryIdempotencyStore();
        var audit = new ConcurrentQueue<string>();
        var endpoints = new ConcurrentDictionary<string, string>();
        var notifications = new ConcurrentQueue<CustomerNotification>();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.Route<SubmitOrder>(
            static (message, _) => message.Payload.CustomerTier == CustomerTier.Vip,
            "orders.priority");
        bus.RouteDefault<SubmitOrder>("orders.standard");

        await bus.HandleAsync<SubmitOrder, BackplaneOrderAccepted>(
            "orders.standard",
            async (message, context, token) =>
                await AcceptOrderAsync("orders.standard", message, context, token).ConfigureAwait(false),
            idempotency,
            cancellationToken);

        await bus.HandleAsync<SubmitOrder, BackplaneOrderAccepted>(
            "orders.priority",
            async (message, context, token) =>
                await AcceptOrderAsync("orders.priority", message, context, token).ConfigureAwait(false),
            idempotency,
            cancellationToken);

        await bus.SubscribeAsync<BackplaneOrderSubmitted>(
            "orders.submitted",
            "billing-service",
            async (message, context, token) =>
            {
                audit.Enqueue($"billing:received:{message.Payload.OrderId}");
                if (message.Payload.Total > 300m)
                {
                    await bus.PublishAsync(
                        "payments.declined",
                        new PaymentDeclined(message.Payload.OrderId, "authorization-declined"),
                        context.Headers,
                        token).ConfigureAwait(false);
                    return;
                }

                await bus.PublishAsync(
                    "payments.captured",
                    new PaymentCaptured(message.Payload.OrderId, message.Payload.Total),
                    context.Headers,
                    token).ConfigureAwait(false);
            },
            cancellationToken);

        await bus.SubscribeAsync<BackplaneOrderSubmitted>(
            "orders.submitted",
            "audit-service",
            (message, context, _) =>
            {
                audit.Enqueue($"audit:order-submitted:{message.Payload.OrderId}:{context.Headers.CorrelationId}");
                return default;
            },
            cancellationToken);

        await bus.SubscribeAsync<PaymentCaptured>(
            "payments.captured",
            "fulfillment-service",
            async (message, context, token) =>
            {
                audit.Enqueue($"fulfillment:scheduled:{message.Payload.OrderId}");
                await bus.PublishAsync(
                    "shipments.scheduled",
                    new ShipmentScheduled(message.Payload.OrderId, $"trk-{message.Payload.OrderId}"),
                    context.Headers,
                    token).ConfigureAwait(false);
            },
            cancellationToken);

        await bus.SubscribeAsync<PaymentDeclined>(
            "payments.declined",
            "notification-service",
            (message, context, _) =>
            {
                RecordNotification(new CustomerNotification(
                    message.Payload.OrderId,
                    "payment-declined",
                    context.Headers.CorrelationId ?? string.Empty));
                return default;
            },
            cancellationToken);

        await bus.SubscribeAsync<ShipmentScheduled>(
            "shipments.scheduled",
            "notification-service",
            (message, context, _) =>
            {
                RecordNotification(new CustomerNotification(
                    message.Payload.OrderId,
                    "shipment-scheduled",
                    context.Headers.CorrelationId ?? string.Empty));
                return default;
            },
            cancellationToken);

        var accepted = new List<BackplaneOrderAccepted>
        {
            await SubmitAsync("order-standard", 90m, CustomerTier.Standard, "idem-order-standard").ConfigureAwait(false),
            await SubmitAsync("order-standard", 90m, CustomerTier.Standard, "idem-order-standard").ConfigureAwait(false),
            await SubmitAsync("order-vip", 125m, CustomerTier.Vip, "idem-order-vip").ConfigureAwait(false),
            await SubmitAsync("order-declined", 425m, CustomerTier.Standard, "idem-order-declined").ConfigureAwait(false)
        };

        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

        return new BackplaneDemoSummary(
            accepted,
            audit.ToArray(),
            notifications.ToArray(),
            endpoints.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value,
                StringComparer.Ordinal),
            outbox.Records,
            transport.DeliveryLog);

        async ValueTask<BackplaneOrderAccepted> SubmitAsync(
            string orderId,
            decimal total,
            CustomerTier tier,
            string idempotencyKey)
        {
            var command = Message<SubmitOrder>
                .Create(new SubmitOrder(orderId, total, tier))
                .WithMessageId($"msg-{orderId}")
                .WithCorrelationId($"corr-{orderId}")
                .WithIdempotencyKey(idempotencyKey);

            return await bus.RequestAsync<SubmitOrder, BackplaneOrderAccepted>(command, cancellationToken).ConfigureAwait(false);
        }

        async ValueTask<BackplaneOrderAccepted> AcceptOrderAsync(
            string endpoint,
            Message<SubmitOrder> message,
            MessageContext context,
            CancellationToken token)
        {
            endpoints[message.Payload.OrderId] = endpoint;
            audit.Enqueue($"orders:accepted:{message.Payload.OrderId}:{endpoint}");

            await bus.PublishAsync(
                "orders.submitted",
                new BackplaneOrderSubmitted(message.Payload.OrderId, message.Payload.Total, message.Payload.CustomerTier),
                context.Headers,
                token).ConfigureAwait(false);

            return new BackplaneOrderAccepted(message.Payload.OrderId, endpoint, context.Headers.CorrelationId ?? string.Empty);
        }

        void RecordNotification(CustomerNotification notification)
        {
            notifications.Enqueue(notification);
            audit.Enqueue($"notification:{notification.OrderId}:{notification.Kind}");

            if (notifications.Count == 3)
                completed.TrySetResult();
        }
    }
}

/// <summary>Application-owned transport contract. RabbitMQ, Azure Service Bus, Postgres, or MQTT adapters would implement this boundary.</summary>
public interface IBackplaneTransport : IAsyncDisposable
{
    /// <summary>Subscribes an endpoint handler to a transport address.</summary>
    ValueTask<IAsyncDisposable> SubscribeAsync(
        string address,
        string subscriberName,
        BackplaneTransportHandler handler,
        CancellationToken cancellationToken = default);

    /// <summary>Sends an envelope to an address and returns the matching delivery count.</summary>
    ValueTask<int> SendAsync(
        string address,
        BackplaneEnvelope envelope,
        CancellationToken cancellationToken = default);
}

/// <summary>Application-owned transport handler delegate.</summary>
public delegate ValueTask BackplaneTransportHandler(
    BackplaneEnvelope envelope,
    MessageContext context,
    CancellationToken cancellationToken);

/// <summary>Transport-neutral message envelope for the demo backplane.</summary>
public sealed record BackplaneEnvelope(Type PayloadType, object Payload, MessageHeaders Headers);

/// <summary>A tiny bus facade that composes PatternKit patterns around an application-owned transport.</summary>
public sealed class BackplaneBus
{
    private readonly IBackplaneTransport _transport;
    private readonly BackplaneOutbox _outbox;
    private readonly List<BackplaneRoute> _routes = new();
    private ContentRouter<object, string>? _router;

    /// <summary>Creates a bus facade over the supplied transport and outbox.</summary>
    public BackplaneBus(IBackplaneTransport transport, BackplaneOutbox outbox)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _outbox = outbox ?? throw new ArgumentNullException(nameof(outbox));
    }

    /// <summary>Adds a content-based route for request messages.</summary>
    public BackplaneBus Route<TPayload>(
        ContentRouter<TPayload, string>.RoutePredicate predicate,
        string address)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ValidateAddress(address);

        _routes.Add(new BackplaneRoute(
            typeof(TPayload),
            (payload, context) => payload is TPayload typed && predicate(Message<TPayload>.Create(typed), context),
            address,
            IsDefault: false));
        _router = null;
        return this;
    }

    /// <summary>Adds a default route for request messages of <typeparamref name="TPayload"/>.</summary>
    public BackplaneBus RouteDefault<TPayload>(string address)
    {
        ValidateAddress(address);
        _routes.Add(new BackplaneRoute(
            typeof(TPayload),
            static (_, _) => true,
            address,
            IsDefault: true));
        _router = null;
        return this;
    }

    /// <summary>Registers a request/reply handler at a transport address.</summary>
    public async ValueTask<IAsyncDisposable> HandleAsync<TRequest, TResponse>(
        string address,
        BackplaneRequestHandler<TRequest, TResponse> handler,
        InMemoryIdempotencyStore? idempotencyStore = null,
        CancellationToken cancellationToken = default)
    {
        ValidateAddress(address);
        ArgumentNullException.ThrowIfNull(handler);

        var receiver = idempotencyStore is null
            ? null
            : IdempotentReceiver<TRequest, TResponse>.Create(
                    idempotencyStore,
                    (message, context, token) => handler(message, context, token))
                .OnDuplicate(PatternKit.Messaging.Reliability.DuplicateMessagePolicy.ReplayCompleted)
                .OnMissingKey(MissingIdempotencyKeyPolicy.Process)
                .Build();

        return await _transport.SubscribeAsync(
            address,
            address,
            async (envelope, context, token) =>
            {
                if (envelope.Payload is not TRequest request)
                    return;

                var requestMessage = new Message<TRequest>(request, envelope.Headers);
                var response = receiver is null
                    ? await handler(requestMessage, context, token).ConfigureAwait(false)
                    : (await receiver.HandleAsync(requestMessage, context, token).ConfigureAwait(false)).Result;

                if (response is null || string.IsNullOrWhiteSpace(envelope.Headers.ReplyTo))
                    return;

                var responseHeaders = envelope.Headers
                    .WithCausationId(envelope.Headers.MessageId ?? Guid.NewGuid().ToString("N"))
                    .WithMessageId(Guid.NewGuid().ToString("N"));

                await _transport.SendAsync(
                    envelope.Headers.ReplyTo!,
                    new BackplaneEnvelope(typeof(TResponse), response, responseHeaders),
                    token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Subscribes a named handler to an event topic.</summary>
    public ValueTask<IAsyncDisposable> SubscribeAsync<TEvent>(
        string topic,
        string subscriberName,
        BackplaneEventHandler<TEvent> handler,
        CancellationToken cancellationToken = default)
    {
        ValidateAddress(topic);
        ValidateAddress(subscriberName);
        ArgumentNullException.ThrowIfNull(handler);

        return _transport.SubscribeAsync(
            topic,
            subscriberName,
            (envelope, context, token) =>
            {
                if (envelope.Payload is not TEvent payload)
                    return default;

                return handler(new Message<TEvent>(payload, envelope.Headers), context, token);
            },
            cancellationToken);
    }

    /// <summary>Sends a request to the content-routed endpoint and waits for the typed response.</summary>
    public async ValueTask<TResponse> RequestAsync<TRequest, TResponse>(
        Message<TRequest> message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var replyAddress = $"reply.{Guid.NewGuid():N}";
        var responseSource = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var reply = await SubscribeAsync<TResponse>(
            replyAddress,
            replyAddress,
            (response, _, _) =>
            {
                responseSource.TrySetResult(response.Payload);
                return default;
            },
            cancellationToken).ConfigureAwait(false);

        var headers = EnsureRequestHeaders(message.Headers).WithReplyTo(replyAddress);
        var routedAddress = ResolveAddress(message.Payload, new MessageContext(headers, cancellationToken));
        var deliveries = await _transport.SendAsync(
            routedAddress,
            new BackplaneEnvelope(typeof(TRequest), message.Payload!, headers),
            cancellationToken).ConfigureAwait(false);

        if (deliveries == 0)
            throw new InvalidOperationException($"No backplane handler is subscribed to '{routedAddress}'.");

        return await responseSource.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Publishes an event through an application outbox before handing it to the transport.</summary>
    public async ValueTask PublishAsync<TEvent>(
        string topic,
        TEvent payload,
        MessageHeaders headers,
        CancellationToken cancellationToken = default)
    {
        ValidateAddress(topic);
        ArgumentNullException.ThrowIfNull(headers);

        var envelope = new BackplaneEnvelope(typeof(TEvent), payload!, EnsureEventHeaders(headers));
        var record = await _outbox.EnqueueAsync(topic, envelope, cancellationToken).ConfigureAwait(false);
        var delivered = await _transport.SendAsync(topic, envelope, cancellationToken).ConfigureAwait(false);
        _outbox.MarkDispatched(record.Id, delivered);
    }

    private string ResolveAddress<TPayload>(TPayload payload, MessageContext context)
    {
        if (_routes.Count == 0)
            throw new InvalidOperationException("No backplane routes have been configured.");

        var router = _router ??= BuildRouter();
        return router.Route(Message<object>.Create(payload!), context);
    }

    private ContentRouter<object, string> BuildRouter()
    {
        var builder = ContentRouter<object, string>.Create();
        foreach (var route in _routes.Where(static route => !route.IsDefault))
        {
            builder.When((message, context) =>
                message.Payload is not null
                && route.PayloadType.IsInstanceOfType(message.Payload)
                && route.Predicate(message.Payload, context))
            .Then((_, _) => route.Address);
        }

        foreach (var route in _routes.Where(static route => route.IsDefault))
        {
            builder.When((message, _) =>
                message.Payload is not null && route.PayloadType.IsInstanceOfType(message.Payload))
            .Then((_, _) => route.Address);
        }

        return builder.Build();
    }

    private static MessageHeaders EnsureRequestHeaders(MessageHeaders headers)
    {
        var result = headers;
        if (string.IsNullOrWhiteSpace(result.MessageId))
            result = result.WithMessageId(Guid.NewGuid().ToString("N"));
        if (string.IsNullOrWhiteSpace(result.CorrelationId))
            result = result.WithCorrelationId(result.MessageId!);

        return result;
    }

    private static MessageHeaders EnsureEventHeaders(MessageHeaders headers)
    {
        var result = headers.WithMessageId(Guid.NewGuid().ToString("N"));
        if (string.IsNullOrWhiteSpace(result.CorrelationId))
            result = result.WithCorrelationId(result.MessageId!);

        return result;
    }

    private static void ValidateAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Backplane addresses cannot be null, empty, or whitespace.", nameof(address));
    }
}

/// <summary>Request handler delegate used by the demo bus facade.</summary>
public delegate ValueTask<TResponse> BackplaneRequestHandler<TRequest, TResponse>(
    Message<TRequest> message,
    MessageContext context,
    CancellationToken cancellationToken);

/// <summary>Event handler delegate used by the demo bus facade.</summary>
public delegate ValueTask BackplaneEventHandler<TEvent>(
    Message<TEvent> message,
    MessageContext context,
    CancellationToken cancellationToken);

/// <summary>Thread-safe in-memory transport used by tests to emulate a pluggable broker adapter.</summary>
public sealed class InMemoryBackplaneTransport : IBackplaneTransport
{
    private readonly object _gate = new();
    private readonly List<BackplaneSubscription> _subscriptions = new();
    private readonly List<string> _deliveryLog = new();
    private bool _disposed;

    /// <summary>Gets the completed transport deliveries.</summary>
    public IReadOnlyList<string> DeliveryLog
    {
        get
        {
            lock (_gate)
                return _deliveryLog.ToArray();
        }
    }

    /// <inheritdoc />
    public async ValueTask<IAsyncDisposable> SubscribeAsync(
        string address,
        string subscriberName,
        BackplaneTransportHandler handler,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Validate(address, nameof(address));
        Validate(subscriberName, nameof(subscriberName));
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new BackplaneSubscription(address, subscriberName, handler, Remove);
        await subscription.StartAsync(cancellationToken).ConfigureAwait(false);

        lock (_gate)
            _subscriptions.Add(subscription);

        return subscription;
    }

    /// <inheritdoc />
    public async ValueTask<int> SendAsync(
        string address,
        BackplaneEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        Validate(address, nameof(address));
        ArgumentNullException.ThrowIfNull(envelope);

        BackplaneSubscription[] snapshot;
        lock (_gate)
            snapshot = _subscriptions.ToArray();

        var deliveries = new List<Task>(snapshot.Length);
        var recipientList = RecipientList<BackplaneEnvelope>.Create();
        foreach (var subscription in snapshot)
        {
            recipientList.When(subscription.Name, (message, _) => subscription.Address == address)
                .Then((message, context) =>
                {
                    lock (_gate)
                        _deliveryLog.Add($"{address}->{subscription.Name}:{message.Payload.PayloadType.Name}");

                    deliveries.Add(subscription.DeliverAsync(message.Payload, context, cancellationToken).AsTask());
                });
        }

        var result = recipientList.Build().Dispatch(
            Message<BackplaneEnvelope>.Create(envelope),
            new MessageContext(envelope.Headers, cancellationToken));

        await Task.WhenAll(deliveries).ConfigureAwait(false);
        return result.Count;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        BackplaneSubscription[] subscriptions;
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            subscriptions = _subscriptions.ToArray();
            _subscriptions.Clear();
        }

        foreach (var subscription in subscriptions)
            await subscription.DisposeAsync().ConfigureAwait(false);
    }

    private void Remove(BackplaneSubscription subscription)
    {
        lock (_gate)
            _subscriptions.Remove(subscription);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name);
    }

    private static void Validate(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null, empty, or whitespace.", paramName);
    }
}

internal sealed class BackplaneSubscription : IAsyncDisposable
{
    private readonly BackplaneTransportHandler _handler;
    private readonly Action<BackplaneSubscription> _remove;
    private readonly Mailbox<BackplaneEnvelope> _mailbox;

    internal BackplaneSubscription(
        string address,
        string name,
        BackplaneTransportHandler handler,
        Action<BackplaneSubscription> remove)
    {
        Address = address;
        Name = name;
        _handler = handler;
        _remove = remove;
        _mailbox = Mailbox<BackplaneEnvelope>
            .Create((message, context, cancellationToken) =>
                _handler(message.Payload, context, cancellationToken))
            .Bounded(128, MailboxBackpressurePolicy.Wait)
            .OnError(MailboxErrorPolicy.Stop)
            .Build();
    }

    internal string Address { get; }

    internal string Name { get; }

    internal ValueTask StartAsync(CancellationToken cancellationToken) => _mailbox.StartAsync(cancellationToken);

    internal ValueTask<MailboxPostResult> DeliverAsync(
        BackplaneEnvelope envelope,
        MessageContext context,
        CancellationToken cancellationToken)
        => _mailbox.PostAsync(Message<BackplaneEnvelope>.Create(envelope), context, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        _remove(this);
        await _mailbox.StopAsync().ConfigureAwait(false);
        _mailbox.Dispose();
    }
}

/// <summary>Application outbox used by the demo bus before transport dispatch.</summary>
public sealed class BackplaneOutbox
{
    private readonly object _gate = new();
    private readonly List<BackplaneOutboxRecord> _records = new();

    /// <summary>Gets all recorded outbox messages.</summary>
    public IReadOnlyList<BackplaneOutboxRecord> Records
    {
        get
        {
            lock (_gate)
                return _records.ToArray();
        }
    }

    /// <summary>Enqueues a transport-neutral envelope.</summary>
    public ValueTask<BackplaneOutboxRecord> EnqueueAsync(
        string address,
        BackplaneEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var record = new BackplaneOutboxRecord(
            Guid.NewGuid().ToString("N"),
            address,
            envelope.PayloadType.Name,
            envelope.Headers.CorrelationId ?? string.Empty,
            Dispatched: false,
            Delivered: 0);

        lock (_gate)
            _records.Add(record);

        return new ValueTask<BackplaneOutboxRecord>(record);
    }

    /// <summary>Marks a queued record as dispatched.</summary>
    public void MarkDispatched(string id, int delivered)
    {
        lock (_gate)
        {
            for (var i = 0; i < _records.Count; i++)
            {
                if (_records[i].Id == id)
                {
                    _records[i] = _records[i] with { Dispatched = true, Delivered = delivered };
                    return;
                }
            }
        }
    }
}

internal sealed record BackplaneRoute(
    Type PayloadType,
    Func<object, MessageContext, bool> Predicate,
    string Address,
    bool IsDefault);

/// <summary>Order command sent through the demo backplane.</summary>
public sealed record SubmitOrder(string OrderId, decimal Total, CustomerTier CustomerTier);

/// <summary>Request/reply response returned by the order service.</summary>
public sealed record BackplaneOrderAccepted(string OrderId, string Endpoint, string CorrelationId);

/// <summary>Event emitted after an order command is accepted.</summary>
public sealed record BackplaneOrderSubmitted(string OrderId, decimal Total, CustomerTier CustomerTier);

/// <summary>Event emitted after payment capture.</summary>
public sealed record PaymentCaptured(string OrderId, decimal Amount);

/// <summary>Event emitted after payment decline.</summary>
public sealed record PaymentDeclined(string OrderId, string Reason);

/// <summary>Event emitted after shipment scheduling.</summary>
public sealed record ShipmentScheduled(string OrderId, string TrackingNumber);

/// <summary>Notification side effect produced by the demo application.</summary>
public sealed record CustomerNotification(string OrderId, string Kind, string CorrelationId);

/// <summary>Outbox record returned by the demo for assertions and documentation.</summary>
public sealed record BackplaneOutboxRecord(
    string Id,
    string Address,
    string PayloadType,
    string CorrelationId,
    bool Dispatched,
    int Delivered);

/// <summary>Customer tier used by the content router.</summary>
public enum CustomerTier { Standard, Vip }

/// <summary>Summary returned by the backplane facade demo.</summary>
public sealed record BackplaneDemoSummary(
    IReadOnlyList<BackplaneOrderAccepted> Accepted,
    IReadOnlyList<string> Audit,
    IReadOnlyList<CustomerNotification> Notifications,
    IReadOnlyDictionary<string, string> Endpoints,
    IReadOnlyList<BackplaneOutboxRecord> Outbox,
    IReadOnlyList<string> DeliveryLog);
