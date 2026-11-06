# Resequencer

Resequencer buffers out-of-order messages and releases only the contiguous sequence that is ready for processing.

```csharp
var resequencer = Resequencer<ShipmentEvent>
    .Create("shipment-events")
    .SelectSequence((message, context) => message.Payload.Sequence)
    .Build();

var result = resequencer.Accept(Message<ShipmentEvent>.Create(new(2, "ship-1", "Allocated")));
```

Use it when a transport, partner feed, or partitioned stream can deliver events out of order but downstream handlers require ordered processing. The fluent runtime path supports named resequencers, custom starting offsets, duplicate detection, and contiguous release batches.

The source-generated path uses `[GenerateResequencer]` and `[ResequencerSequence]`. Import the shipment example through `AddShipmentResequencerDemo()` or `AddPatternKitExamples()`.
