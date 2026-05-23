using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class OutboxStoreExtensionsTests
{
    [Scenario("EnqueueObjectAsync WithPayloadAndHeaders StoresCorrectly")]
    [Fact]
    public async Task EnqueueObjectAsync_WithPayloadAndHeaders_StoresCorrectly()
    {
        var store = new InMemoryOutboxStore<object>();
        var payload = new { Type = "OrderCreated", OrderId = 42 };
        var headers = new Dictionary<string, string>
        {
            ["content-type"] = "application/json",
            ["source"] = "order-service",
        };

        var record = await store.EnqueueObjectAsync(payload, headers);

        ScenarioExpect.NotNull(record);
        ScenarioExpect.False(record.Dispatched);
        ScenarioExpect.Equal(1, store.Records.Count);
        ScenarioExpect.Equal(payload, record.Message.Payload);
        ScenarioExpect.Equal("application/json", record.Message.Headers.GetString("content-type"));
        ScenarioExpect.Equal("order-service", record.Message.Headers.GetString("source"));
    }

    [Scenario("EnqueueObjectAsync WithNullHeaders StoresEmptyHeadersMessage")]
    [Fact]
    public async Task EnqueueObjectAsync_WithNullHeaders_StoresEmptyHeadersMessage()
    {
        var store = new InMemoryOutboxStore<object>();
        var payload = "plain-string-payload";

        var record = await store.EnqueueObjectAsync(payload, headers: null);

        ScenarioExpect.NotNull(record);
        ScenarioExpect.Equal(payload, record.Message.Payload);
        ScenarioExpect.Equal(0, record.Message.Headers.Count);
    }

    [Scenario("EnqueueObjectAsync RespectsCancellation")]
    [Fact]
    public async Task EnqueueObjectAsync_RespectsCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var store = new InMemoryOutboxStore<object>();

        await ScenarioExpect.ThrowsAsync<OperationCanceledException>(
            () => store.EnqueueObjectAsync("payload", null, cts.Token).AsTask());
    }
}
