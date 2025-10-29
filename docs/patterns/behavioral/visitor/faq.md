# Visitor — FAQ

Common questions about using the fluent Visitor APIs in PatternKit.

---

## What’s the difference between `Visitor<TBase, TResult>` and `ActionVisitor<TBase>`?

- `Visitor<TBase, TResult>` returns a value from handlers.
- `ActionVisitor<TBase>` only performs side effects; no return value.

---

## How do I avoid exceptions when no type matches?

Either add `.Default(...)` or call `TryVisit(...)` (`TryVisitAsync(...)`) which returns `false` instead of throwing.

---

## Does registration order matter?

Yes. The first matching registration wins. Put more specific types before base types.

---

## Are visitors thread‑safe?

Built visitors are immutable and thread‑safe. Builders are not thread‑safe. Ensure your handlers’ dependencies are safe for concurrent use.

---

## Can I compose visitors?

Yes. Compose per module (e.g., Billing, Catalog), then combine at an edge (router, renderer). This keeps chains short and local.

---

## Can I mix sync and async handlers?

Use `AsyncVisitor`/`AsyncActionVisitor` for async; they support sync adapters (`.On<T>(Func<T, TResult>)` wraps to `ValueTask`).

---

## How do I test visitors?

Unit‑test match ordering, default behavior, and negative cases (unknown types). See `test/PatternKit.Tests/Behavioral/VisitorTests.cs:16` and `test/PatternKit.Tests/Behavioral/AsyncVisitorTests.cs:16`.

---

## Is this the same as classic GoF Visitor?

Conceptually yes (separate operations from types) but implemented non‑intrusively: you don’t modify domain types or implement `Accept(...)`.

---

## What about performance?

Dispatch is O(N) over registrations using a tight `for` loop. Keep frequent matches early and shard very large hierarchies if needed.

---

## Can I handle open generics?

Visitors work with concrete runtime types. For open generic families, register the concrete constructed types at composition time.

