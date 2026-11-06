# Invalid Message Channel

Invalid Message Channel routes messages that fail validation into a dedicated `MessageChannel<InvalidMessage<TPayload>>` with the original message, reason, headers, and routing timestamp preserved.

## Fluent

```csharp
var invalids = MessageChannel<InvalidMessage<OrderImportCommand>>
    .Create("invalid-order-imports")
    .Build();

var channel = InvalidMessageChannel<OrderImportCommand>.Create("order-import-invalids")
    .To(invalids)
    .When(message => string.IsNullOrWhiteSpace(message.Payload.Sku))
    .Because(_ => "SKU is required.")
    .Build();

var result = channel.Route(message);
```

Messages that do not match the invalid predicate are left unrouted. Invalid messages are wrapped with the reason and original headers before being sent to the invalid channel.

## Source Generator

Use `[GenerateInvalidMessageChannel]` when the invalid-channel factory should be owned by generated code and completed by application validation rules:

```csharp
[GenerateInvalidMessageChannel(typeof(OrderImportCommand), ChannelName = "order-import-invalids")]
public static partial class GeneratedOrderInvalidMessageChannel;
```

The generated `Create(MessageChannel<InvalidMessage<TPayload>> invalidChannel)` method returns an `InvalidMessageChannel<TPayload>.Builder` already connected to the target invalid channel.
