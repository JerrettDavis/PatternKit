# Polling Consumer

Polling Consumer explicitly asks a message source for the next available message.

```csharp
var consumer = PollingConsumer<ReplenishmentRequest>
    .Create("warehouse-replenishment-poller")
    .From(context => channel.TryReceive().Message)
    .Build();

var result = consumer.Poll();
```

Use it when application code controls the polling cadence, backoff, and transaction boundary. The fluent runtime path accepts any synchronous poll source and returns typed empty or received results.

The source-generated path uses `[GeneratePollingConsumer]` and `[PollingConsumerSource]`. Import the warehouse example through `AddWarehousePollingConsumerDemo()` or `AddPatternKitExamples()`.
