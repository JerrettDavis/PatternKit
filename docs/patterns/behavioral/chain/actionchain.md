# Behavioral.Chain.ActionChain

**ActionChain\<TCtx>** is a tiny, middleware-style pipeline for “branchless rule packs.”
Each step receives the current context and a `next` delegate; it can **continue** or **short-circuit**.

Use it when you want *ordered rules* (logging, validation, pre-auth, pricing, etc.) without `if` ladders, and when you
need very explicit continue/stop semantics.

---

## TL;DR

```csharp
using PatternKit.Behavioral.Chain;

var log = new List<string>();

var chain = ActionChain<HttpRequest>.Create()
    .When((in r) => r.Headers.ContainsKey("X-Request-Id"))
    .ThenContinue(r => log.Add($"reqid={r.Headers["X-Request-Id"]}"))

    .When((in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal) &&
                    !r.Headers.ContainsKey("Authorization"))
    .ThenStop(r => log.Add("deny: missing auth"))

    // Tail runs only if earlier steps called `next`
    .Finally((in r, next) =>
    {
        log.Add($"{r.Method} {r.Path}");
        next(in r); // terminal `next` is a no-op
    })
    .Build();

chain.Execute(new HttpRequest("GET", "/health", new Dictionary<string,string>()));
chain.Execute(new HttpRequest("GET", "/admin/metrics", new Dictionary<string,string>()));
// => ["GET /health", "deny: missing auth", "GET /admin/metrics"]
```

---

## Why ActionChain?

* **Linear, readable rule packs**: each rule says *when* it applies and *what* it does.
* **Strict stop by default**: if a handler doesn’t call `next`, the chain ends immediately.
* **Low overhead**: builds a single, composed delegate; `Execute` is just one call.
* **Perf-shaped**: `in` parameters avoid copies; no LINQ; minimal allocations after `Build()`.

---

## Core API

```csharp
public sealed class ActionChain<TCtx>
{
    public delegate void Next(in TCtx ctx);
    public delegate void Handler(in TCtx ctx, Next next);
    public delegate bool Predicate(in TCtx ctx);

    public void Execute(in TCtx ctx);

    public static Builder Create();

    public sealed class Builder
    {
        // Always-run middleware (if continued)
        public Builder Use(Handler handler);

        // Conditional block
        public WhenBuilder When(Predicate predicate);

        // Tail handler (runs only if chain wasn’t short-circuited)
        public Builder Finally(Handler tail);

        public ActionChain<TCtx> Build();
    }

    public sealed class WhenBuilder
    {
        // Run custom handler when predicate is true; else continue automatically
        public Builder Do(Handler handler);

        // Run action and STOP the chain when predicate is true
        public Builder ThenStop(Action<TCtx> action);

        // Run action and CONTINUE when predicate is true
        public Builder ThenContinue(Action<TCtx> action);
    }
}
```

### Semantics (important!)

* **Strict stop**: Any handler can end the chain by not calling `next`.
  This also **skips `Finally`**. If you truly need “always run,” split logging into a separate chain or ensure every
  earlier step calls `next`.
* **Ordering matters**: handlers run in the order you register them.
* **`When(...).ThenContinue` vs `ThenStop`**:

    * `ThenContinue` executes the action and *always* calls `next`.
    * `ThenStop` executes the action and *never* calls `next`.

---

## Patterns you’ll use

* **Auth gate + logging** (strict stop):

  ```csharp
  var chain = ActionChain<HttpRequest>.Create()
      .When(static (in r) => r.Path.StartsWith("/admin") &&
                             !r.Headers.ContainsKey("Authorization"))
      .ThenStop(r => audit.Deny(r)) // stop: no tail
      .Finally((in r, next) => { audit.Seen(r); next(in r); })
      .Build();
  ```

* **Pre-authorization checks** (multiple early exits):

  ```csharp
  var chain = ActionChain<Cart>.Create()
      .When(static (in c) => c.Items.Count == 0)
      .ThenStop(c => c.Fail("empty-basket"))
      .When(static (in c) => c.CustomerAge < 21 && c.Items.Any(i => i.AgeRestricted))
      .ThenStop(c => c.Fail("age"))
      .Finally((in c, next) => { c.Pass("preauth-ok"); next(in c); })
      .Build();
  ```

* **Branchless rule packs** (totals/discounts):

  ```csharp
  var totals = ActionChain<Tx>.Create()
      .Use(static (in c, next) => { c.RecomputeSubtotal(); next(in c); })
      .When(static (in c) => c.FirstTenderIsCash).ThenContinue(c => c.AddDiscount(0.02m, "cash"))
      .When(static (in c) => c.HasLoyalty).ThenContinue(c => c.AddDiscount(0.05m, "loyalty"))
      .Finally(static (in c, next) => { c.ComputeTax(); next(in c); })
      .Build();
  ```

---

## Testing with TinyBDD (spec-style)

```csharp
await Given("a chain that denies /admin without auth", () =>
{
    var log = new List<string>();
    var chain = ActionChain<HttpRequest>.Create()
        .When((in r) => r.Path.StartsWith("/admin") &&
                        !r.Headers.ContainsKey("Authorization"))
        .ThenStop(r => log.Add("deny"))
        .Finally((in r, next) => { log.Add($"{r.Method} {r.Path}"); next(in r); })
        .Build();
    return (chain, log);
})
.When("GET /admin no auth", s => { s.chain.Execute(new("GET","/admin",new Dictionary<string,string>())); return s; })
.Then("first line is deny", s => s.log[0] == "deny")
.And("no tail logged (strict stop)", s => s.log.Count == 1)
.AssertPassed();
```

---

## Tips & gotchas

* **Want tail to always run?** Don’t use `ThenStop` earlier, or move the “always-run” logic to a separate chain invoked
  after this one.
* **Avoid captures**: copy locals (`var pred = _pred;`) like the builder does, to keep delegates non-allocating.
* **Use `in` everywhere**: it keeps hot-path costs low for structs and larger contexts.
* **Compose freely**: you can wrap chains into stages (e.g., a transaction pipeline), or put chains behind higher-level
  builders.

---

## See also

* [ResultChain](./resultchain.md) – like ActionChain, but steps return a result and short-circuit on failure.
* [BranchBuilder](../../creational/builder/branchbuilder.md) – zero-`if` router (predicate → step) for first-match-wins dispatch.
* [Strategy](../strategy/strategy.md) / [TryStrategy](../strategy/trystrategy.md) – single-choice or first-success selection for handlers/parsers.
