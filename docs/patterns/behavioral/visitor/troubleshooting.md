# Visitor — Troubleshooting

Tips for common issues when using Visitors.

---

## `InvalidOperationException: No strategy matched`

Cause
- No registration matched and no `.Default(...)` set.

Fix
- Add `.Default(...)` for resilience, or use `TryVisit(...)` and branch on the boolean result.

---

## Handler never runs (shadowed by base type)

Cause
- A base type registration appears before a more specific subtype.

Fix
- Move the specific `On<Derived>` registration before the base `On<Base>`.

---

## Concurrent usage issues

Cause
- Sharing the builder across threads; or handlers depend on non‑thread‑safe services.

Fix
- Build once and share the built instance. Ensure handler dependencies are thread‑safe or scoped properly.

---

## Unexpected allocations

Cause
- Capturing large closures; using boxing inside handlers; returning large new objects per call.

Fix
- Keep handlers lean, avoid boxing/value‑type to object conversions, reuse buffers where appropriate.

---

## Async deadlocks / responsiveness issues

Cause
- Blocking in async handlers, not honoring `CancellationToken`.

Fix
- Make handlers truly async, pass `ct`, and bail early on cancellation.

