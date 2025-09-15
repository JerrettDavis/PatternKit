# Result Chain (`Behavioral.Chain.ResultChain`)

A **first–match-wins** chain that can *produce a value*.
Each handler receives `(in TIn input, out TOut? result, Next next)` and can either:

* **Produce** a result and return `true` → the chain short-circuits, or
* **Delegate** to `next(input, out result)` → the chain continues.

It’s the “router with a return value” sibling of [`ActionChain<TCtx>`](./actionchain.md).

---

## Why use `ResultChain`?

Use it when you need **ordered rules that return something**:

* HTTP‐ish routing → `HttpResponse`
* Command parsers → `ICommand`
* Promotion/price/feature selection → `CalculationResult`
* Fallback/“NotFound” defaults via a terminal tail

If you only need **side effects**, prefer `ActionChain<TCtx>`.
If you want a simpler **first match mapping** (no `next`), see `Strategy`/`TryStrategy`.

---

## Quick example: tiny router

```csharp
using PatternKit.Behavioral.Chain;

public readonly record struct Request(string Method, string Path);
public readonly record struct Response(int Status, string Body);

var router = ResultChain<Request, Response>.Create()
    // GET /health
    .When(static (in r) => r.Method == "GET" && r.Path == "/health")
        .Then(r => new Response(200, "OK"))
    // GET /users/{id}
    .When(static (in r) => r.Method == "GET" && r.Path.StartsWith("/users/"))
        .Then(r => new Response(200, $"user:{r.Path[7..]}"))
    // default / not found
    .Finally(static (in _, out Response? res, _) => { res = new(404, "not found"); return true; })
    .Build();

var ok1 = router.Execute(in new Request("GET", "/health"), out var res1);  // ok1=true, res1=200 OK
var ok2 = router.Execute(in new Request("GET", "/nope"),   out var res2);  // ok2=true, res2=404
```

### “Sometimes produce, sometimes delegate”

Use `When(...).Do(handler)` when a rule might *conditionally* produce and otherwise pass control onward:

```csharp
var chain = ResultChain<int, string>.Create()
    .When(static (in x) => x % 2 == 0).Do(static (in x, out string? r, ResultChain<int,string>.Next next) =>
    {
        // Only handle THE answer; otherwise delegate
        if (x == 42) { r = "forty-two"; return true; }
        return next(in x, out r);
    })
    .When(static (in x) => x % 2 == 0).Then(_ => "even") // handles delegated evens
    .Finally(static (in _, out string? r, _) => { r = "odd"; return true; })
    .Build();
```

---

## API surface

```csharp
public sealed class ResultChain<TIn, TOut>
{
    public delegate bool Next(in TIn input, out TOut? result);
    public delegate bool TryHandler(in TIn input, out TOut? result, Next next);
    public delegate bool Predicate(in TIn input);

    public bool Execute(in TIn input, out TOut? result);

    public sealed class Builder
    {
        public Builder Use(TryHandler handler);
        public WhenBuilder When(Predicate predicate);
        public Builder Finally(TryHandler tail);  // terminal fallback
        public ResultChain<TIn, TOut> Build();

        public sealed class WhenBuilder
        {
            public Builder Do(TryHandler handler);         // may produce OR delegate
            public Builder Then(Func<TIn, TOut> produce);  // produces and stops
        }
    }

    public static Builder Create();
}
```

### Semantics

* **Order is preserved.** First matching producer wins.
* **`Then(Func<TIn,TOut>)`**: if predicate is true → produce result, return `true`.
* **`Do(TryHandler)`**: you decide to produce or delegate by calling `next`.
* **`Finally(TryHandler)`**: runs *only* if the chain reaches the tail (i.e., nobody produced earlier).
  Typical use: default/NotFound.
* **`Execute(...)`** returns:

    * `true` when any handler (or `Finally`) produced a result; `result` is set.
    * `false` when no one produced and no `Finally` ran; `result` is `default`.

---

## Patterns

### 1) Default/NotFound tail

```csharp
.Finally(static (in _, out MyResult? r, _) => { r = MyResult.NotFound; return true; })
```

### 2) Multi-stage processing

Chain “can I handle this?” steps. Each step may produce, or else delegate:

```csharp
.When(static (in x) => IsFastPath(x)).Do(static (in x, out R? r, var next) =>
{
    if (TryFast(x, out r)) return true;
    return next(in x, out r); // let later handlers try
})
.When(static (in x) => IsSlowPath(x)).Then(SlowCompute)
```

### 3) Cross-cut logging without duplication

Put it in `Finally` **only if** you want it to run when nothing matched.
Otherwise log inside the `Then/Do` that produced.

---

## Performance & threading

* The chain composes to a **single delegate** at `Build()` time (reverse fold).
  No allocations during `Execute` besides what your handlers do.
* The built chain is **immutable** and **thread-safe**. Builders are **not** thread-safe.

---

## Gotchas & tips

* For lambdas passed to `When(...)`, the parameter is **`in`**.
  Prefer explicit static lambdas to avoid captures and compiler warnings:

  ```csharp
  .When(static (in r) => r.Flag)     // good
  .Then(static r => ...)             // Then’s lambda is a normal parameter (no `in`)
  ```

* If you omit `Finally` and nothing produces, `Execute` returns `false` and `result` is `default`.

---

## Tests

See `PatternKit.Tests/Behavioral/Chain/ResultChainTests.cs` for executable specs covering:

* First match wins; fallback via `Finally`
* No tail → `Execute` returns `false`
* `When.Do` → produce vs delegate
* Registration order guarantees
* Tail runs only when no earlier producer

These tests use **TinyBDD** so the assertions read like documentation.
