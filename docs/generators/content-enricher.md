# Content Enricher Generator

`[GenerateContentEnricher]` creates an `AsyncContentEnricher<TPayload>` factory from attributed async enrichment methods.

```csharp
[GenerateContentEnricher(typeof(CustomerProfileUpdate), EnricherName = "customer-profile-enrichment")]
public static partial class GeneratedCustomerProfileContentEnricher
{
    [ContentEnrichmentStep("normalize-email", Order = 10)]
    private static ValueTask<CustomerProfileUpdate> NormalizeEmail(
        CustomerProfileUpdate profile,
        MessageContext context,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(profile with { Email = profile.Email?.Trim().ToLowerInvariant() });
}
```

Step methods must be static, return `ValueTask<TPayload>`, and accept `TPayload`, `MessageContext`, and `CancellationToken`. `ContentEnrichmentErrorPolicy.UseDefault` requires `DefaultFactoryName` to point at a static method that accepts and returns the payload type.

The generated factory is suitable for DI registration:

```csharp
services.AddSingleton(_ => GeneratedCustomerProfileContentEnricher.Create());
```
