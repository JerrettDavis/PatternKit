# Auth & Logging with `ActionChain<HttpRequest>`

A tiny example that shows how to use **PatternKit.Behavioral.Chain** to build request-logging and auth checks with **no `if`/`else`** and **first-match-wins** semantics.

* **Goal:** log a request id when present, short-circuit unauthorized `/admin/*` requests, and (otherwise) log the method/path.
* **Key idea:** branchless pipelines using `When(...).ThenContinue(...)`, `When(...).ThenStop(...)`, and a `Finally(...)` tail.

---

## The demo pipeline

```csharp
using PatternKit.Behavioral.Chain;

public readonly record struct HttpRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers);
public readonly record struct HttpResponse(int Status, string Body);

public static class AuthLoggingDemo
{
    public static List<string> Run()
    {
        var log = new List<string>();

        var chain = ActionChain<HttpRequest>.Create()
            // 1) request id (continue)
            .When(static (in r) => r.Headers.ContainsKey("X-Request-Id"))
            .ThenContinue(r => log.Add($"reqid={r.Headers["X-Request-Id"]}"))

            // 2) admin requires auth (stop)
            .When(static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal)
                                   && !r.Headers.ContainsKey("Authorization"))
            .ThenStop(_ => log.Add("deny: missing auth"))

            // 3) tail – runs only if the chain did not stop
            .Finally((in r, next) =>
            {
                log.Add($"{r.Method} {r.Path}");
                next(r); // terminal next is a no-op
            })
            .Build();

        // simulate
        chain.Execute(new HttpRequest("GET", "/health", new Dictionary<string,string>()));
        chain.Execute(new HttpRequest("GET", "/admin/metrics", new Dictionary<string,string>()));

        return log;
    }
}
```

### What it logs (strict-stop semantics)

`Run()` returns:

```
GET /health
deny: missing auth
```

Why not a third line (`GET /admin/metrics`)? Because **`.ThenStop(...)` halts the pipeline** and the `Finally(...)` tail does **not** run after a stop. That’s by design—great for auth short-circuits.

---

## Mental model

* **First match wins**: the first `When(...)` whose predicate is `true` executes its `Then...` and the others are skipped.
* **`.ThenContinue(...)`**: perform side effects and continue evaluating later steps (and eventually `Finally`).
* **`.ThenStop(...)`**: perform side effects and **end the pipeline immediately** (no `Finally`).
* **`Finally(...)`**: tail step that runs **only if the chain didn’t stop**.

---

## Variant: “Always log method/path”

If you want method/path to be logged **even when denied**, move that logging up front with `Use(...)`:

```csharp
var chain = ActionChain<HttpRequest>.Create()
    .Use((in r, next) => { log.Add($"{r.Method} {r.Path}"); next(r); })
    .When(static (in r) => r.Headers.ContainsKey("X-Request-Id"))
    .ThenContinue(r => log.Add($"reqid={r.Headers["X-Request-Id"]}"))
    .When(static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal)
                           && !r.Headers.ContainsKey("Authorization"))
    .ThenStop(_ => log.Add("deny: missing auth"))
    .Build();
```

Now the simulated run yields:

```
GET /health
GET /admin/metrics
deny: missing auth
```

(Logging happens before the deny short-circuit.)

---

## TinyBDD smoke tests

Here’s a compact set that locks in the strict-stop behavior (no method/path after deny):

```csharp
using TinyBDD;
using TinyBDD.Xunit;
using Xunit;
using Xunit.Abstractions;

[Feature("Auth & Logging demo (ActionChain<HttpRequest>)")]
public sealed class AuthLoggingDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Run() logs health, then denies admin without trailing method/path")]
    [Fact]
    public async Task Demo_Run_Smoke()
    {
        await Given("the demo Run() helper", () => (Func<List<string>>)AuthLoggingDemo.Run)
            .When("running it", run => run())
            .Then("first line is GET /health", log => log.ElementAtOrDefault(0) == "GET /health")
            .And("second line is deny", log => log.ElementAtOrDefault(1) == "deny: missing auth")
            .And("stops after deny (no third line)", log => log.Count == 2)
            .AssertPassed();
    }

    [Scenario("Order with both: X-Request-Id then deny (no method/path)")]
    [Fact]
    public async Task RequestId_Then_Deny()
    {
        var log = new List<string>();
        var chain = ActionChain<HttpRequest>.Create()
            .When(static (in r) => r.Headers.ContainsKey("X-Request-Id"))
            .ThenContinue(r => log.Add($"reqid={r.Headers["X-Request-Id"]}"))
            .When(static (in r) => r.Path.StartsWith("/admin") && !r.Headers.ContainsKey("Authorization"))
            .ThenStop(_ => log.Add("deny: missing auth"))
            .Finally((in r, next) => { log.Add($"{r.Method} {r.Path}"); next(r); })
            .Build();

        await Given("the chain and log", () => (chain, log))
            .When("GET /admin/x with X-Request-Id and no auth", t =>
            {
                var (c, l) = t;
                c.Execute(new HttpRequest("GET", "/admin/x", new Dictionary<string,string>{{"X-Request-Id","rid-7"}}));
                return l;
            })
            .Then("reqid first", l => l.ElementAtOrDefault(0) == "reqid=rid-7")
            .And("deny second", l => l.ElementAtOrDefault(1) == "deny: missing auth")
            .And("no method/path after stop", l => l.Count == 2)
            .AssertPassed();
    }
}
```

> Tip: use `ElementAtOrDefault` in tests to avoid index exceptions and get clearer failure messages.

---

## When to use this pattern

* **Middleware-like** concerns where some steps are pure side effects (logging, metrics, request-id) and others **short-circuit** (auth, feature gates).
* Places where you want **declarative ordering** and **no nested conditionals**—add/remove rules without touching the rest.

That’s it—simple, predictable, and production-friendly.
