# Generated Message Translator

This example normalizes partner order events into an application-owned commerce event. It shows the fluent runtime path, the source-generated path, and the `IServiceCollection` integration an importing app can use.

Source:

- `src/PatternKit.Examples/Messaging/PartnerEventTranslatorExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/PartnerEventTranslatorExampleTests.cs`

## Runtime Path

```csharp
var translator = PartnerOrderTranslatorPolicies.CreateFluentTranslator();
var result = translator.Translate(PartnerEventTranslatorExample.CreatePartnerMessage(
    "partner-a",
    "EXT-100",
    125m));
```

The translator preserves correlation headers, drops the raw partner signature header, and writes the normalized content type.

## Generated Path

```csharp
[GenerateMessageTranslator(typeof(PartnerOrderAccepted), typeof(CommerceOrderAccepted), TranslatorName = "partner-order-translator")]
[MessageTranslatorDropHeader("raw-signature")]
[MessageTranslatorHeader(MessageHeaderNames.ContentType, "application/vnd.patternkit.commerce-order-accepted+json")]
public static partial class GeneratedPartnerOrderTranslator;
```

The generated factory returns the same runtime translator type:

```csharp
var translator = GeneratedPartnerOrderTranslator.Create();
var result = translator.Translate(partnerMessage);
```

## DI Integration

```csharp
services.AddPartnerEventTranslatorExample();

var importer = provider.GetRequiredService<PartnerOrderImportService>();
var summary = importer.Import(partnerMessage);
```

`AddPatternKitExamples()` also registers this example through `GeneratedMessageTranslatorExample` so catalog consumers can resolve it with the rest of the production-ready examples.
