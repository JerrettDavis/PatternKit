#if NET8_0
using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MQTTnet;
using MQTTnet.Protocol;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Testcontainers.RabbitMq;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class BackplaneTestcontainerE2ETests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task RabbitMqBackplane_RunsRequestReplyAndPublishSubscribeThroughContainer()
    {
#pragma warning disable CS0618 // Testcontainers keeps the parameterless builder for fluent module setup.
        await using var container = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.13-alpine")
            .Build();
#pragma warning restore CS0618

        await container.StartAsync();
        await using var transport = await RabbitMqBackplaneTransport.CreateAsync(container.GetConnectionString());

        await AssertBackplaneAsync(transport);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task MqttBackplane_RunsRequestReplyAndPublishSubscribeThroughContainer()
    {
        const int mqttPort = 1883;
#pragma warning disable CS0618 // Testcontainers keeps the parameterless builder for generic container setup.
        await using var container = new ContainerBuilder()
            .WithImage("eclipse-mosquitto:2.0")
            .WithPortBinding(mqttPort, assignRandomHostPort: true)
            .WithResourceMapping(
                Encoding.UTF8.GetBytes("listener 1883 0.0.0.0\nallow_anonymous true\n"),
                "/mosquitto/config/mosquitto.conf")
            .Build();
#pragma warning restore CS0618

        await container.StartAsync();
        await using var transport = await MqttBackplaneTransport.CreateAsync(
            container.Hostname,
            container.GetMappedPublicPort(mqttPort));

        await AssertBackplaneAsync(transport);
    }

    private static async Task AssertBackplaneAsync(IBackplaneTransport transport)
    {
        var outbox = new BackplaneOutbox();
        var idempotency = new InMemoryIdempotencyStore();
        var observed = new TaskCompletionSource<CustomerNotification>(TaskCreationOptions.RunContinuationsAsynchronously);
        BackplaneClient? client = null;

        await using var host = await BackplaneHost.Create()
            .UseTransport(() => transport)
            .UseOutbox(outbox)
            .UseIdempotencyStore(idempotency)
            .MapDefaultCommand<SubmitOrder, BackplaneOrderAccepted>("orders.standard")
            .ReceiveEndpoint("orders.standard", endpoint =>
                endpoint.HandleCommand<SubmitOrder, BackplaneOrderAccepted>(async (message, context, cancellationToken) =>
                {
                    await client!.PublishAsync(
                        "orders.submitted",
                        new BackplaneOrderSubmitted(message.Payload.OrderId, message.Payload.Total, message.Payload.CustomerTier),
                        context.Headers,
                        cancellationToken);

                    return new BackplaneOrderAccepted(
                        message.Payload.OrderId,
                        "orders.standard",
                        context.Headers.CorrelationId ?? string.Empty);
                }))
            .ReceiveEndpoint("notification-service", endpoint =>
                endpoint.Subscribe<BackplaneOrderSubmitted>("orders.submitted", (message, context, _) =>
                {
                    observed.TrySetResult(new CustomerNotification(
                        message.Payload.OrderId,
                        "order-submitted",
                        context.Headers.CorrelationId ?? string.Empty));
                    return default;
                }))
            .BuildAsync();

        client = host.Client;

        var accepted = await host.Client.RequestAsync<SubmitOrder, BackplaneOrderAccepted>(
            Message<SubmitOrder>
                .Create(new SubmitOrder("container-order", 42m, CustomerTier.Standard))
                .WithMessageId("msg-container-order")
                .WithCorrelationId("corr-container-order")
                .WithIdempotencyKey("idem-container-order"));

        var notification = await observed.Task.WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(new BackplaneOrderAccepted("container-order", "orders.standard", "corr-container-order"), accepted);
        Assert.Equal(new CustomerNotification("container-order", "order-submitted", "corr-container-order"), notification);
        Assert.Single(outbox.Records);
        Assert.Equal("orders.submitted", outbox.Records[0].Address);
        Assert.True(outbox.Records[0].Dispatched);
        Assert.Equal(1, outbox.Records[0].Delivered);
        Assert.True(idempotency.TryGet("idem-container-order", out var claim));
        Assert.Equal(IdempotencyEntryStatus.Completed, claim!.Status);
    }
}

internal sealed class RabbitMqBackplaneTransport : IBackplaneTransport
{
    private const string ExchangePrefix = "patternkit.backplane.";
    private readonly IConnection _connection;
    private readonly IChannel _publisher;
    private readonly ConcurrentDictionary<string, byte> _subscriptions = new(StringComparer.Ordinal);
    private bool _disposed;

    private RabbitMqBackplaneTransport(IConnection connection, IChannel publisher)
    {
        _connection = connection;
        _publisher = publisher;
    }

    internal static async ValueTask<RabbitMqBackplaneTransport> CreateAsync(string connectionString)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(connectionString),
            ClientProvidedName = "patternkit-testcontainer-e2e"
        };
        var connection = await factory.CreateConnectionAsync();
        var publisher = await connection.CreateChannelAsync();
        return new RabbitMqBackplaneTransport(connection, publisher);
    }

    public async ValueTask<IAsyncDisposable> SubscribeAsync(
        string address,
        string subscriberName,
        BackplaneTransportHandler handler,
        CancellationToken cancellationToken = default)
    {
        var exchange = ExchangeName(address);
        var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: false, autoDelete: true, cancellationToken: cancellationToken);

        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue.QueueName, exchange, routingKey: string.Empty, cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            var envelope = BackplaneEnvelopeCodec.Decode(args.Body.ToArray());
            await handler(envelope, new MessageContext(envelope.Headers, cancellationToken), cancellationToken);
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue.QueueName,
            autoAck: false,
            consumerTag: string.Empty,
            noLocal: false,
            exclusive: false,
            arguments: null,
            consumer,
            cancellationToken);

        var key = $"{address}\0{subscriberName}\0{consumerTag}";
        _subscriptions[key] = 0;
        return new RabbitMqBackplaneSubscription(channel, consumerTag, _subscriptions, key);
    }

    public async ValueTask<int> SendAsync(
        string address,
        BackplaneEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var exchange = ExchangeName(address);
        await _publisher.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: false, autoDelete: true, cancellationToken: cancellationToken);
        await _publisher.BasicPublishAsync(
            exchange,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                ContentType = "application/json",
                MessageId = envelope.Headers.MessageId,
                CorrelationId = envelope.Headers.CorrelationId,
                ReplyTo = envelope.Headers.ReplyTo
            },
            body: BackplaneEnvelopeCodec.Encode(envelope),
            cancellationToken);

        return CountSubscriptions(address);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await _publisher.CloseAsync();
        await _connection.CloseAsync();
    }

    private int CountSubscriptions(string address)
        => _subscriptions.Keys.Count(key => key.StartsWith(address + "\0", StringComparison.Ordinal));

    private static string ExchangeName(string address) => ExchangePrefix + address.Replace('.', '-');
}

internal sealed class RabbitMqBackplaneSubscription : IAsyncDisposable
{
    private readonly IChannel _channel;
    private readonly string _consumerTag;
    private readonly ConcurrentDictionary<string, byte> _subscriptions;
    private readonly string _key;

    internal RabbitMqBackplaneSubscription(
        IChannel channel,
        string consumerTag,
        ConcurrentDictionary<string, byte> subscriptions,
        string key)
    {
        _channel = channel;
        _consumerTag = consumerTag;
        _subscriptions = subscriptions;
        _key = key;
    }

    public async ValueTask DisposeAsync()
    {
        _subscriptions.TryRemove(_key, out _);
        await _channel.BasicCancelAsync(_consumerTag);
        await _channel.CloseAsync();
    }
}

internal sealed class MqttBackplaneTransport : IBackplaneTransport
{
    private readonly MqttClientFactory _factory = new();
    private readonly string _host;
    private readonly int _port;
    private readonly IMqttClient _publisher;
    private readonly ConcurrentDictionary<string, byte> _subscriptions = new(StringComparer.Ordinal);
    private bool _disposed;

    private MqttBackplaneTransport(string host, int port, IMqttClient publisher)
    {
        _host = host;
        _port = port;
        _publisher = publisher;
    }

    internal static async ValueTask<MqttBackplaneTransport> CreateAsync(string host, int port)
    {
        var factory = new MqttClientFactory();
        var options = new MqttClientOptionsBuilder()
            .WithClientId("patternkit-publisher-" + Guid.NewGuid().ToString("N"))
            .WithTcpServer(host, port)
            .WithCleanSession()
            .Build();
        var publisher = factory.CreateMqttClient();
        await ConnectWithRetryAsync(publisher, options, CancellationToken.None);
        return new MqttBackplaneTransport(host, port, publisher);
    }

    public async ValueTask<IAsyncDisposable> SubscribeAsync(
        string address,
        string subscriberName,
        BackplaneTransportHandler handler,
        CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateMqttClient();
        var options = new MqttClientOptionsBuilder()
            .WithClientId($"patternkit-{subscriberName}-{Guid.NewGuid():N}")
            .WithTcpServer(_host, _port)
            .WithCleanSession()
            .Build();

        client.ApplicationMessageReceivedAsync += async args =>
        {
            var envelope = BackplaneEnvelopeCodec.Decode(args.ApplicationMessage.Payload.ToArray());
            await handler(envelope, new MessageContext(envelope.Headers, cancellationToken), cancellationToken);
        };

        await ConnectWithRetryAsync(client, options, cancellationToken);
        await client.SubscribeAsync(
            new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(TopicName(address), MqttQualityOfServiceLevel.AtLeastOnce)
                .Build(),
            cancellationToken);

        var key = $"{address}\0{subscriberName}\0{Guid.NewGuid():N}";
        _subscriptions[key] = 0;
        return new MqttBackplaneSubscription(client, _subscriptions, key);
    }

    public async ValueTask<int> SendAsync(
        string address,
        BackplaneEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(TopicName(address))
            .WithPayload(BackplaneEnvelopeCodec.Encode(envelope).ToArray())
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _publisher.PublishAsync(message, cancellationToken);
        return _subscriptions.Keys.Count(key => key.StartsWith(address + "\0", StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (_publisher.IsConnected)
            await _publisher.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);

        _publisher.Dispose();
    }

    private static string TopicName(string address) => "patternkit/backplane/" + address.Replace('.', '/');

    private static async Task ConnectWithRetryAsync(
        IMqttClient client,
        MqttClientOptions options,
        CancellationToken cancellationToken)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await client.ConnectAsync(options, cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < 29)
            {
                last = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
        }

        throw new InvalidOperationException("MQTT broker did not become available.", last);
    }
}

internal sealed class MqttBackplaneSubscription : IAsyncDisposable
{
    private readonly IMqttClient _client;
    private readonly ConcurrentDictionary<string, byte> _subscriptions;
    private readonly string _key;

    internal MqttBackplaneSubscription(
        IMqttClient client,
        ConcurrentDictionary<string, byte> subscriptions,
        string key)
    {
        _client = client;
        _subscriptions = subscriptions;
        _key = key;
    }

    public async ValueTask DisposeAsync()
    {
        _subscriptions.TryRemove(_key, out _);
        if (_client.IsConnected)
            await _client.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);

        _client.Dispose();
    }
}

internal static class BackplaneEnvelopeCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    internal static ReadOnlyMemory<byte> Encode(BackplaneEnvelope envelope)
    {
        var dto = new BackplaneEnvelopeDto(
            envelope.PayloadType.AssemblyQualifiedName!,
            JsonSerializer.Serialize(envelope.Payload, envelope.PayloadType, Options),
            envelope.Headers.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase));

        return JsonSerializer.SerializeToUtf8Bytes(dto, Options);
    }

    internal static BackplaneEnvelope Decode(byte[] payload)
    {
        var dto = JsonSerializer.Deserialize<BackplaneEnvelopeDto>(payload, Options)
            ?? throw new InvalidOperationException("Backplane envelope payload was empty.");
        var payloadType = Type.GetType(dto.PayloadType, throwOnError: true)!;
        var message = JsonSerializer.Deserialize(dto.PayloadJson, payloadType, Options)
            ?? throw new InvalidOperationException($"Backplane payload '{payloadType.FullName}' was empty.");

        return new BackplaneEnvelope(
            payloadType,
            message,
            new MessageHeaders(dto.Headers.Select(static pair => new KeyValuePair<string, object?>(pair.Key, pair.Value))));
    }

    private sealed record BackplaneEnvelopeDto(
        string PayloadType,
        string PayloadJson,
        IReadOnlyDictionary<string, string> Headers);
}
#endif
