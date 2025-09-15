# TryStrategy<TIn, TOut>

A **first-success, non-throwing strategy**: evaluate a sequence of `Try` handlers in order until one succeeds. Each handler attempts to produce a `TOut` and returns `true` on success (setting the `out` value) or `false` to let the next handler try.

Use `TryStrategy` when you want a safe, allocation-light parsing/coercion pipeline (e.g., `Coercer<T>`) or any "try A, else B" flow where failures are expected and should not throw.

---

## TL;DR

```csharp
var parser = TryStrategy<string, int>.Create()
    .Always((in string s, out int r) => int.TryParse(s, out r))
    .Finally((in string _, out int r) => { r = 0; return true; })
    .Build();

if (parser.Execute("123", out var n)) Console.WriteLine(n); // 123
```

`Execute(in, out)` returns `true` when a handler produced a value; otherwise `false` and `out` is `default` (unless the optional `Finally` provided a fallback).

---

## What it is

* **First-success wins**: handlers are tried in registration order; the first that returns `true` wins.
* **Non-throwing**: `Execute` signals success via a `bool`, never throws just because no handler matched.
* **Low-cost hot path**: handlers are compiled into arrays and iterated in a simple `for` loop.

---

## API shape

```csharp
var b = TryStrategy<TIn, TOut>.Create()
    .Always(TryHandler)     // append a handler that may succeed
    .Finally(TryHandler)    // optional fallback (always runs if provided)
    .Build();

bool ok = b.Execute(in input, out TOut? result);
```

* `Always(TryHandler)` (or `.When(...).ThenTry(...)` in some builders) registers attempts.
* `Finally(TryHandler)` provides a guaranteed fallback; callers can still use the boolean result to detect whether a "real" handler succeeded.

---

## Typical patterns

* **Coercion / parsing**: chain a set of type-specific parsers, ending with a convertible fallback. See `Coercer<T>`.
* **Content negotiation**: try several negotiators until one reports `true` and sets a content type.
* **Loose deserialization**: try JSON, then CSV, then plain string parsing.

---

## Gotchas

* Handlers must not swallow exceptions you want surfaced; if a handler throws, the exception will propagate unless you explicitly catch inside the handler.
* Registration order matters: put the fastest and most-specific handlers first.

---

## See also

* [Strategy](./strategy.md) — first-match that returns a `TOut` and throws when nothing matches.
* [ActionStrategy](./actionstrategy.md) — first-match actions with no return value.
* [AsyncStrategy](./asyncstrategy.md) — async first-match strategy.
* [BranchBuilder](../../creational/builder/branchbuilder.md) — the low-level composer used by strategy families.

