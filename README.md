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

For first-party `IServiceCollection` integration in ASP.NET Core, worker services, or generic host apps:

```bash
dotnet add package PatternKit.Hosting.Extensions --version <latest>
```

```csharp
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Hosting.DependencyInjection;
using PatternKit.Messaging.Channels;

var services = new ServiceCollection();

services
    .AddPatternKitMessageChannel<OrderCommand>(
        "orders",
        channel => channel.WithCapacity(100, MessageChannelBackpressurePolicy.Reject))
    .AddPatternKitRetryPolicy<ServiceReply>(
        "inventory-retry",
        retry => retry.WithMaxAttempts(3).HandleResult(static reply => !reply.Available));

public sealed record OrderCommand(string OrderId, decimal Total);
public sealed record ServiceReply(bool Available);
```

See [Hosting Extensions](docs/guides/hosting-extensions.md) for reusable DI registrations that do not depend on the examples assembly.

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
PatternKit currently tracks 109 production-readiness patterns. Each catalog pattern is represented in tests, documentation, real-world examples, IoC integration, and the BenchmarkDotNet coverage matrix.

| Category | Count | Patterns |
| --- | ---: | --- |
| Application Architecture | 23 | Activity Tracker, Aggregate Root, Anti-Corruption Layer, Audit Log, Bounded Context, Context Map, CQRS, Data Mapper, Domain Event, Domain Service, Event Sourcing, Feature Toggle, Identity Map, Manual Task Gate, Materialized View, Repository, Service Layer, Specification, Table Data Gateway, Timeout Manager, Transaction Script, Unit of Work, Value Object |
| Behavioral | 11 | Chain of Responsibility, Command, Interpreter, Iterator, Mediator, Memento, Observer, State, Strategy, Template Method, Visitor |
| Cloud Architecture | 18 | Ambassador, Backends for Frontends, Bulkhead, Cache-Aside, Cache Stampede Protection, Circuit Breaker, External Configuration Store, Gateway Aggregation, Gateway Routing, Health Endpoint Monitoring, Leader Election, Priority Queue, Queue-Based Load Leveling, Rate Limiting, Retry, Scheduler Agent Supervisor, Sidecar, Strangler Fig |
| Creational | 5 | Abstract Factory, Builder, Factory Method, Prototype, Singleton |
| Enterprise Integration | 41 | Aggregator, Canonical Data Model, Channel Adapter, Channel Purger, Claim Check, Competing Consumers, Content Enricher, Content-Based Router, Control Bus, Correlation Identifier, Dead Letter Channel, Durable Subscriber, Dynamic Router, Event Notification, Event-Carried State Transfer, Event-Driven Consumer, Guaranteed Delivery, Invalid Message Channel, Mailbox, Message Bus, Message Channel, Message Envelope, Message Expiration, Message Filter, Message History, Message Store, Message Translator, Messaging Bridge, Messaging Gateway, Pipes and Filters, Polling Consumer, Publish-Subscribe, Recipient List, Request-Reply, Resequencer, Routing Slip, Saga / Process Manager, Scatter-Gather, Service Activator, Splitter, Wire Tap |
| Messaging Reliability | 3 | Idempotent Receiver, Inbox, Outbox |
| Structural | 7 | Adapter, Bridge, Composite, Decorator, Facade, Flyweight, Proxy |

## Benchmark Snapshot

BenchmarkDotNet guidance is documented in [docs/guides/benchmarks.md](docs/guides/benchmarks.md), and the full published result matrix is in [docs/guides/benchmark-results.md](docs/guides/benchmark-results.md). This snapshot was captured on Windows 11, Intel Core i9-14900K, .NET SDK 10.0.108, .NET 10.0.8, BenchmarkDotNet 0.15.8, using the `current-tfm` job.

| Pattern | Phase | Fluent mean | Fluent allocation | Generated mean | Generated allocation | Read |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Abstract Factory | Construction | 715.345 ns | 5,992 B | 720.811 ns | 5,992 B | Effectively equivalent for tenant widget factory composition. |
| Abstract Factory | Execution | 750.189 ns | 6,200 B | 735.733 ns | 6,200 B | Same allocation; generated was slightly faster for login widget creation. |
| Adapter | Construction | 34.668 ns | 320 B | 3.607 ns | 24 B | Generated adapter construction was materially faster and allocated less. |
| Adapter | Execution | 59.084 ns | 416 B | 20.479 ns | 80 B | Generated adapter execution was faster and allocated less for shipment adaptation. |
| Activity Tracker | Construction | 13.09 ns | 152 B | 12.98 ns | 152 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Activity Tracker | Execution | 446.88 ns | 1,656 B | 452.36 ns | 1,656 B | Same allocation; fluent was slightly faster for dashboard loading gates. |
| Manual Task Gate | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Manual Task Gate | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Timeout Manager | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Timeout Manager | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Aggregate Root | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Aggregate Root | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Aggregator | Construction | 14.562 ns | 168 B | 15.235 ns | 168 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Aggregator | Execution | 188.000 ns | 1,088 B | 200.564 ns | 1,088 B | Same allocation; fluent was faster for order line aggregation. |
| Ambassador | Construction | 55.42 ns | 448 B | 48.03 ns | 360 B | Generated reduced construction time and allocation in this microbenchmark. |
| Ambassador | Execution | 87.92 ns | 624 B | 93.72 ns | 624 B | Same allocation; fluent was slightly faster in this path. |
| Anti-Corruption Layer | Construction | 59.62 ns | 440 B | 59.77 ns | 440 B | Effectively equivalent for this microbenchmark. |
| Anti-Corruption Layer | Execution | 109.13 ns | 656 B | 107.42 ns | 656 B | Same allocation; generated was slightly faster for legacy order import. |
| Audit Log | Construction | 19.70 ns | 192 B | 18.62 ns | 192 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Audit Log | Execution | 265.81 ns | 968 B | 273.42 ns | 968 B | Same allocation; fluent was slightly faster for submit-and-approve audit logging. |
| Backends For Frontends | Construction | 58.00 ns | 512 B | 42.48 ns | 296 B | Generated reduced construction time and allocation in this microbenchmark. |
| Backends For Frontends | Execution | 25.40 ns | 216 B | 29.77 ns | 216 B | Same allocation; fluent was faster for the web summary shaping workflow. |
| Builder | Construction | 48.24 ns | 320 B | 160.455 us | 129,236 B | Fluent builder construction is much lighter; generated measures full HostApplicationBuilder composition. |
| Builder | Execution | 65.14 ns | 352 B | 500.676 us | 279,923 B | Fluent option building is much lighter; generated exercises the full host-backed application build. |
| Bridge | Construction | 53.115 ns | 504 B | 3.821 ns | 24 B | Generated bridge construction was materially faster and allocated less. |
| Bridge | Execution | 91.848 ns | 664 B | 30.004 ns | 160 B | Generated bridge forwarding was faster and allocated less for notice rendering. |
| Bulkhead | Construction | 20.56 ns | 216 B | 20.48 ns | 216 B | Effectively equivalent for this microbenchmark. |
| Bulkhead | Execution | 102.70 ns | 592 B | 106.11 ns | 592 B | Same allocation; fluent was slightly faster for the shipping allocation workflow. |
| Cache Stampede Protection | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Cache Stampede Protection | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Cache-Aside | Construction | 19.91 ns | 200 B | 19.85 ns | 200 B | Effectively equivalent for this microbenchmark. |
| Cache-Aside | Execution | 216.50 ns | 1,048 B | 208.60 ns | 1,048 B | Same allocation; generated was slightly faster for the miss-then-hit workflow. |
| Canonical Data Model | Construction | 75.482 ns | 632 B | 59.947 ns | 496 B | Generated reduced construction time and allocation in this microbenchmark. |
| Canonical Data Model | Execution | 116.680 ns | 832 B | 92.082 ns | 696 B | Generated reduced execution time and allocation for order normalization. |
| Channel Adapter | Construction | 38.469 ns | 384 B | 38.681 ns | 384 B | Effectively equivalent for this microbenchmark. |
| Channel Adapter | Execution | 204.907 ns | 888 B | 198.374 ns | 888 B | Same allocation; generated was slightly faster for the ERP order document round-trip workflow. |
| Channel Purger | Construction | 28.72 ns | 288 B | 16.66 ns | 168 B | Generated reduced construction time and allocation for the maintenance purger factory. |
| Channel Purger | Execution | 402.57 ns | 2,456 B | 269.76 ns | 1,680 B | Generated reduced execution time and allocation for the inventory backlog purge workflow. |
| Invalid Message Channel | Construction | 20.15 ns | 176 B | 19.76 ns | 176 B | Fluent and generated builder construction were effectively equivalent. |
| Invalid Message Channel | Execution | 428.21 ns | 2,680 B | 441.83 ns | 2,680 B | Fluent and generated invalid-message routing had equivalent allocation with minor timing variance. |
| Claim Check | Construction | 111.92 ns | 1,664 B | 110.71 ns | 1,664 B | Effectively equivalent for this microbenchmark. |
| Claim Check | Execution | 355.24 ns | 2,976 B | 342.12 ns | 2,976 B | Same allocation; generated was slightly faster for the large-document restore workflow. |
| Competing Consumers | Construction | 35.66 ns | 328 B | 35.40 ns | 328 B | Effectively equivalent for this microbenchmark. |
| Competing Consumers | Execution | 75.34 ns | 416 B | 75.23 ns | 416 B | Effectively equivalent for the fulfillment dispatch workflow. |
| Composite | Construction | 82.460 ns | 848 B | 30.685 ns | 216 B | Generated composite construction was faster and allocated less for the storage tree. |
| Composite | Execution | 90.010 ns | 848 B | 36.760 ns | 216 B | Generated composite traversal was faster and allocated less for size calculation. |
| Circuit Breaker | Construction | 14.33 ns | 128 B | 13.73 ns | 128 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Circuit Breaker | Execution | 85.34 ns | 488 B | 85.19 ns | 488 B | Effectively equivalent for the accepted fulfillment workflow. |
| Content-Based Router | Construction | 42.913 ns | 400 B | 44.118 ns | 400 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Content-Based Router | Execution | 52.789 ns | 520 B | 60.816 ns | 576 B | Fluent was faster and allocated less for wholesale order routing. |
| Content Enricher | Construction | 36.39 ns | 312 B | 36.22 ns | 312 B | Effectively equivalent for this microbenchmark. |
| Content Enricher | Execution | 1.357 us | 1,400 B | 1.344 us | 1,400 B | Same allocation; generated was slightly faster for customer profile enrichment. |
| Control Bus | Construction | 115.64 ns | 880 B | 79.88 ns | 624 B | Generated reduced construction time and allocation in this microbenchmark. |
| Control Bus | Execution | 290.44 ns | 1,688 B | 232.48 ns | 1,432 B | Generated reduced execution time and allocation for operational command dispatch. |
| CQRS | Construction | 199.3 ns | 2.09 KB | 88.001 us | 309.24 KB | Fluent mediator construction is much lighter; generated measurement includes full IServiceCollection dispatcher composition. |
| CQRS | Execution | 479.9 ns | 3.40 KB | 96.453 us | 324.83 KB | Fluent route is much lighter in this microbenchmark; generated route exercises the full DI-integrated dispatcher workflow. |
| Data Mapper | Construction | 40.56 ns | 288 B | 12.87 ns | 112 B | Generated reduced construction time and allocation in this microbenchmark. |
| Data Mapper | Execution | 188.09 ns | 1,104 B | 97.71 ns | 672 B | Generated reduced execution time and allocation for the map-store-load workflow. |
| Dead Letter Channel | Construction | 12.35 ns | 120 B | 12.50 ns | 120 B | Effectively equivalent for this microbenchmark. |
| Dead Letter Channel | Execution | 999.95 ns | 7,056 B | 1.023 us | 7,024 B | Generated allocated slightly less, while fluent was slightly faster for capture-and-replay preparation. |
| Durable Subscriber | Construction | 98.55 ns | 760 B | 81.45 ns | 616 B | Generated reduced construction time and allocation for checkpointed replay subscriber setup. |
| Durable Subscriber | Execution | 711.81 ns | 3,912 B | 605.65 ns | 3,128 B | Generated reduced catch-up projection replay time and allocation in this microbenchmark. |
| Dynamic Router | Construction | 213.4 ns | 1.29 KB | 214.0 ns | 1.29 KB | Fluent and generated route-table construction were effectively equivalent. |
| Dynamic Router | Execution | 406.7 ns | 2.83 KB | 415.2 ns | 2.83 KB | Same allocation; fluent was slightly faster for fulfillment route-table dispatch. |
| Message Bus | Construction | 186.7 ns | 1,264 B | 160.0 ns | 904 B | Generated reduced construction time and allocation for topic bus topology setup. |
| Message Bus | Execution | 503.8 ns | 3,192 B | 587.7 ns | 3,232 B | Fluent was faster and allocated slightly less for publishing order events to topic subscribers. |
| Messaging Bridge | Construction | 184.0 ns | 1,328 B | 185.5 ns | 1,328 B | Same allocation; fluent and generated bridge construction were effectively equivalent. |
| Messaging Bridge | Execution | 666.8 ns | 3,912 B | 670.8 ns | 3,912 B | Same allocation; fluent was slightly faster for partner order imports. |
| Correlation Identifier | Construction | 13.188 ns | 96 B | 6.133 ns | 48 B | Generated reduced construction time and allocation for correlation builder setup. |
| Correlation Identifier | Execution | 235.073 ns | 1,480 B | 224.406 ns | 1,432 B | Generated was slightly faster and allocated slightly less for order request/reply correlation. |
| Message History | Construction | 6.843 ns | 56 B | 6.075 ns | 56 B | Same allocation; generated was slightly faster for history recorder construction. |
| Message History | Execution | 275.298 ns | 1,400 B | 285.765 ns | 1,400 B | Same allocation; fluent was slightly faster for order history recording. |
| Decorator | Construction | 34.293 ns | 264 B | 17.669 ns | 168 B | Generated decorator composition was faster and allocated less. |
| Decorator | Execution | 60.765 ns | 384 B | 35.551 ns | 304 B | Generated decorator execution was faster and allocated less for decorated storage reads. |
| Domain Event | Construction | 199.5 ns | 1.34 KB | 157.6 ns | 1.04 KB | Generated reduced construction time and allocation in this microbenchmark. |
| Domain Event | Execution | 367.2 ns | 1.77 KB | 346.4 ns | 1.55 KB | Generated reduced execution time and allocation for the order-placed dispatch workflow. |
| Bounded Context | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Bounded Context | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Context Map | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Context Map | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Domain Service | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Domain Service | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Event-Carried State Transfer | Construction | 7.552 ns | 48 B | 6.751 ns | 48 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Event-Carried State Transfer | Execution | 58.508 ns | 448 B | 59.071 ns | 448 B | Effectively equivalent for the inventory projection workflow. |
| Event Notification | Construction | 30.920 ns | 232 B | 31.926 ns | 232 B | Effectively equivalent for this microbenchmark. |
| Event Notification | Execution | 93.209 ns | 704 B | 106.973 ns | 704 B | Same allocation; fluent was faster for order notification publishing. |
| Event Sourcing | Construction | 12.92 ns | 144 B | 13.00 ns | 144 B | Effectively equivalent for this microbenchmark. |
| Event Sourcing | Execution | 239.97 ns | 1,168 B | 245.13 ns | 1,168 B | Same allocation; fluent was slightly faster for place-and-pay event replay. |
| Event-Driven Consumer | Construction | 41.394 ns | 336 B | 25.216 ns | 192 B | Generated reduced construction time and allocation in this microbenchmark. |
| Event-Driven Consumer | Execution | 135.584 ns | 888 B | 122.305 ns | 688 B | Generated reduced execution time and allocation for the order-accepted event workflow. |
| External Configuration Store | Construction | 42.02 ns | 392 B | 41.06 ns | 328 B | Generated reduced construction time and allocation in this microbenchmark. |
| External Configuration Store | Execution | 34.94 ns | 48 B | 35.78 ns | 48 B | Same allocation; fluent was slightly faster for the cached tenant settings workflow. |
| Factory Method | Construction | 72.397 ns | 512 B | 0.355 ns | 0 B | Generated static factory surface resolution avoids runtime builder allocation. |
| Factory Method | Execution | 1.792 us | 10,568 B | 1.681 us | 10,056 B | Generated was faster and allocated less for service-module registration. |
| Facade | Construction | 59.132 ns | 624 B | 12.567 ns | 112 B | Generated facade construction was faster and allocated less for the shipping facade. |
| Facade | Execution | 100.182 ns | 664 B | 68.503 ns | 208 B | Generated facade execution was faster and allocated less for shipping quote calculation. |
| Feature Toggle | Construction | 84.97 ns | 496 B | 82.73 ns | 496 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Feature Toggle | Execution | 151.85 ns | 1,120 B | 150.87 ns | 1,120 B | Effectively equivalent for checkout feature evaluation. |
| Flyweight | Construction | 20.595 ns | 192 B | 23.842 ns | 272 B | Fluent construction was lighter; generated route includes LRU cache infrastructure. |
| Flyweight | Execution | 58.502 ns | 400 B | 109.987 ns | 704 B | Fluent was faster and allocated less in this LRU-vs-unbounded cache comparison. |
| Gateway Aggregation | Construction | 104.21 ns | 856 B | 64.99 ns | 560 B | Generated reduced construction time and allocation in this microbenchmark. |
| Gateway Aggregation | Execution | 109.55 ns | 632 B | 112.95 ns | 632 B | Same allocation; fluent was slightly faster for the dashboard aggregation workflow. |
| Gateway Routing | Construction | 56.20 ns | 464 B | 50.72 ns | 336 B | Generated reduced construction time and allocation in this microbenchmark. |
| Gateway Routing | Execution | 23.63 ns | 200 B | 20.13 ns | 168 B | Generated reduced execution time and allocation for the inventory routing workflow. |
| Health Endpoint Monitoring | Construction | 35.68 ns | 264 B | 35.25 ns | 264 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Health Endpoint Monitoring | Execution | 38.50 ns | 248 B | 31.67 ns | 248 B | Same allocation; generated was faster for the fulfillment health evaluation workflow. |
| Idempotent Receiver | Construction | 17.022 ns | 184 B | 17.021 ns | 184 B | Effectively equivalent for this microbenchmark. |
| Idempotent Receiver | Execution | 99.419 ns | 608 B | 99.051 ns | 608 B | Effectively equivalent for idempotent command handling. |
| Inbox | Construction | 22.259 ns | 208 B | 21.675 ns | 208 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Inbox | Execution | 113.126 ns | 632 B | 110.770 ns | 608 B | Generated reduced execution time and allocation for inbox command processing. |
| Identity Map | Construction | 10.62 ns | 112 B | 10.68 ns | 112 B | Effectively equivalent for this microbenchmark. |
| Identity Map | Execution | 108.91 ns | 968 B | 94.83 ns | 968 B | Same allocation; generated was faster for scoped identity-map reuse. |
| Leader Election | Construction | 14.28 ns | 104 B | 15.91 ns | 104 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Leader Election | Execution | 43.62 ns | 360 B | 144.37 ns | 312 B | Generated allocated about 13% less memory, while fluent was faster in this path. |
| Materialized View | Construction | 140.9 ns | 1.05 KB | 147.4 ns | 1.05 KB | Same allocation; fluent was slightly faster in this microbenchmark. |
| Materialized View | Execution | 389.5 ns | 2.02 KB | 386.0 ns | 2.02 KB | Effectively equivalent for this scenario. |
| Mailbox | Construction | 17.030 ns | 216 B | 29.867 ns | 360 B | Fluent was faster and allocated less for disposable mailbox construction. |
| Mailbox | Execution | 1.856 us | 2,620 B | 1.956 us | 2,474 B | Generated allocated less, while fluent was faster for serialized mailbox processing. |
| Message Channel | Construction | 10.282 ns | 120 B | 10.148 ns | 120 B | Effectively equivalent for this microbenchmark. |
| Message Channel | Execution | 72.921 ns | 512 B | 71.924 ns | 512 B | Same allocation; generated was slightly faster for the inventory adjustment workflow. |
| Message Envelope | Construction | 248.580 ns | 1,688 B | 228.019 ns | 1,688 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Message Envelope | Execution | 455.486 ns | 2,752 B | 427.664 ns | 2,752 B | Same allocation; generated was slightly faster for message context enrichment. |
| Message Filter | Construction | 25.431 ns | 232 B | 25.626 ns | 232 B | Effectively equivalent for this microbenchmark. |
| Message Filter | Execution | 44.637 ns | 424 B | 45.826 ns | 424 B | Same allocation; fluent was slightly faster for order fraud screening. |
| Message Expiration | Construction | 22.55 ns | 248 B | 14.63 ns | 144 B | Generated policy construction reduced time and allocation. |
| Message Expiration | Execution | 88.54 ns | 728 B | 103.86 ns | 536 B | Generated reduced allocation; fluent was faster for the stamp-and-evaluate flow. |
| Guaranteed Delivery | Construction | 22.778 ns | 200 B | 22.522 ns | 200 B | Fluent and generated queue construction were effectively equivalent. |
| Guaranteed Delivery | Execution | 175.488 ns | 752 B | 167.434 ns | 752 B | Generated was slightly faster with the same allocation for enqueue, receive, and acknowledge. |
| Message Routing | Construction | 23.42 ns | 224 B | 23.33 ns | 224 B | Effectively equivalent for this microbenchmark. |
| Message Routing | Execution | 707.34 ns | 4,744 B | 679.97 ns | 4,632 B | Generated reduced execution time and allocation for the route/split/aggregate workflow. |
| Message Store | Construction | 18.824 ns | 216 B | 18.721 ns | 216 B | Effectively equivalent for this microbenchmark. |
| Message Store | Execution | 274.799 ns | 1,576 B | 265.470 ns | 1,576 B | Same allocation; generated was slightly faster for record-and-replay lookup. |
| Message Translator | Construction | 39.49 ns | 424 B | 39.65 ns | 424 B | Effectively equivalent for this microbenchmark. |
| Message Translator | Execution | 365.30 ns | 2,528 B | 381.79 ns | 2,528 B | Same allocation; fluent was slightly faster in this path. |
| Messaging Gateway | Construction | 14.094 ns | 160 B | 14.167 ns | 160 B | Effectively equivalent for this microbenchmark. |
| Messaging Gateway | Execution | 66.597 ns | 560 B | 67.558 ns | 560 B | Same allocation; fluent was slightly faster for payment authorization. |
| Outbox | Construction | 9.544 ns | 88 B | 9.672 ns | 88 B | Effectively equivalent for this microbenchmark. |
| Outbox | Execution | 118.307 ns | 424 B | 122.972 ns | 424 B | Same allocation; fluent was slightly faster for enqueue-and-dispatch processing. |
| Pipes and Filters | Construction | 32.99 ns | 264 B | 32.98 ns | 264 B | Effectively equivalent for this microbenchmark. |
| Pipes and Filters | Execution | 138.66 ns | 800 B | 137.18 ns | 800 B | Same allocation; generated was slightly faster for the fulfillment pipeline workflow. |
| Polling Consumer | Construction | 35.577 ns | 328 B | 4.367 ns | 32 B | Generated materially reduced construction time and allocation in this microbenchmark. |
| Polling Consumer | Execution | 52.658 ns | 384 B | 15.783 ns | 96 B | Generated reduced execution time and allocation for the replenishment polling workflow. |
| Prototype | Construction | 886.538 ns | 6,888 B | 20.985 ns | 152 B | Generated clone source construction is much lighter than populating the fluent registry. |
| Prototype | Execution | 1.193 us | 7,832 B | 21.717 ns | 152 B | Generated cloning was materially faster and allocated less for the character prototype. |
| Proxy | Construction | 9.099 ns | 88 B | 6.037 ns | 48 B | Generated proxy construction was faster and allocated less. |
| Proxy | Execution | 20.148 ns | 120 B | 3.116 ns | 0 B | Generated proxy execution avoided allocation and was materially faster for price calculation. |
| Publish-Subscribe | Construction | 156.89 ns | 1,616 B | 153.49 ns | 1,616 B | Same allocation; generated was slightly faster for topology composition. |
| Publish-Subscribe | Execution | 6.380 us | 10,386 B | 6.275 us | 10,417 B | Generated was slightly faster; fluent allocated slightly less for two-subscriber dispatch. |
| Reliability Pipeline | Construction | 34.90 ns | 392 B | 33.16 ns | 328 B | Generated reduced construction time and allocation in this microbenchmark. |
| Reliability Pipeline | Execution | 2.303 us | 3,992 B | 381.36 ns | 1,872 B | Generated was materially faster and allocated less for duplicate inbox processing plus outbox dispatch. |
| Recipient List | Construction | 30.304 ns | 288 B | 30.380 ns | 288 B | Effectively equivalent for this microbenchmark. |
| Recipient List | Execution | 123.057 ns | 992 B | 120.325 ns | 968 B | Generated was slightly faster and allocated slightly less for shipment fan-out. |
| Request-Reply | Construction | 79.15 ns | 672 B | 78.63 ns | 672 B | Effectively equivalent for request/reply topology composition. |
| Request-Reply | Execution | 11.196 us | 13,492 B | 10.864 us | 13,493 B | Generated was slightly faster; allocations were effectively equivalent for the request/reply exchange. |
| Repository | Construction | 9.793 ns | 112 B | 9.239 ns | 112 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Repository | Execution | 146.37 ns | 888 B | 143.27 ns | 888 B | Same allocation; generated was slightly faster for the seed-and-query workflow. |
| Resequencer | Construction | 16.89 ns | 192 B | 17.27 ns | 192 B | Effectively equivalent for this microbenchmark. |
| Resequencer | Execution | 311.90 ns | 2,456 B | 303.94 ns | 2,456 B | Same allocation; generated was slightly faster for the three-event shipment resequencing workflow. |
| Routing Slip | Construction | 35.508 ns | 256 B | 32.448 ns | 256 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Routing Slip | Execution | 515.138 ns | 3,504 B | 507.633 ns | 3,568 B | Generated was slightly faster; fluent allocated slightly less for the fulfillment itinerary. |
| Saga / Process Manager | Construction | 39.453 ns | 272 B | 42.138 ns | 272 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Saga / Process Manager | Execution | 89.969 ns | 672 B | 105.648 ns | 784 B | Fluent was faster and allocated less for the order saga workflow. |
| Singleton | Construction | 26.618 ns | 184 B | 0.366 ns | 0 B | Generated static singleton surface resolution avoids runtime builder allocation. |
| Singleton | Execution | 74.198 ns | 504 B | 0.205 ns | 0 B | Generated static singleton access avoids the fluent resolver allocation path. |
| Priority Queue | Construction | 14.67 ns | 128 B | 14.33 ns | 128 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Priority Queue | Execution | 95.63 ns | 536 B | 93.42 ns | 536 B | Same allocation; generated was slightly faster for the fulfillment scheduling workflow. |
| Queue-Based Load Leveling | Construction | 17.64 ns | 176 B | 17.54 ns | 176 B | Effectively equivalent for this microbenchmark. |
| Queue-Based Load Leveling | Execution | 94.49 ns | 480 B | 94.42 ns | 480 B | Effectively equivalent for the fulfillment enqueue workflow. |
| Retry | Construction | 25.36 ns | 208 B | 27.18 ns | 208 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Retry | Execution | 110.53 ns | 600 B | 109.52 ns | 600 B | Same allocation; generated was slightly faster for the transient retry workflow. |
| Rate Limiting | Construction | 19.16 ns | 168 B | 18.19 ns | 168 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Rate Limiting | Execution | 247.62 ns | 1,200 B | 245.56 ns | 1,200 B | Same allocation; generated was slightly faster for the tenant rejection workflow. |
| Scatter-Gather | Construction | 59.78 ns | 408 B | 62.41 ns | 408 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Scatter-Gather | Execution | 327.74 ns | 1,704 B | 388.12 ns | 2,064 B | Fluent was faster and allocated less for the supplier quote fan-out workflow. |
| Scheduler Agent Supervisor | Construction | 47.29 ns | 400 B | 45.40 ns | 400 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Scheduler Agent Supervisor | Execution | 177.46 ns | 1,304 B | 180.14 ns | 1,304 B | Effectively equivalent for this scenario. |
| Sidecar | Construction | 59.96 ns | 488 B | 52.09 ns | 400 B | Generated reduced construction time and allocation in this microbenchmark. |
| Sidecar | Execution | 99.61 ns | 640 B | 100.51 ns | 640 B | Same allocation; fluent was slightly faster for the order sidecar submission workflow. |
| Service Activator | Construction | 4.825 ns | 32 B | 4.641 ns | 32 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Service Activator | Execution | 25.48 ns | 256 B | 26.49 ns | 256 B | Same allocation; fluent was slightly faster in this path. |
| Service Layer | Construction | 56.33 ns | 496 B | 41.36 ns | 296 B | Generated reduced construction time and allocation in this microbenchmark. |
| Service Layer | Execution | 151.32 ns | 960 B | 148.10 ns | 872 B | Generated slightly reduced execution time and allocation for the register-customer workflow. |
| Specification | Construction | 196.03 ns | 1,704 B | 136.87 ns | 1,008 B | Generated reduced construction time and allocation in this microbenchmark. |
| Specification | Execution | 111.25 ns | 344 B | 93.30 ns | 344 B | Same allocation; generated was faster for loan-application evaluation. |
| Value Object | Construction | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Value Object | Execution | Pending | Pending | Pending | Pending | Covered by the BenchmarkDotNet matrix; publish measured values after the next benchmark refresh. |
| Splitter | Construction | 3.664 ns | 24 B | 4.020 ns | 24 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Splitter | Execution | 135.516 ns | 808 B | 134.062 ns | 832 B | Generated was slightly faster; fluent allocated slightly less for order line splitting. |
| Strangler Fig | Construction | 53.71 ns | 416 B | 42.35 ns | 288 B | Generated reduced construction time and allocation in this microbenchmark. |
| Strangler Fig | Execution | 24.64 ns | 200 B | 20.50 ns | 168 B | Generated reduced execution time and allocation for the enterprise checkout routing workflow. |
| Table Data Gateway | Construction | 9.740 ns | 120 B | 9.698 ns | 120 B | Effectively equivalent for this microbenchmark. |
| Table Data Gateway | Execution | 90.51 ns | 600 B | 96.35 ns | 600 B | Same allocation; fluent was slightly faster for the insert-update-query workflow. |
| Transaction Script | Construction | 20.634 ns | 240 B | 5.839 ns | 40 B | Generated materially reduced construction time and allocation in this microbenchmark. |
| Transaction Script | Execution | 184.93 ns | 1,136 B | 98.28 ns | 600 B | Generated reduced execution time and allocation for the submit-order workflow. |
| Unit Of Work | Construction | 49.50 ns | 304 B | 46.91 ns | 304 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Unit Of Work | Execution | 121.03 ns | 824 B | 96.91 ns | 520 B | Generated reduced execution time and allocation for the checkout commit workflow. |
| Wire Tap | Construction | 47.13 ns | 496 B | 40.99 ns | 336 B | Generated reduced construction time and allocation in this microbenchmark. |
| Wire Tap | Execution | 214.72 ns | 1,232 B | 191.45 ns | 1,064 B | Generated reduced execution time and allocation for the order observability workflow. |
| Chain of Responsibility | Construction | 58.0959 ns | 656 B | 3.0491 ns | 24 B | Generated chain construction was materially faster and allocated less. |
| Chain of Responsibility | Execution | 60.2624 ns | 656 B | 4.3664 ns | 0 B | Generated handler dispatch was faster and allocation-free for the approval route. |
| Command | Construction | 19.6229 ns | 208 B | 3.2500 ns | 24 B | Generated command handler setup was faster and allocated less. |
| Command | Execution | 23.1740 ns | 232 B | 0.0125 ns | 0 B | Generated static command dispatch removed fluent command allocation overhead in this microbenchmark. |
| Interpreter | Construction | 149.7429 ns | 1,352 B | 109.6622 ns | 896 B | Generated interpreter factory reduced construction time and allocation. |
| Interpreter | Execution | 240.8832 ns | 1,464 B | 189.7219 ns | 1,008 B | Generated interpreter rules were faster and allocated less for pricing expressions. |
| Iterator | Construction | 34.2051 ns | 352 B | 4.8293 ns | 48 B | Generated iterator construction was materially lighter than fluent flow composition. |
| Iterator | Execution | 84.8700 ns | 480 B | 14.2925 ns | 48 B | Generated iterator execution was faster and allocated less for revenue traversal. |
| Mediator | Construction | 97.3925 ns | 1,144 B | 37.7610 ns | 416 B | Generated dispatcher mediator construction was faster and allocated less. |
| Mediator | Execution | 154.8279 ns | 1,320 B | 64.4207 ns | 416 B | Generated dispatcher send path was faster and allocated less for command dispatch. |
| Memento | Construction | 12.9626 ns | 128 B | 15.7943 ns | 120 B | Fluent construction was slightly faster; generated history allocated slightly less. |
| Memento | Execution | 116.4308 ns | 376 B | 6.0277 ns | 48 B | Generated memento capture and restore was materially faster and allocated less. |
| Observer | Construction | 4.9422 ns | 40 B | 6.9757 ns | 56 B | Fluent observer construction was slightly lighter for this small instance. |
| Observer | Execution | 30.9549 ns | 176 B | 44.3640 ns | 368 B | Fluent publish was lighter for this single-subscriber microbenchmark. |
| State | Construction | 173.0951 ns | 1,688 B | 3.0356 ns | 24 B | Generated state machine construction was materially faster and allocated less. |
| State | Execution | 179.4049 ns | 1,688 B | 3.3041 ns | 24 B | Generated transition dispatch was materially faster and allocated less. |
| Strategy | Construction | 49.6712 ns | 432 B | 49.4027 ns | 432 B | Fluent and generated strategy builders were effectively equivalent. |
| Strategy | Execution | 51.8884 ns | 432 B | 49.9407 ns | 432 B | Generated strategy execution was slightly faster with the same allocation. |
| Template Method | Construction | 10.3334 ns | 88 B | 3.0359 ns | 24 B | Generated template construction was faster and allocated less. |
| Template Method | Execution | 12.9121 ns | 88 B | 0.1900 ns | 0 B | Generated template execution removed fluent delegate overhead in this microbenchmark. |
| Visitor | Construction | 166.0976 ns | 1,720 B | 9.3177 ns | 112 B | Generated visitor builder construction was materially faster and allocated less. |
| Visitor | Execution | 197.2918 ns | 1,760 B | 59.6870 ns | 432 B | Generated visitor dispatch was faster and allocated less for document nodes. |

Run the benchmarks on target hardware before making final route decisions:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *LeaderElection* --artifacts artifacts/benchmarks --join
```
