# Content Enricher

Content Enricher augments a message payload with computed, normalized, or externally sourced data before a handler mutates state. Use it at message intake boundaries when downstream consumers require a complete payload but upstream systems provide partial data.

## Fluent Path

`AsyncContentEnricher<TPayload>.Create()` builds an ordered async enrichment pipeline. Each step receives the current payload, `MessageContext`, and `CancellationToken`, then returns the enriched payload copy. Per-step error policies let production pipelines throw, skip, or apply a default payload transform.

## Source-Generated Path

Use `[GenerateContentEnricher]` on a partial host and mark static `ValueTask<TPayload>` enrichment methods with `[ContentEnrichmentStep]`. The generator emits an `AsyncContentEnricher<TPayload>` factory with ordered steps and optional `UseDefault` fallback factories.

## Production Notes

- Keep enrichment steps deterministic and idempotent where possible.
- Use `UseDefault` only for safe business defaults.
- Register the generated factory with `IServiceCollection` when importing the pipeline into hosted services or ASP.NET Core handlers.
