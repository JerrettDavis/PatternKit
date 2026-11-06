# Correlation Identifier Generator

`[GenerateCorrelationIdentifier]` emits a typed factory that returns a configured `CorrelationIdentifier<T>.Builder`.

```csharp
[GenerateCorrelationIdentifier(
    typeof(Order),
    FactoryName = "Create",
    HeaderName = "X-Correlation")]
public static partial class OrderCorrelation;

var correlation = OrderCorrelation.Create()
    .Select((message, _) => "order:" + message.Payload.Id)
    .Build();
```

The generated path removes repeated boilerplate for the payload type, factory name, header name, and preserve-existing policy while still allowing teams to add selectors or generators fluently at composition time.
