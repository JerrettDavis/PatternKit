# Visitor — Alternatives & Comparisons

This page compares PatternKit’s fluent Visitor with common alternatives in C# and when to choose each.

---

## C# Pattern Matching (`switch` / `is`)

Pros
- Built‑in and terse for one‑off decisions
- Exhaustiveness checks in some cases

Cons
- Hard to centralize and reuse across the codebase
- Grows unwieldy as operations multiply; logic scatters

Choose pattern matching when the branching is local and not reused.

---

## Virtual Methods / Classic OO Polymorphism

Pros
- Behavior lives with the type; easy to find
- No external registry

Cons
- You must modify types for every new operation
- Can bloat domain types with unrelated concerns

Choose virtual methods when the behavior is core to the type and changes with it.

---

## Classic GoF Visitor (Double‑Dispatch)

Pros
- Compile‑time safety via `Accept(Visitor)` across all nodes

Cons
- Intrusive: every type must implement `Accept`
- Adding a new subtype requires touching the visitor interface and all implementations

PatternKit’s visitors are non‑intrusive and fluent: no changes to your domain types.

---

## Dictionaries of `Type` → Delegate

Pros
- Simple mapping for small cases

Cons
- Manual type casting; error‑prone
- No ordering / specificity control (base vs derived)
- No first‑match / default semantics built‑in

PatternKit provides ordering, default handling, and strong typing out of the box.

---

## Strategy Pattern

Pros
- Predicate‑based dispatch, not type‑based
- Great for content negotiation, rule packs

Cons
- Not keyed by runtime type

Use `Strategy` for predicate logic; use `Visitor` for runtime type dispatch.

