# Visitor — Enterprise Guide

This guide provides practical guidance for adopting Visitor in larger codebases: architecture, performance, testing, and operations.

## Architecture & Organization
- Centralize composition: create dedicated static factories (e.g., `ReceiptRendering.CreateRenderer()`).
- Favor composition over mega-visitors: split by module (Billing, Catalog, POS) and compose at the edge.
- Keep handlers small; delegate complex logic to services.

## Error Handling & Defaults
- Always add a `Default(...)` for resilience and observability. Log unknown types and continue.
- Result visitors: prefer returning error objects vs throwing in handlers; reserve throws for exceptional conditions.
- Action visitors: aim for idempotency; guard external side effects.

## Performance
- Registration order matters; put frequent types first.
- Avoid per-call allocations in handlers. Cache dependencies; reuse buffers.
- For very large hierarchies, consider pre-sharding by “family” or using multiple visitors.

## Concurrency & Thread Safety
- Built visitors are immutable and thread-safe. Register once, share many.
- Ensure downstream services are thread-safe or scoped appropriately (e.g., per-request).

## Testing Strategy
- BDD tests per visitor: cover match, default, and ordering behavior.
- Add negative tests (unknown type) and concurrency smoke tests for action visitors.
- Use example-driven tests to document behavior to new team members.

## Migration Tips
- Replace `switch`/`if` chains with `On<T>` registrations incrementally.
- Start with a thin visitor over existing logic; move logic into focused handlers gradually.
- Keep old code behind feature toggles while validating behavior parity.

## Security & Compliance
- Treat handlers as policy-enforcement points (authorization, validation) when applicable.
- Log minimally necessary PII; pass security context via handler closures rather than globals.

## Operations
- Expose versioning: recompose visitors per release; keep factories discoverable.
- Document defaults clearly: what is logged, when it triggers, how to extend.
