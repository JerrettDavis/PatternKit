# MiniRouter — a tiny, composable API gateway/router

> **TL;DR**
> `MiniRouter` shows how three PatternKit primitives compose into a pragmatic HTTP-ish pipeline:
>
> * **Middleware** (side-effects, first-match-wins) — built with [xref\:PatternKit.Behavioral.Strategy.ActionStrategy\`1](xref:PatternKit.Behavioral.Strategy.ActionStrategy`1)
> * **Routes** (return a response, first-match-wins) — built with [xref\:PatternKit.Behavioral.Strategy.Strategy\`2](xref:PatternKit.Behavioral.Strategy.Strategy`2)
> * **Content negotiation** (try handlers until one succeeds) — built with [xref\:PatternKit.Behavioral.Strategy.TryStrategy\`2](xref:PatternKit.Behavioral.Strategy.TryStrategy`2)

This sample lives in `PatternKit.Examples.ApiGateway` and is intentionally tiny so you can lift it into tests, demos, or “just-enough” services.

---

## What it is (and isn’t)

**MiniRouter** is not a web framework. It’s a **pure-function** pipeline you can exercise from unit tests or swap behind your existing transport (ASP.NET, Minimal APIs, Lambda, etc.). It demonstrates:

* A **first-match-wins** mindset across middleware and routing
* **Short-circuit** behavior (e.g., auth checks) without exceptions
* **Allocation-light** execution with by-`in` parameters and static lambdas
* Clean separation of **effects** (middleware) and **results** (routes)

---

## The primitives

### Request/Response

* [xref\:PatternKit.Examples.ApiGateway.Request](xref:PatternKit.Examples.ApiGateway.Request) is a tiny immutable input: `Method`, `Path`, `Headers`, `Body?`
* [xref\:PatternKit.Examples.ApiGateway.Response](xref:PatternKit.Examples.ApiGateway.Response) is an immutable output: `StatusCode`, `ContentType`, `Body`
* [xref\:PatternKit.Examples.ApiGateway.Responses](xref:PatternKit.Examples.ApiGateway.Responses) holds helpers: `Text`, `Json`, `NotFound`, `Unauthorized`

### Router

* [xref\:PatternKit.Examples.ApiGateway.MiniRouter](xref:PatternKit.Examples.ApiGateway.MiniRouter) composes three strategies:

    * `_middleware : ActionStrategy<Request>` — **fire-and-forget** side effects (logging, metrics, auth messages)
    * `_routes : Strategy<Request, Response>` — **produce** a `Response`
    * `_negotiate : TryStrategy<Request, string>` — **select** a `Content-Type` if a route left it blank

---

## Building a router

Use the fluent builder:

```csharp
var router = MiniRouter.Create()
    // --- middleware (first-match-wins) ---
    .Use(
        static (in r) => r.Headers.ContainsKey("X-Request-Id"),
        static (in r) => Console.WriteLine($"reqid={r.Headers["X-Request-Id"]}"))
    .Use(
        static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal) &&
                         !r.Headers.ContainsKey("Authorization"),
        static (in _) => Console.WriteLine("Denied: missing Authorization"))

    // --- routes (first-match-wins) ---
    .Map(
        static (in r) => r is { Method: "GET", Path: "/health" },
        static (in _) => Responses.Text(200, "OK"))
    .Map(
        static (in r) => r.Method == "GET" && r.Path.StartsWith("/users/", StringComparison.Ordinal),
        static (in r) =>
        {
            var idStr = r.Path["/users/".Length..];
            return int.TryParse(idStr, out var id)
                ? Responses.Json(200, $"{{\"id\":{id},\"name\":\"user{id}\"}}")
                : Responses.Text(404, "User not found");
        })
    .Map(
        static (in r) => r is { Method: "POST", Path: "/users" },
        static (in _) => Responses.Json(201, "{\"ok\":true}"))

    // auth route fallback (denied)
    .Map(
        static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal) &&
                         !r.Headers.ContainsKey("Authorization"),
        static (in _) => Responses.Unauthorized())

    // default
    .NotFound(static (in _) => Responses.NotFound())
    .Build();
```

**Notes**

* **Middleware** uses [xref\:PatternKit.Behavioral.Strategy.ActionStrategy\`1](xref:PatternKit.Behavioral.Strategy.ActionStrategy`1): *first* matching action runs; others are skipped.
* **Routes** use [xref\:PatternKit.Behavioral.Strategy.Strategy\`2](xref:PatternKit.Behavioral.Strategy.Strategy`2): *first* matching handler returns a `Response`.
* A **default route** is set via `.NotFound(...)`.
* The builder sets a **noop default middleware** so `.Handle` never throws due to “no middleware.”

---

## Content negotiation

If a route returns an empty `ContentType`, **MiniRouter** will ask the negotiator to supply one. The **default negotiator**:

1. If `Accept: application/json` → `application/json; charset=utf-8`
2. Else if `Accept: text/plain` → `text/plain; charset=utf-8`
3. Else → default to JSON

You can provide your own:

```csharp
var neg = TryStrategy<Request, string>.Create()
    .Always(static (in r, out string? ct) =>
    {
        if (r.Headers.TryGetValue("Accept", out var a) && a.Contains("application/xml"))
        { ct = "application/xml; charset=utf-8"; return true; }
        ct = null; return false;
    })
    .Finally(static (in _, out string? ct) => { ct = "application/json; charset=utf-8"; return true; })
    .Build();

var router = MiniRouter.Create()
    // ... Use/Map/NotFound ...
    .WithNegotiator(neg)
    .Build();
```

---

## Demo walkthrough

`Demo.Run()` wires the router, then simulates requests:

```csharp
Print(router.Handle(new Request("GET", "/health", commonHeaders)));
Print(router.Handle(new Request("GET", "/users/42", commonHeaders)));
Print(router.Handle(new Request("GET", "/users/abc", commonHeaders)));
Print(router.Handle(new Request("GET", "/admin/metrics", new Dictionary<string, string>()))); // unauthorized
Print(router.Handle(new Request("POST", "/users", commonHeaders, "{\"name\":\"Ada\"}")));
Print(router.Handle(new Request("GET", "/nope", commonHeaders)));
```

**Illustrative output** (order matters—note the middleware log before the 401):

```
200 text/plain; charset=utf-8
OK

200 application/json; charset=utf-8
{"id":42,"name":"user42"}

404 text/plain; charset=utf-8
User not found

Denied: missing Authorization
401 text/plain; charset=utf-8
Unauthorized

201 application/json; charset=utf-8
{"ok":true}

404 text/plain; charset=utf-8
Not Found
```

---

## Why three strategies?

| Concern     | Type                                                                                                           | Behavior                                    | Why here                                      |
| ----------- | -------------------------------------------------------------------------------------------------------------- | ------------------------------------------- | --------------------------------------------- |
| Middleware  | [xref\:PatternKit.Behavioral.Strategy.ActionStrategy\`1](xref:PatternKit.Behavioral.Strategy.ActionStrategy`1) | First matching **action** runs (no return). | Logging, metrics, auth *messages*, CORS, etc. |
| Routing     | [xref\:PatternKit.Behavioral.Strategy.Strategy\`2](xref:PatternKit.Behavioral.Strategy.Strategy`2)             | First matching **handler** returns a value. | Pick a `Response` for the request.            |
| Negotiation | [xref\:PatternKit.Behavioral.Strategy.TryStrategy\`2](xref:PatternKit.Behavioral.Strategy.TryStrategy`2)       | Chain **try** handlers until one succeeds.  | Fill in `ContentType` if missing.             |

All three are allocation-light, immutable once built, and thread-safe to execute.

---

## Testing with TinyBDD

We ship BDD-style tests in `PatternKit.Examples.Tests.ApiGateway`.

### Health check

```csharp
await Given("a default router", DefaultRouter)
  .When("GET /health", r => r.Handle(new Request("GET", "/health", Headers())))
  .Then("status is 200", res => res.StatusCode == 200)
  .And("content-type is text/plain", res => res.ContentType.StartsWith("text/plain"))
  .AssertPassed();
```

### Middleware first-match wins

```csharp
var hits = new List<string>();

MiniRouter Build()
  => MiniRouter.Create()
      .Use(static (in r) => r.Path.StartsWith("/a"), (in _) => hits.Add("A"))
      .Use(static (in r) => r.Path.StartsWith("/a"), (in _) => hits.Add("B")) // also matches but won't run
      .Map(static (in _) => true, static (in _) => Responses.Text(200, "ok"))
      .NotFound(static (in _) => Responses.NotFound())
      .Build();

await Given("two matching middleware", Build)
  .When("GET /a", r => r.Handle(new Request("GET", "/a", Headers())))
  .Then("exactly one ran", _ => hits.Count == 1 && hits[0] == "A")
  .AssertPassed();
```

### Content negotiation

```csharp
await Given("negotiating router", NegotiatingRouter)
  .When("GET /neg with Accept: text/plain",
        r => r.Handle(new Request("GET", "/neg", Headers(accept: "text/plain"))))
  .Then("content-type is text/plain", res => res.ContentType.StartsWith("text/plain"))
  .AssertPassed();
```

---

## Extending MiniRouter

* **Add middleware**: `.Use(predicate, action)` — only the **first** matching action runs.
* **Add routes**: `.Map(predicate, handler)` — only the **first** matching handler returns.
* **Change NotFound**: `.NotFound(handler)` — default when nothing matches.
* **Swap negotiator**: `.WithNegotiator(tryStrategy)` — e.g., add `application/xml`.

**Tip:** Prefer **static method groups** or **static lambdas** for zero-capture delegates and better allocations.

---

## Performance notes

* By-`in` parameters on strategies avoid defensive copies for structs.
* Static lambdas (`static (in r) => ...`) prevent hidden captures/allocations.
* Immutable, pre-built pipelines are **thread-safe**; builders are not.

---

## Troubleshooting

* **Multiple middleware actions run**
  Ensure your conditions don’t both match earlier branches. **First match wins**; later branches are skipped only if an earlier branch matched.

* **Route not hit**
  Check earlier `.Map` conditions—an earlier, broader predicate may be capturing the request.

* **Missing content-type**
  If a handler returns `Response` with an empty `ContentType`, the negotiator fills it. Provide your own via `.WithNegotiator(...)` if defaults don’t suit.

---

## API reference

* [xref\:PatternKit.Examples.ApiGateway.MiniRouter](xref:PatternKit.Examples.ApiGateway.MiniRouter)

    * [xref\:PatternKit.Examples.ApiGateway.MiniRouter.Create\*](xref:PatternKit.Examples.ApiGateway.MiniRouter.Create*)
    * [xref\:PatternKit.Examples.ApiGateway.MiniRouter.Handle\*](xref:PatternKit.Examples.ApiGateway.MiniRouter.Handle*)
    * [xref\:PatternKit.Examples.ApiGateway.MiniRouter.Builder](xref:PatternKit.Examples.ApiGateway.MiniRouter.Builder)
* [xref\:PatternKit.Examples.ApiGateway.Request](xref:PatternKit.Examples.ApiGateway.Request) / [xref\:PatternKit.Examples.ApiGateway.Response](xref:PatternKit.Examples.ApiGateway.Response) / [xref\:PatternKit.Examples.ApiGateway.Responses](xref:PatternKit.Examples.ApiGateway.Responses)
* [xref\:PatternKit.Behavioral.Strategy.ActionStrategy\`1](xref:PatternKit.Behavioral.Strategy.ActionStrategy`1)
* [xref\:PatternKit.Behavioral.Strategy.Strategy\`2](xref:PatternKit.Behavioral.Strategy.Strategy`2)
* [xref\:PatternKit.Behavioral.Strategy.TryStrategy\`2](xref:PatternKit.Behavioral.Strategy.TryStrategy`2)

---

### Appendix: End-to-end demo

Run:

```csharp
PatternKit.Examples.ApiGateway.Demo.Run();
```

It prints the sequence described above (health, users/42, users/abc, admin unauthorized with a middleware log line first, POST /users, and 404 for /nope).
