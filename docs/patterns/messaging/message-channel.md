# Message Channel

Message Channel provides a typed queue between message producers and consumers.

```csharp
var channel = MessageChannel<InventoryAdjustment>
    .Create("inventory-adjustments")
    .WithCapacity(32)
    .Build();

channel.Send(Message<InventoryAdjustment>.Create(new("SKU-100", 3, "cycle-count")));
var next = channel.TryReceive();
```

Use it when an application needs an explicit messaging boundary between code that produces work and code that processes work. The fluent runtime path supports unbounded channels, bounded channels, reject or drop-oldest backpressure, snapshots, and typed receive results.

The source-generated path uses `[GenerateMessageChannel]`. Import the inventory example through `AddInventoryMessageChannelDemo()` or `AddPatternKitExamples()`.
