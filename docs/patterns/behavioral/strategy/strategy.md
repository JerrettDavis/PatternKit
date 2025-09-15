# Strategy\<TIn, TOut>

A **first-match-wins, synchronous strategy**: evaluate predicates in order and execute the handler for the **first** branch that matches. The chosen handler returns a `TOut`.

Use it to replace `switch`/`if-else` cascades with a small, composable decision pipeline: routing, labelers, mappers, pick-an-algorithm, etc.

---

## What it is

* **Deterministic branching**: registration order = evaluation order; only the first match runs.
* **Return a value**: each handler produces a `TOut`.
* **Optional default**: a fallback handler when nothing matches; otherwise `Execute` throws.
* **Immutable & thread-safe** after `Build()`.

> If you want a non-throwing variant, see **TryStrategy\<TIn, TOut>**.
> If you only need side effects (no return), see **ActionStrategy\<TIn>**.

---

## TL;DR example

```csharp
using PatternKit.Behavioral.Strategy;

var classify = Strategy<int, string>.Create()
    .When(static i => i > 0).Then(static _ => "positive")
    .When(static i => i < 0).Then(static _ => "negative")
    .Default(static _ => "zero")
    .Build();

var a = classify.Execute( 7); // "positive"
var b = classify.Execute(-3); // "negative"
var c = classify.Execute( 0); // "zero"
```

If you **omit** `.Default(...)` and nothing matches, `Execute` throws `InvalidOperationException` (via `Throw.NoStrategyMatched<T>()`).

---

## Building branches

Each branch is a **predicate + handler** pair:

```csharp
var chooseStorage = Strategy<string, IBlobStore>.Create()
    .When(static path => path.StartsWith("s3:", StringComparison.Ordinal))
        .Then(static _ => new S3BlobStore())
    .When(static path => path.StartsWith("gs:", StringComparison.Ordinal))
        .Then(static _ => new GcsBlobStore())
    .Default(static _ => new FileSystemBlobStore())
    .Build();
```

**First match wins.** If more than one predicate is `true`, only the earliest one runs.

---

## Typical patterns

### 1) Simple mapping / labeling

```csharp
var label = Strategy<int, string>.Create()
    .When(static n => (n & 1) == 0).Then(static _ => "even")
    .When(static n => n % 3 == 0).Then(static _ => "div3")
    .Default(static _ => "other")
    .Build();
```

### 2) Content negotiation (sync)

```csharp
var pickWriter = Strategy<string, IWriter>.Create()
    .When(static ct => ct == "application/json").Then(static _ => new JsonWriter())
    .When(static ct => ct == "text/csv").Then(static _ => new CsvWriter())
    .Default(static _ => new TextWriter())
    .Build();
```

### 3) Rule packs with “most specific first”

```csharp
var price = Strategy<Item, Money>.Create()
    .When(static it => it.OnSale).Then(static it => it.BasePrice * 0.8m)
    .When(static it => it.IsWholesale).Then(static it => it.BasePrice * 0.9m)
    .Default(static it => it.BasePrice)
    .Build();
```

---

## API shape

```csharp
var s = Strategy<TIn, TOut>.Create()
    .When(static (in TIn x) => /* bool */).Then(static (in TIn x) => /* TOut */)
    .Default(static (in TIn x) => /* TOut */) // optional
    .Build();

TOut result = s.Execute(in input); // throws if no match and no default
```

* **`When(predicate).Then(handler)`**: registers a branch.
* **`.Default(handler)`**: sets the fallback when no predicates match.
* **`Execute(in TIn)`**: runs the first matching handler or the default; throws if neither exists.

All delegates accept `in TIn` for zero-copy pass-through of structs.

---

## Testing (TinyBDD style)

```csharp
[Scenario("First-match wins; default runs when none match")]
[Fact]
public async Task Strategy_FirstMatch_Default()
{
    var strat = Strategy<int, string>.Create()
        .When(static i => i > 0).Then(static _ => "pos")
        .When(static i => i < 0).Then(static _ => "neg")
        .Default(static _ => "zero")
        .Build();

    await Given("the strategy", () => strat)
        .When("Execute(3)",  s => s.Execute(3))
        .Then("is 'pos'",   r => r == "pos")
        .When("Execute(-2)", s => strat.Execute(-2))
        .Then("is 'neg'",    r => r == "neg")
        .When("Execute(0)",  s => strat.Execute(0))
        .Then("is 'zero'",   r => r == "zero")
        .AssertPassed();
}
```

---

## Design notes

* **No LINQ / reflection in the hot path** — predicates/handlers are arrays iterated with a simple `for` loop.
* **Immutability** — after `Build()` the strategy can be shared across threads.
* **Order matters** — put the most specific predicates first.

---

## Gotchas

* **No default + no match ⇒ throw.** Use **TryStrategy\<TIn, TOut>** if you want a non-throwing “no match” path (`bool Execute(in, out TOut?)`).
* **Side effects?** Prefer **ActionStrategy\<TIn>** when you only need actions and no return value.
* **Async?** Use **AsyncStrategy\<TIn, TOut>** when your predicates/handlers await.

---

## See also

* **TryStrategy\<TIn, TOut>** — first-match with `bool` success + `out` result (no throw on no match).
* **ActionStrategy\<TIn>** — first-match, side-effect only (no result).
* **AsyncStrategy\<TIn, TOut>** — first-match with async handlers returning `ValueTask<TOut>`.
* **BranchBuilder** — the low-level composer used by all strategies.
