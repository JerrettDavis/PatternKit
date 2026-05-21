# Claim Check Generator

`[GenerateClaimCheck]` creates a factory for `ClaimCheck<TPayload>`.

```csharp
using PatternKit.Generators.Messaging;
using PatternKit.Messaging.Transformation;

[GenerateClaimCheck(typeof(LargeOrderDocument), FactoryName = "Build", StoreName = "document-archive")]
public static partial class LargeDocumentClaims
{
    [ClaimCheckStoreFactory]
    private static IClaimCheckStore<LargeOrderDocument> CreateStore()
        => new InMemoryClaimCheckStore<LargeOrderDocument>();
}
```

Generated output:

```csharp
var claimCheck = LargeDocumentClaims.Build();
var claim = claimCheck.Store(documentMessage);
var restored = claimCheck.Restore(claim);
```

## Diagnostics

- `PKCC001`: the claim-check host must be partial.
- `PKCC002`: exactly one `[ClaimCheckStoreFactory]` method is required.
- `PKCC003`: the store factory must be static, parameterless, and return `IClaimCheckStore<TPayload>`.

## Example

- `src/PatternKit.Examples/Messaging/LargeDocumentClaimCheckExample.cs`
- `test/PatternKit.Examples.Tests/Messaging/LargeDocumentClaimCheckExampleTests.cs`
