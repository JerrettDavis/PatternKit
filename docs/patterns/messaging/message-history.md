# Message History

Message History records the components and operations that handled a message while preserving the original payload and metadata. Use it when production support, audit trails, or cross-system troubleshooting need to answer where a message has been.

PatternKit stores immutable `MessageHistoryEntry` values in a message header. Each recorder appends one hop:

```csharp
var received = MessageHistory<HistoryOrder>
    .Create("checkout-api")
    .Action("received")
    .Details(message => message.Payload.Channel)
    .Build();

var result = received.Record(message);
var history = MessageHistory<HistoryOrder>.Read(result);
```

Use Message History for observability that must travel with the message envelope. Use Wire Tap when the observation should be copied out-of-band, and use Audit Log when the record is a domain or compliance event rather than transport metadata.
