# Message History Generator

`[GenerateMessageHistory]` emits a typed message history factory.

```csharp
[GenerateMessageHistory(
    typeof(HistoryOrder),
    "checkout-api",
    FactoryName = "Create",
    Action = "received")]
public static partial class GeneratedOrderReceivedHistory;
```

The generated factory returns a `MessageHistory<TPayload>.Builder`, so application code can still add runtime-only details or a deterministic test clock:

```csharp
var history = GeneratedOrderReceivedHistory.Create()
    .Details(message => message.Payload.Channel)
    .Build();
```

Diagnostics:

| ID | Meaning |
| --- | --- |
| `PKMH001` | The host type must be `partial`. |
| `PKMH002` | Factory, component, action, and header names must be non-empty. |
