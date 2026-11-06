# Invalid Message Channel Generator

`[GenerateInvalidMessageChannel]` creates a strongly typed builder factory for `InvalidMessageChannel<TPayload>`.

```csharp
[GenerateInvalidMessageChannel(
    typeof(OrderImportCommand),
    FactoryName = "Create",
    ChannelName = "order-import-invalids")]
public static partial class GeneratedOrderInvalidMessageChannel;
```

Generated shape:

```csharp
public static InvalidMessageChannel<OrderImportCommand>.Builder Create(
    MessageChannel<InvalidMessage<OrderImportCommand>> invalidChannel);
```

The host type must be partial. Applications finish the builder with validation predicates and reason functions from their own domain.
