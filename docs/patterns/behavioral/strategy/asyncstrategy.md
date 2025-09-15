# AsyncStrategy\<TIn, TOut>

A **first-match-wins, asynchronous strategy**: evaluate predicates in order and execute the handler for the **first**
branch that matches. Handlers return a `TOut` via `ValueTask<TOut>`.

Great for things like: async routing/dispatch, picking a storage backend, selecting an algorithm or serializer, or any
“try A, else B” flow that needs awaits.

---

## Why use it

* **Deterministic control flow**: registration order = evaluation order; only the first match runs.
* **Async-first**: both predicates and handlers can await.
* **Explicit defaults**: optional fallback handler when nothing matches.
* **Immutable & thread-safe** after `Build()` (safe for concurrent calls).
* **Allocation-light**: uses arrays and `ValueTask` to minimize overhead.

---

## TL;DR example

```csharp
using PatternKit.Behavioral.Strategy;

var strat = AsyncStrategy<int, string>.Create()
    .When((n, ct) => new ValueTask<bool>(n < 0))
        .Then((n, ct) => new ValueTask<string>("negative"))
    .When((n, ct) => new ValueTask<bool>(n == 0))
        .Then((n, ct) => new ValueTask<string>("zero"))
    .Default((n, ct) => new ValueTask<string>("positive"))
    .Build();

var s1 = await strat.ExecuteAsync(-5); // "negative"
var s2 = await strat.ExecuteAsync(0);  // "zero"
var s3 = await strat.ExecuteAsync(7);  // "positive"
```

---

## Building branches

Each branch is a **predicate + handler** pair:

```csharp
var s = AsyncStrategy<Request, Response>.Create()
    .When((req, ct) => new ValueTask<bool>(req.Path.StartsWith("/admin")))
        .Then(async (req, ct) =>
        {
            await Audit(req, ct);
            return await HandleAdmin(req, ct);
        })
    .When((req, ct) => new ValueTask<bool>(req.Path.StartsWith("/api/")))
        .Then((req, ct) => HandleApi(req, ct)) // already returns ValueTask<Response>
    .Default((req, ct) => NotFound(req))       // fallback runs if nothing matched
    .Build();
```

**First match wins**: if multiple predicates are `true`, only the earliest registered one runs.

---

## Defaults & errors

* If you provide **`.Default(handler)`**, it runs when no predicates match.
* If you **omit** a default and nothing matches, `ExecuteAsync` throws `InvalidOperationException`
  to signal **no branch matched**.

---

## Cancellation

`CancellationToken` flows into both predicate and handler:

```csharp
var s = AsyncStrategy<int, string>.Create()
    .When((_, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<bool>(true);
    })
    .Then((_, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<string>("ok");
    })
    .Build();
```

If the token is canceled, your predicate/handler can throw `OperationCanceledException` and the call will surface it.

---

## Synchronous adapters (nice for quick wiring)

You don’t have to write `ValueTask` everywhere:

```csharp
var s = AsyncStrategy<int, string>.Create()
    .When(n => n % 2 == 0)                    // sync predicate
        .Then((_, _) => new ValueTask<string>("even"))
    .When((n, _) => new ValueTask<bool>(n >= 0))
        .Then((_, _) => new ValueTask<string>("nonneg"))
    .Default(_ => "other")                    // sync default
    .Build();

await s.ExecuteAsync(2);   // "even"
await s.ExecuteAsync(1);   // "nonneg"
await s.ExecuteAsync(-1);  // "other"
```

---

## Testing (TinyBDD style)

```csharp
[Scenario("First matching async branch runs; default used when none match")]
[Fact]
public async Task FirstMatchAndDefault()
{
    var log = new List<string>();
    var strat = AsyncStrategy<int, string>.Create()
        .When((n, _) => new ValueTask<bool>(n > 0))
            .Then((n, _) => { log.Add("pos"); return new ValueTask<string>("+" + n); })
        .When((n, _) => new ValueTask<bool>(n < 0))
            .Then((n, _) => { log.Add("neg"); return new ValueTask<string>(n.ToString()); })
        .Default((_, _) => new ValueTask<string>("zero"))
        .Build();

    var r1 = await strat.ExecuteAsync(5);
    Assert.Equal("+5", r1);
    Assert.Equal("pos", string.Join("|", log));
}
```

Our repository includes comprehensive tests covering: first-match behavior, default vs. throw, sync adapters, order
guarantees, and cancellation.

---

## Design notes

* **Built on `BranchBuilder`**: the builder collects `(Predicate, Handler)` pairs and compiles them into arrays.
* **`ValueTask` everywhere**: avoids allocating `Task` for already-completed operations.
* **No reflection / no LINQ** in the hot path: simple loops over arrays.

---

## Gotchas

* **Order matters.** Put the most specific predicates first.
* **Default is optional.** Without it, expect `InvalidOperationException` when nothing matches.
* **Predicate/handler exceptions** are not swallowed—let them surface or handle them upstream.

---

## See also

* **ActionStrategy\<TIn>** — first-match actions with no return value.
* **Strategy\<TIn, TOut>** — synchronous result-producing strategy (throws on no match).
* **TryStrategy\<TIn, TOut>** — synchronous, result-producing strategy that can “not match” without throwing.
* **BranchBuilder** — the generic composition utility AsyncStrategy builds upon.

A **first-match-wins, asynchronous strategy**: evaluate predicates in order and execute the handler for the **first**
branch that matches. Handlers return a `TOut` via `ValueTask<TOut>`.

Great for things like: async routing/dispatch, picking a storage backend, selecting an algorithm or serializer, or any
“try A, else B” flow that needs awaits.

---

## Why use it

* **Deterministic control flow**: registration order = evaluation order; only the first match runs.
* **Async-first**: both predicates and handlers can await.
* **Explicit defaults**: optional fallback handler when nothing matches.
* **Immutable & thread-safe** after `Build()` (safe for concurrent calls).
* **Allocation-light**: uses arrays and `ValueTask` to minimize overhead.

---

## TL;DR example

```csharp
using PatternKit.Behavioral.Strategy;

var strat = AsyncStrategy<int, string>.Create()
    .When((n, ct) => new ValueTask<bool>(n < 0))
        .Then((n, ct) => new ValueTask<string>("negative"))
    .When((n, ct) => new ValueTask<bool>(n == 0))
        .Then((n, ct) => new ValueTask<string>("zero"))
    .Default((n, ct) => new ValueTask<string>("positive"))
    .Build();

var s1 = await strat.ExecuteAsync(-5); // "negative"
var s2 = await strat.ExecuteAsync(0);  // "zero"
var s3 = await strat.ExecuteAsync(7);  // "positive"
```

---

## Building branches

Each branch is a **predicate + handler** pair:

```csharp
var s = AsyncStrategy<Request, Response>.Create()
    .When((req, ct) => new ValueTask<bool>(req.Path.StartsWith("/admin")))
        .Then(async (req, ct) =>
        {
            await Audit(req, ct);
            return await HandleAdmin(req, ct);
        })
    .When((req, ct) => new ValueTask<bool>(req.Path.StartsWith("/api/")))
        .Then((req, ct) => HandleApi(req, ct)) // already returns ValueTask<Response>
    .Default((req, ct) => NotFound(req))       // fallback runs if nothing matched
    .Build();
```

**First match wins**: if multiple predicates are `true`, only the earliest registered one runs.

---

## Defaults & errors

* If you provide **`.Default(handler)`**, it runs when no predicates match.
* If you **omit** a default and nothing matches, `ExecuteAsync` throws `InvalidOperationException`
  to signal **no branch matched**.

---

## Cancellation

`CancellationToken` flows into both predicate and handler:

```csharp
var s = AsyncStrategy<int, string>.Create()
    .When((_, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<bool>(true);
    })
    .Then((_, ct) =>
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<string>("ok");
    })
    .Build();
```

If the token is canceled, your predicate/handler can throw `OperationCanceledException` and the call will surface it.

---

## Synchronous adapters (nice for quick wiring)

You don’t have to write `ValueTask` everywhere:

```csharp
var s = AsyncStrategy<int, string>.Create()
    .When(n => n % 2 == 0)                    // sync predicate
        .Then((_, _) => new ValueTask<string>("even"))
    .When((n, _) => new ValueTask<bool>(n >= 0))
        .Then((_, _) => new ValueTask<string>("nonneg"))
    .Default(_ => "other")                    // sync default
    .Build();

await s.ExecuteAsync(2);   // "even"
await s.ExecuteAsync(1);   // "nonneg"
await s.ExecuteAsync(-1);  // "other"
```

---

## Testing (TinyBDD style)

```csharp
[Scenario("First matching async branch runs; default used when none match")]
[Fact]
public async Task FirstMatchAndDefault()
{
    var log = new List<string>();
    var strat = AsyncStrategy<int, string>.Create()
        .When((n, _) => new ValueTask<bool>(n > 0))
            .Then((n, _) => { log.Add("pos"); return new ValueTask<string>("+" + n); })
        .When((n, _) => new ValueTask<bool>(n < 0))
            .Then((n, _) => { log.Add("neg"); return new ValueTask<string>(n.ToString()); })
        .Default((_, _) => new ValueTask<string>("zero"))
        .Build();

    var r1 = await strat.ExecuteAsync(5);
    Assert.Equal("+5", r1);
    Assert.Equal("pos", string.Join("|", log));
}
```

Our repository includes comprehensive tests covering: first-match behavior, default vs. throw, sync adapters, order
guarantees, and cancellation.

---

## Design notes

* **Built on `BranchBuilder`**: the builder collects `(Predicate, Handler)` pairs and compiles them into arrays.
* **`ValueTask` everywhere**: avoids allocating `Task` for already-completed operations.
* **No reflection / no LINQ** in the hot path: simple loops over arrays.

---

## Gotchas

* **Order matters.** Put the most specific predicates first.
* **Default is optional.** Without it, expect `InvalidOperationException` when nothing matches.
* **Predicate/handler exceptions** are not swallowed—let them surface or handle them upstream.

---

## See also

* **ActionStrategy\<TIn>** — first-match actions with no return value.
* **Strategy\<TIn, TOut>** — synchronous result-producing strategy (throws on no match).
* **TryStrategy\<TIn, TOut>** — synchronous, result-producing strategy that can “not match” without throwing.
* **BranchBuilder** — the generic composition utility AsyncStrategy builds upon.
