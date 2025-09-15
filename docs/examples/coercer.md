# Coercer\<T> — strategy-driven, allocation-light type coercion

**Goal:** turn “whatever came in” (JSON, strings, primitives) into the *type you actually want*—without `if/else` piles or reflection in the hot path.

This demo shows how `Coercer<T>` compiles a tiny **TryStrategy** pipeline once per closed generic (e.g. `Coercer<int>`, `Coercer<string[]>`) and then uses **first-match-wins** handlers to coerce values at runtime.

---

## Why use it

* **Fast path first:** already-typed values return immediately (no copies, no boxing/unboxing churn).
* **Deterministic order:** a small array of non-capturing delegates runs top-to-bottom; the first success wins.
* **Culture-safe:** the fallback conversion uses `InvariantCulture` so your tests and prod behave the same on Windows/Linux.
* **Tiny surface:** just call `Coercer<T>.From(object?)` or `any.Coerce<T>()`.

---

## Quick start

```csharp
using System.Text.Json;
using PatternKit.Examples.Coercion;

// From JSON
var i  = Coercer<int>.From(JsonDocument.Parse("123").RootElement);            // 123
var b  = Coercer<bool>.From(JsonDocument.Parse("true").RootElement);          // true
var s  = Coercer<string>.From(JsonDocument.Parse("\"hello\"").RootElement);   // "hello"
var xs = Coercer<string[]>.From(JsonDocument.Parse("[\"a\",\"b\"]").RootElement); // ["a","b"]

// From “anything”
int? viaExt     = ((object)"27").Coerce<int>();      // 27  (Convertible fallback, invariant)
double? d       = ((object)"2.25").Coerce<double>(); // 2.25
string[]? one   = ((object)"only").Coerce<string[]>(); // ["only"]

// Nulls return default(T)
int? none = Coercer<int?>.From(null); // null
```

---

## What `Coercer<T>` handles by default

`Coercer<T>.From(object?)` applies these steps **in order**:

1. **Direct cast (fast path)**
   If the input is already `T`, return it as-is.

2. **Typed handlers** (first-match-wins)

| Input                      | Target `T`                                  | Behavior                                     |
| -------------------------- | ------------------------------------------- | -------------------------------------------- |
| `JsonElement` (any)        | `string`                                    | `je.ToString()`                              |
| `JsonElement` (array)      | `string[]`                                  | Enumerate elements, `.ToString()` each       |
| `JsonElement` (number)     | `int` / `float` / `double` (incl. nullable) | `GetInt32()` / `GetSingle()` / `GetDouble()` |
| `JsonElement` (true/false) | `bool` (incl. nullable)                     | `GetBoolean()`                               |
| `string`                   | `string[]`                                  | Wrap into single-element array               |

3. **Convertible fallback (last resort)**
   If the input is `IConvertible` and the target (or its nullable underlying type) is **primitive or `decimal`**, use:

```csharp
Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)
```

If no handler succeeds, return `default(T)`.

---

## Ordering & priority (why the results are stable)

The strategy array is compiled once per `Coercer<T>` at type init. Runtime calls **don’t branch on type**; they just loop through a small array of delegates:

* **DirectCast** runs first (zero cost success).
* A **type-specific block** (e.g., “if T is `string[]`”) injects only the relevant handlers.
* **ConvertibleFallback** is *always last* so it can’t steal cases that have precise JSON readers.

This order is what makes things like `JsonElement 123 → int` culture-proof and fast.

---

## Culture & precision notes

* The fallback uses **`InvariantCulture`**. `"2.25"` parses as 2.25 regardless of OS locale.
* JSON numeric handlers (`GetInt32`, `GetSingle`, `GetDouble`) avoid string round-trips and honor JSON number semantics.
* If you need bankers’ rounding or custom numeric policy, add a bespoke handler (see below).

---

## Extending or customizing

`Coercer<T>`’s default pipeline lives in `Build()`. To add a new target (e.g., `Guid`) or tweak ordering:

* **Library edit**: add a new handler in `Build()` (e.g., for `Guid`), placing it *before* `ConvertibleFallback`.
* **Wrapper approach**: write your own converter and call it **before** `Coercer<T>`:

```csharp
static Guid? TryGuid(object? v)
{
    if (v is string s && Guid.TryParse(s, out var g)) return g;
    if (v is JsonElement je && je.ValueKind == JsonValueKind.String &&
        Guid.TryParse(je.GetString(), out g)) return g;
    return null;
}

static Guid? CoerceGuid(object? v) => TryGuid(v) ?? Coercer<Guid>.From(v);
```

Use the wrapper when you can’t or don’t want to change the shared coercer.

---

## Error behavior

No exceptions are thrown for failed coercions—handlers just return `false` and the next one tries. The fallback catches and swallows `Convert.ChangeType` exceptions.

---

## Tests (TinyBDD)

See `PatternKit.Examples.Tests/Coercion/CoercerTests.cs`. They read like specifications:

* JSON → primitives (`int`, `float`, `double`, `bool`)
* JSON → `string` / `string[]`
* Single `string` → `string[]`
* Convertible fallback for `"27"`, `"2.25"`, etc.
* Ordering guard: JSON numeric handlers beat the fallback
* Culture stability (`InvariantCulture`)

> Tip (Linux/macOS): our test host pins `en-US` to keep currency/number formatting stable across platforms.

---

## Performance cheatsheet

* **No LINQ** in the hot path; the handlers are a flat array of non-capturing delegates.
* **Zero alloc** for direct-cast successes; obvious allocations only when building `string[]` or when JSON `.ToString()` is used.
* **Thread-safe**: the strategy array is immutable per closed generic and reused across calls.

---

## Troubleshooting

* **Got `default(T)` back?** No handler matched. Check target type and the exact input shape (`JsonElement.ValueKind`, nullable vs. non-nullable).
* **Parsed a localized number incorrectly?** Ensure you rely on JSON handlers (preferred) or feed `InvariantCulture`-formatted text; the fallback already uses `InvariantCulture`.
* **Need decimals?** Add a `JsonElement → decimal` handler, then slot it before `ConvertibleFallback`.
