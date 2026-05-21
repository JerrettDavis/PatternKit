# Message Translator Generator

`[GenerateMessageTranslator]` creates a factory for `MessageTranslator<TInput,TOutput>`.

Use it when partner or transport event normalization is part of the application contract and should be validated by the compiler.

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateMessageTranslator(typeof(PartnerOrderAccepted), typeof(CommerceOrderAccepted), FactoryName = "Build")]
[MessageTranslatorDropHeader("raw-signature")]
[MessageTranslatorHeader(MessageHeaderNames.ContentType, "application/vnd.myapp.order+json")]
public static partial class PartnerOrderTranslator
{
    [MessageTranslatorHandler]
    private static CommerceOrderAccepted Translate(Message<PartnerOrderAccepted> message, MessageContext context)
        => new($"commerce-{message.Payload.ExternalOrderId}", message.Payload.Amount, message.Payload.PartnerId);
}
```

Generated output:

```csharp
var translator = PartnerOrderTranslator.Build();
var result = translator.Translate(partnerMessage);
```

## Diagnostics

- `PKMT001`: the translator host must be partial.
- `PKMT002`: exactly one `[MessageTranslatorHandler]` method is required.
- `PKMT003`: the handler must be static, return the output payload type, and accept `Message<TInput>` plus `MessageContext`.

## Example

- `src/PatternKit.Examples/Messaging/PartnerEventTranslatorExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/PartnerEventTranslatorExampleTests.cs`
