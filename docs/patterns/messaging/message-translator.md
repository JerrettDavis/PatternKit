# Message Translator

`MessageTranslator<TInput,TOutput>` translates one message contract into another while applying an explicit header policy. Use it at integration boundaries where partner, vendor, or transport-specific events need to become application-owned contracts before they enter routers, sagas, mailboxes, or reliability pipelines.

## Runtime Path

```csharp
using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

var translator = MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted>
    .Create("partner-order-translator")
    .TranslateWith(static (message, context) => new CommerceOrderAccepted(
        $"commerce-{message.Payload.ExternalOrderId}",
        message.Payload.Amount,
        message.Payload.PartnerId))
    .DropHeader("raw-signature")
    .SetHeader(MessageHeaderNames.ContentType, "application/vnd.myapp.order+json")
    .Build();

var result = translator.Translate(partnerMessage);
```

The translator returns `MessageTranslationResult<TOutput>` instead of leaking transformation exceptions into routing code. Invalid translator output or header policy failures produce a failed result with the captured exception.

## Header Policies

Headers are preserved by default so correlation, causation, message identifiers, and tenant metadata survive the contract change. Fluent policies can remove sensitive transport headers, keep only an allow-list, or set normalized headers:

```csharp
var translator = MessageTranslator<RawEvent, NormalizedEvent>
    .Create("normalizer")
    .TranslateWith(static (message, _) => new NormalizedEvent(message.Payload.Id))
    .KeepHeaders(MessageHeaderNames.CorrelationId, "tenant-id")
    .SetHeader(MessageHeaderNames.ContentType, "application/vnd.myapp.normalized+json")
    .Build();
```

## Source-Generated Path

Use `[GenerateMessageTranslator]` when a translator contract should be compile-time visible and reusable from dependency injection setup:

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;

[GenerateMessageTranslator(typeof(PartnerOrderAccepted), typeof(CommerceOrderAccepted), TranslatorName = "partner-order-translator")]
[MessageTranslatorDropHeader("raw-signature")]
[MessageTranslatorHeader(MessageHeaderNames.ContentType, "application/vnd.myapp.order+json")]
public static partial class PartnerOrderTranslator
{
    [MessageTranslatorHandler]
    private static CommerceOrderAccepted Translate(Message<PartnerOrderAccepted> message, MessageContext context)
        => new($"commerce-{message.Payload.ExternalOrderId}", message.Payload.Amount, message.Payload.PartnerId);
}
```

The generated factory returns `MessageTranslator<PartnerOrderAccepted, CommerceOrderAccepted>` and emits the same runtime builder calls.

## DI Integration

Examples can be imported through `Microsoft.Extensions.DependencyInjection`:

```csharp
services.AddPartnerEventTranslatorExample();

var service = provider.GetRequiredService<PartnerOrderImportService>();
var summary = service.Import(partnerMessage);
```

Production example:

- `src/PatternKit.Examples/Messaging/PartnerEventTranslatorExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/PartnerEventTranslatorExampleTests.cs`
