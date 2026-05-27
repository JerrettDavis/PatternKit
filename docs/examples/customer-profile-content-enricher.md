# Customer Profile Content Enricher

The customer profile content-enricher example shows an importable enrichment pipeline for profile updates.

- Normalizes inbound email addresses.
- Applies a safe default tier when upstream data is incomplete.
- Derives marketing opt-in state from the enriched tier.
- Registers the generated `AsyncContentEnricher<CustomerProfileUpdate>` with `IServiceCollection`.

Use `AddCustomerProfileContentEnricherDemo()` for the focused example or `AddPatternKitExamples()` for the aggregate catalog registration.
