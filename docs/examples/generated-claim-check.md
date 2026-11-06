# Generated Claim Check

This example stores a large order document outside the message flow, routes a `ClaimCheckReference`, and restores the original document when a downstream component needs the full payload.

Source:

- `src/PatternKit.Examples/Messaging/LargeDocumentClaimCheckExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/LargeDocumentClaimCheckExampleTests.cs`

## Runtime Path

```csharp
var claimCheck = LargeDocumentClaimCheckPolicies.CreateFluentClaimCheck(
    new InMemoryClaimCheckStore<LargeOrderDocument>());

var claim = claimCheck.Store(documentMessage);
var restored = claimCheck.Restore(claim);
```

## Generated Path

```csharp
[GenerateClaimCheck(typeof(LargeOrderDocument), ClaimCheckName = "large-document-claim-check", StoreName = "document-archive", ClaimIdPrefix = "order-doc")]
public static partial class GeneratedLargeDocumentClaimCheck
{
    [ClaimCheckStoreFactory]
    private static IClaimCheckStore<LargeOrderDocument> CreateStore()
        => new InMemoryClaimCheckStore<LargeOrderDocument>();
}
```

## DI Integration

```csharp
services.AddLargeDocumentClaimCheckExample();

var workflow = provider.GetRequiredService<LargeDocumentWorkflow>();
var summary = workflow.Process(documentMessage);
```

`AddPatternKitExamples()` also registers this example through `GeneratedClaimCheckExample`.
