# Claim Check

`ClaimCheck<TPayload>` stores a large or sensitive payload outside the message flow and replaces it with a `ClaimCheckReference`. Later, the same claim check restores the original payload and headers from an `IClaimCheckStore<TPayload>`.

Use it when routing, saga, or mailbox steps should carry a lightweight reference instead of a large document body.

## Runtime Path

```csharp
using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

var store = new InMemoryClaimCheckStore<LargeOrderDocument>();
var claimCheck = ClaimCheck<LargeOrderDocument>
    .Create("large-document-claim-check")
    .InStore("document-archive")
    .UseStore(store)
    .UseClaimIds(static (message, _) => $"order-doc:{message.Headers.MessageId}")
    .Build();

var claim = await claimCheck.StoreAsync(documentMessage);
var restored = await claimCheck.RestoreAsync(claim);
```

`StoreAsync` preserves message metadata on the claim reference and records the original headers with the stored payload. `RestoreAsync` returns `ClaimCheckRestoreResult<TPayload>` so missing references can be handled without exceptions.

## Source-Generated Path

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Transformation;

[GenerateClaimCheck(typeof(LargeOrderDocument), StoreName = "document-archive", ClaimIdPrefix = "order-doc")]
public static partial class LargeDocumentClaims
{
    [ClaimCheckStoreFactory]
    private static IClaimCheckStore<LargeOrderDocument> CreateStore()
        => new InMemoryClaimCheckStore<LargeOrderDocument>();
}
```

The generated factory returns `ClaimCheck<LargeOrderDocument>` and wires the declared store factory, store name, and deterministic claim-id prefix.

## DI Integration

```csharp
services.AddLargeDocumentClaimCheckExample();

var workflow = provider.GetRequiredService<LargeDocumentWorkflow>();
var summary = workflow.Process(documentMessage);
```

Production example:

- `src/PatternKit.Examples/Messaging/LargeDocumentClaimCheckExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/LargeDocumentClaimCheckExampleTests.cs`
