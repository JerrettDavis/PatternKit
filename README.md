# PatternKit

> **Fluent Design Patterns for Modern .NET**  
> Fluent APIs and incremental source generators for classic patterns in modern .NET.


[![CI](https://github.com/JerrettDavis/PatternKit/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/PatternKit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JerrettDavis/PatternKit/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/JerrettDavis/PatternKit/security/code-scanning)
[![codecov](https://codecov.io/gh/JerrettDavis/PatternKit/graph/badge.svg?token=I0LO3HLUTP)](https://codecov.io/gh/JerrettDavis/PatternKit)
[![License](https://img.shields.io/badge/license-MIT-green.svg?style=flat-square)](LICENSE)

---

## Overview

**PatternKit** provides fluent runtime helpers and Roslyn incremental source generators for GoF-style design patterns in .NET.
Instead of repeating handwritten pattern scaffolding, it favors:

- **Fluent builders** and DSLs (chainable, declarative, composable).
- **Source generators** that emit deterministic code at compile time.
- **AOT-friendly generated code** where pattern shape is known at design time.
- **Strong typing** with validation diagnostics from analyzers and generator tests.
- **Testable, deterministic APIs** that pair naturally with BDD and xUnit/NUnit/MSTest.

The goal is to make patterns clear to read, straightforward to test, and inexpensive to maintain.

---

## Quick Start

Install via NuGet:

```bash
dotnet add package PatternKit --version <latest>
```

Use a pattern immediately—here’s a simple **Strategy**:

```csharp
using PatternKit.Behavioral.Strategy;

var classify = Strategy<int, string>.Create()
    .When(i => i > 0).Then(i => "positive")
    .When(i => i < 0).Then(i => "negative")
    .Default(_ => "zero")
    .Build();

Console.WriteLine(classify.Execute(5));   // positive
Console.WriteLine(classify.Execute(-3));  // negative
Console.WriteLine(classify.Execute(0));   // zero
```

Or a **TryStrategy** for first-match-wins pipelines:

```csharp
var parse = TryStrategy<string, int>.Create()
    .Always((in string s, out int r) => int.TryParse(s, out r))
    .Finally((in string _, out int r) => { r = 0; return true; })
    .Build();

if (parse.Execute("123", out var n))
    Console.WriteLine(n); // 123
```

A forkable, lookahead **ReplayableSequence** (Iterator+):

```csharp
using PatternKit.Behavioral.Iterator;

var seq = ReplayableSequence<int>.From(Enumerable.Range(1, 5));
var c = seq.GetCursor();

// Look ahead without consuming
var first = c.Lookahead(0).OrDefault(); // 1
var third = c.Lookahead(2).OrDefault(); // 3

// Consume immutably (returns next cursor)
if (c.TryNext(out var v1, out var c2) && c2.TryNext(out var v2, out var c3))
{
    // v1 = 1, v2 = 2; c3 now points at 3
}

// Fork speculative branch
var fork = c3.Fork();
if (fork.TryNext(out var v3, out fork) && fork.TryNext(out var v4, out fork))
{
    // fork saw 3,4 while c3 is still at 3
}

// LINQ over a cursor (non-destructive to original cursor)
var evens = c3.Where(x => x % 2 == 0).ToList(); // [2,4] using a snapshot enumeration
```

### WindowSequence (sliding / striding windows)
```csharp
using PatternKit.Behavioral.Iterator;

// Full sliding windows (size 3, stride 1)
var slides = Enumerable.Range(1,7)
    .Windows(size:3)
    .Select(w => string.Join(',', w.ToArray()))
    .ToList(); // ["1,2,3","2,3,4","3,4,5","4,5,6","5,6,7"]

// Stride 2 (skip one between window starts)
var stride = Enumerable.Range(1,9)
    .Windows(size:4, stride:2)
    .Select(w => string.Join('-', w.ToArray()))
    .ToList(); // ["1-2-3-4","3-4-5-6","5-6-7-8"]

// Include trailing partial
var partial = new[]{1,2,3,4,5}
    .Windows(size:3, stride:3, includePartial:true)
    .Select(w => (Vals: w.ToArray(), w.IsPartial))
    .ToList(); // [ ([1,2,3], false), ([4,5], true) ]

// Reuse buffer (zero alloc per full window) – copy if you persist
var reused = Enumerable.Range(1,6)
    .Windows(size:3, reuseBuffer:true)
    .Select(w => w.ToArray()) // snapshot copy each window
    .ToList();
```

---

## Pattern Quick Reference
Tiny, copy‑paste friendly snippets for the most common patterns. Each builds an immutable, hot‑path friendly artifact.

### ActionChain (middleware style rule pack)
```csharp
using PatternKit.Behavioral.Chain;

var log = new List<string>();
var chain = ActionChain<HttpRequest>.Create()
    .When((in r) => r.Path.StartsWith("/admin") && !r.Headers.ContainsKey("Authorization"))
    .ThenStop(r => log.Add("deny"))
    .When((in r) => r.Headers.ContainsKey("X-Request-Id"))
    .ThenContinue(r => log.Add($"req={r.Headers["X-Request-Id"]}"))
    .Finally((in r, next) => { log.Add($"{r.Method} {r.Path}"); next(in r); })
    .Build();

chain.Execute(new("GET","/health", new Dictionary<string,string>()));
```

### ResultChain (first-match value producer with fallback)
```csharp
using PatternKit.Behavioral.Chain;

public readonly record struct Request(string Method, string Path);
public readonly record struct Response(int Status, string Body);

var router = ResultChain<Request, Response>.Create()
    .When(static (in r) => r.Method == "GET" && r.Path == "/health")
        .Then(r => new Response(200, "OK"))
    .When(static (in r) => r.Method == "GET" && r.Path.StartsWith("/users/"))
        .Then(r => new Response(200, $"user:{r.Path[7..]}"))
    // default / not found tail (only runs if no earlier handler produced)
    .Finally(static (in _, out Response? res, _) => { res = new Response(404, "not found"); return true; })
    .Build();

router.Execute(new Request("GET", "/health"), out var ok);   // ok.Status = 200
router.Execute(new Request("GET", "/users/alice"), out var u); // 200, Body = user:alice
router.Execute(new Request("GET", "/missing"), out var nf);    // 404, Body = not found
```

### Strategy (first matching branch)
```csharp
using PatternKit.Behavioral.Strategy;
var classify = Strategy<int,string>.Create()
    .When(i => i > 0).Then(_ => "positive")
    .When(i => i < 0).Then(_ => "negative")
    .Default(_ => "zero")
    .Build();
var result = classify.Execute(-5); // negative
```

### TryStrategy (first success wins parsing)
```csharp
var parse = TryStrategy<string,int>.Create()
    .Always((in string s, out int v) => int.TryParse(s, out v))
    .Finally((in string _, out int v) => { v = 0; return true; })
    .Build();
parse.Execute("42", out var n); // n=42
```

### ActionStrategy (multi-fire side‑effects)
```csharp
using PatternKit.Behavioral.Strategy;
var audit = new List<string>();
var strat = ActionStrategy<int>.Create()
    .When(i => i % 2 == 0).Then(i => audit.Add($"even:{i}"))
    .When(i => i > 10).Then(i => audit.Add($"big:{i}"))
    .Build();
strat.Execute(12); // adds even:12, big:12
```

### AsyncStrategy (await external work)
```csharp
var asyncStrat = AsyncStrategy<string,string>.Create()
    .When(s => s.StartsWith("http"))
    .Then(async s => await Task.FromResult("url"))
    .Default(async s => await Task.FromResult("text"))
    .Build();
var kind = await asyncStrat.Execute("http://localhost");
```

### BranchBuilder (first-match router)
```csharp
using PatternKit.Creational.Builder;

// Define delegate shapes (predicates take `in` param for struct-friendliness)
public delegate bool IntPred(in int x);
public delegate string IntHandler(in int x);

var router = BranchBuilder<IntPred, IntHandler>.Create()
    .Add(static (in int v) => v < 0,   static (in int v) => "neg")
    .Add(static (in int v) => v > 0,   static (in int v) => "pos")
    .Default(static (in int _) => "zero")
    .Build(
        fallbackDefault: static (in int _) => "zero",
        projector: static (preds, handlers, hasDefault, def) =>
        {
            // Project into a single dispatch function
            return (Func<int, string>)(x =>
            {
                for (int i = 0; i < preds.Length; i++)
                    if (preds[i](in x))
                        return handlers[i](in x);
                return def(in x);
            });
        });

var a = router(-5); // "neg"
var b = router(10); // "pos"
var c = router(0);  // "zero"
```

### ChainBuilder (collect -> project)
```csharp
using PatternKit.Creational.Builder;

var log = new List<string>();
var pipeline = ChainBuilder<Action<string>>.Create()
    .Add(static s => log.Add($"A:{s}"))
    .AddIf(true, static s => log.Add($"B:{s}"))
    .Add(static s => log.Add($"C:{s}"))
    .Build(actions => (Action<string>)(msg =>
    {
        foreach (var a in actions) a(msg);
    }));

pipeline("run");
// log => ["A:run", "B:run", "C:run"]
```

### Composer (functional state accumulation)
```csharp
using PatternKit.Creational.Builder;

public readonly record struct PersonState(string? Name, int Age);
public sealed record Person(string Name, int Age);

var person = Composer<PersonState, Person>
    .New(static () => default)
    .With(static s => s with { Name = "Ada" })
    .With(static s => s with { Age = 30 })
    .Require(static s => string.IsNullOrWhiteSpace(s.Name) ? "Name required" : null)
    .Build(static s => new Person(s.Name!, s.Age));
```

### MutableBuilder (imperative mutations + validation)
```csharp
using PatternKit.Creational.Builder;

public sealed class Options 
{
    public string? Host { get; set; } 
    public int Port { get; set; }
}

var opts = MutableBuilder<Options>.New(static () => new Options())
    .With(static o => o.Host = "localhost")
    .With(static o => o.Port = 8080)
    .RequireNotEmpty(static o => o.Host, nameof(Options.Host))
    .RequireRange(static o => o.Port, 1, 65535, nameof(Options.Port))
    .Build();
```

### Prototype (clone + mutate)
```csharp
using PatternKit.Creational.Prototype;

public sealed class User { public string Role { get; set; } = "user"; public bool Active { get; set; } = true; }

// Single prototype
var proto = Prototype<User>.Create(
        source: new User { Role = "user", Active = true },
        cloner: static (in User u) => new User { Role = u.Role, Active = u.Active })
    .With(static u => u.Active = false) // default mutation applied to every clone
    .Build();

var admin = proto.Create(u => u.Role = "admin"); // clone + extra mutation

// Registry of prototypes
var registry = Prototype<string, User>.Create()
    .Map("basic", new User { Role = "user", Active = true }, static (in User u) => new User { Role = u.Role, Active = u.Active })
    .Map("admin", new User { Role = "admin", Active = true }, static (in User u) => new User { Role = u.Role, Active = u.Active })
    .Mutate("admin", static u => u.Active = true) // append mutation to admin family
    .Default(new User { Role = "guest", Active = false }, static (in User u) => new User { Role = u.Role, Active = u.Active })
    .Build();

var guest = registry.Create("missing-key"); // falls back to default (guest)
```

### Decorator (fluent wrapping & extension)
```csharp
using PatternKit.Structural.Decorator;

// Add logging to any operation
var calculator = Decorator<int, int>.Create(static x => x * x)
    .Around((x, next) => {
        Console.WriteLine($"Input: {x}");
        var result = next(x);
        Console.WriteLine($"Output: {result}");
        return result;
    })
    .Build();

var squared = calculator.Execute(7); // Logs: Input: 7, Output: 49

// Add caching
var cache = new Dictionary<int, int>();
var cachedOp = Decorator<int, int>.Create(x => ExpensiveComputation(x))
    .Around((x, next) => {
        if (cache.TryGetValue(x, out var cached))
            return cached;
        var result = next(x);
        cache[x] = result;
        return result;
    })
    .Build();

// Chain multiple decorators: validation + transformation
var validated = Decorator<int, int>.Create(static x => 100 / x)
    .Before(static x => x == 0 ? throw new ArgumentException("Cannot be zero") : x)
    .After(static (input, result) => result + input)
    .Build();

var output = validated.Execute(5); // (100 / 5) + 5 = 25
```

### Facade (unified subsystem interface)
```csharp
using PatternKit.Structural.Facade;

// Simplify complex e-commerce operations
public record OrderRequest(string ProductId, int Quantity, string CustomerEmail, decimal Price);
public record OrderResult(bool Success, string? OrderId = null, string? ErrorMessage = null);

var orderFacade = Facade<OrderRequest, OrderResult>.Create()
    .Operation("place-order", (in OrderRequest req) => {
        // Coordinate inventory, payment, shipping, notifications
        var reservationId = inventoryService.Reserve(req.ProductId, req.Quantity);
        var txId = paymentService.Charge(req.Price * req.Quantity);
        var shipmentId = shippingService.Schedule(req.CustomerEmail);
        notificationService.SendConfirmation(req.CustomerEmail);
        
        return new OrderResult(true, OrderId: Guid.NewGuid().ToString());
    })
    .Operation("cancel-order", (in OrderRequest req) => {
        inventoryService.Release(req.ProductId);
        paymentService.Refund(req.ProductId);
        return new OrderResult(true);
    })
    .Default((in OrderRequest _) => new OrderResult(false, ErrorMessage: "Unknown operation"))
    .Build();

// Simple client code - complex subsystem coordination hidden
var result = orderFacade.Execute("place-order", orderRequest);

// Case-insensitive operations
var apiFacade = Facade<string, string>.Create()
    .OperationIgnoreCase("Status", (in string _) => "System OK")
    .OperationIgnoreCase("Version", (in string _) => "v2.0")
    .Build();

var status = apiFacade.Execute("STATUS", ""); // Works with any casing
```

### Proxy (access control & lazy initialization)
```csharp
using PatternKit.Structural.Proxy;

// Virtual Proxy - lazy initialization
var dbProxy = Proxy<string, string>.Create()
    .VirtualProxy(() => {
        var db = new ExpensiveDatabase("connection-string");
        return sql => db.Query(sql);
    })
    .Build();
// Database not created until first Execute call
var result = dbProxy.Execute("SELECT * FROM Users");

// Protection Proxy - access control
var deleteProxy = Proxy<User, bool>.Create(user => DeleteUser(user))
    .ProtectionProxy(user => user.IsAdmin)
    .Build();
deleteProxy.Execute(regularUser); // Throws UnauthorizedAccessException

// Caching Proxy - memoization
var cachedCalc = Proxy<int, int>.Create(x => ExpensiveFibonacci(x))
    .CachingProxy()
    .Build();
cachedCalc.Execute(100); // Calculates
cachedCalc.Execute(100); // Returns cached result

// Logging Proxy - audit trail
var loggedOp = Proxy<Payment, bool>.Create(p => ProcessPayment(p))
    .LoggingProxy(msg => logger.Log(msg))
    .Build();

// Custom Interception - retry logic
var retryProxy = Proxy<string, string>.Create(CallUnreliableService)
    .Intercept((input, next) => {
        for (int i = 0; i < 3; i++) {
            try { return next(input); }
            catch (Exception) when (i < 2) { Thread.Sleep(1000); }
        }
        throw new Exception("Max retries exceeded");
    })
    .Build();

// Remote Proxy - combine caching + logging
var remoteProxy = Proxy<int, string>.Create(id => CallRemoteApi(id))
    .Intercept((id, next) => {
        logger.Log($"Request for ID: {id}");
        var result = next(id);
        logger.Log("Response received");
        return result;
    })
    .Build();
var cachedRemoteProxy = Proxy<int, string>.Create(id => remoteProxy.Execute(id))
    .CachingProxy()
    .Build();
```

---

## Patterns Table
PatternKit currently tracks 88 production-readiness patterns. Each catalog pattern is represented in tests, documentation, real-world examples, IoC integration, and the BenchmarkDotNet coverage matrix.

| Category | Count | Patterns |
| --- | ---: | --- |
| Application Architecture | 15 | Anti-Corruption Layer, Audit Log, CQRS, Data Mapper, Domain Event, Event Sourcing, Feature Toggle, Identity Map, Materialized View, Repository, Service Layer, Specification, Table Data Gateway, Transaction Script, Unit of Work |
| Behavioral | 11 | Chain of Responsibility, Command, Interpreter, Iterator, Mediator, Memento, Observer, State, Strategy, Template Method, Visitor |
| Cloud Architecture | 17 | Ambassador, Backends for Frontends, Bulkhead, Cache-Aside, Circuit Breaker, External Configuration Store, Gateway Aggregation, Gateway Routing, Health Endpoint Monitoring, Leader Election, Priority Queue, Queue-Based Load Leveling, Rate Limiting, Retry, Scheduler Agent Supervisor, Sidecar, Strangler Fig |
| Creational | 5 | Abstract Factory, Builder, Factory Method, Prototype, Singleton |
| Enterprise Integration | 30 | Aggregator, Canonical Data Model, Channel Adapter, Claim Check, Competing Consumers, Content-Based Router, Control Bus, Dead Letter Channel, Event Notification, Event-Carried State Transfer, Event-Driven Consumer, Mailbox, Message Channel, Message Envelope, Message Filter, Message Store, Message Translator, Messaging Gateway, Pipes and Filters, Polling Consumer, Publish-Subscribe, Recipient List, Request-Reply, Resequencer, Routing Slip, Saga / Process Manager, Scatter-Gather, Service Activator, Splitter, Wire Tap |
| Messaging Reliability | 3 | Idempotent Receiver, Inbox, Outbox |
| Structural | 7 | Adapter, Bridge, Composite, Decorator, Facade, Flyweight, Proxy |

## Benchmark Snapshot

BenchmarkDotNet guidance is documented in [docs/guides/benchmarks.md](docs/guides/benchmarks.md), and the full published result matrix is in [docs/guides/benchmark-results.md](docs/guides/benchmark-results.md). This snapshot was captured on Windows 11, Intel Core i9-14900K, .NET SDK 10.0.108, .NET 10.0.8, BenchmarkDotNet 0.15.8, using the `current-tfm` job.

| Pattern | Phase | Fluent mean | Fluent allocation | Generated mean | Generated allocation | Read |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Ambassador | Construction | 55.42 ns | 448 B | 48.03 ns | 360 B | Generated reduced construction time and allocation in this microbenchmark. |
| Ambassador | Execution | 87.92 ns | 624 B | 93.72 ns | 624 B | Same allocation; fluent was slightly faster in this path. |
| Cache-Aside | Construction | 19.91 ns | 200 B | 19.85 ns | 200 B | Effectively equivalent for this microbenchmark. |
| Cache-Aside | Execution | 216.50 ns | 1,048 B | 208.60 ns | 1,048 B | Same allocation; generated was slightly faster for the miss-then-hit workflow. |
| Leader Election | Construction | 14.28 ns | 104 B | 15.91 ns | 104 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Leader Election | Execution | 43.62 ns | 360 B | 144.37 ns | 312 B | Generated allocated about 13% less memory, while fluent was faster in this path. |
| Message Routing | Construction | 23.42 ns | 224 B | 23.33 ns | 224 B | Effectively equivalent for this microbenchmark. |
| Message Routing | Execution | 707.34 ns | 4,744 B | 679.97 ns | 4,632 B | Generated reduced execution time and allocation for the route/split/aggregate workflow. |
| Message Translator | Construction | 39.49 ns | 424 B | 39.65 ns | 424 B | Effectively equivalent for this microbenchmark. |
| Message Translator | Execution | 365.30 ns | 2,528 B | 381.79 ns | 2,528 B | Same allocation; fluent was slightly faster in this path. |
| Reliability Pipeline | Construction | 34.90 ns | 392 B | 33.16 ns | 328 B | Generated reduced construction time and allocation in this microbenchmark. |
| Reliability Pipeline | Execution | 2.303 us | 3,992 B | 381.36 ns | 1,872 B | Generated was materially faster and allocated less for duplicate inbox processing plus outbox dispatch. |
| Retry | Construction | 25.36 ns | 208 B | 27.18 ns | 208 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Retry | Execution | 110.53 ns | 600 B | 109.52 ns | 600 B | Same allocation; generated was slightly faster for the transient retry workflow. |
| Scheduler Agent Supervisor | Construction | 47.29 ns | 400 B | 45.40 ns | 400 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Scheduler Agent Supervisor | Execution | 177.46 ns | 1,304 B | 180.14 ns | 1,304 B | Effectively equivalent for this scenario. |
| Service Activator | Construction | 4.825 ns | 32 B | 4.641 ns | 32 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Service Activator | Execution | 25.48 ns | 256 B | 26.49 ns | 256 B | Same allocation; fluent was slightly faster in this path. |

Run the benchmarks on target hardware before making final route decisions:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *LeaderElection* --artifacts artifacts/benchmarks --join
```
