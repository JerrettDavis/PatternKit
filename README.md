# PatternKit

> **Fluent Design Patterns for Modern .NET**  
> Elegant, declarative, allocation-light implementations of classic patterns—optimized for .NET 9.


[![CI](https://github.com/JerrettDavis/PatternKit/actions/workflows/ci.yml/badge.svg)](https://github.com/JerrettDavis/PatternKit/actions/workflows/ci.yml)
[![CodeQL](https://github.com/JerrettDavis/PatternKit/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/JerrettDavis/PatternKit/security/code-scanning)
[![codecov](https://codecov.io/gh/JerrettDavis/PatternKit/graph/badge.svg?token=I0LO3HLUTP)](https://codecov.io/gh/JerrettDavis/PatternKit)
[![License](https://img.shields.io/badge/license-MIT-green.svg?style=flat-square)](LICENSE)

---

## ✨ Overview

**PatternKit** is a modern library that reimagines the GoF design patterns for .NET 9+.  
Instead of boilerplate-heavy class hierarchies, we favor:

- **Fluent builders** and DSLs (chainable, declarative, composable).
- **Source generators** to eliminate reflection and runtime overhead.
- **Zero-allocation hot paths** for performance-critical scenarios.
- **Strong typing** with `in` parameters, avoiding boxing and defensive copies.
- **Testable, deterministic APIs** that pair naturally with BDD and xUnit/NUnit/MSTest.

Our goal: make patterns a joy to use, not a chore to implement.

---

## 🚀 Quick Start

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

## 📘 Pattern Quick Reference
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

## 📚 Patterns Table
| Category       | Patterns ✓ = implemented                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| -------------- |--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Creational** | [Factory](docs/patterns/creational/factory/factory.md) ✓ • [Composer](docs/patterns/creational/builder/composer.md) ✓ • [ChainBuilder](docs/patterns/creational/builder/chainbuilder.md) ✓ • [BranchBuilder](docs/patterns/creational/builder/chainbuilder.md) ✓ • [MutableBuilder](docs/patterns/creational/builder/mutablebuilder.md) ✓ • [Prototype](docs/patterns/creational/prototype/prototype.md) ✓ • [Singleton](docs/patterns/creational/singleton/singleton.md) ✓                                                                                                                                                                                                                                                                |
| **Structural** | [Adapter](docs/patterns/structural/adapter/fluent-adapter.md) ✓ • [Bridge](docs/patterns/structural/bridge/bridge.md) ✓ • [Composite](docs/patterns/structural/composite/composite.md) ✓ • [Decorator](docs/patterns/structural/decorator/decorator.md) ✓ • [Facade](docs/patterns/structural/facade/facade.md) ✓ • [Flyweight](docs/patterns/structural/flyweight/index.md) ✓ • [Proxy](docs/patterns/structural/proxy/index.md) ✓                                                                                                                                                                                                                                                                                                                                  |
| **Behavioral** | [Strategy](docs/patterns/behavioral/strategy/strategy.md) ✓ • [TryStrategy](docs/patterns/behavioral/strategy/trystrategy.md) ✓ • [ActionStrategy](docs/patterns/behavioral/strategy/actionstrategy.md) ✓ • [ActionChain](docs/patterns/behavioral/chain/actionchain.md) ✓ • [ResultChain](docs/patterns/behavioral/chain/resultchain.md) ✓ • [ReplayableSequence](docs/patterns/behavioral/iterator/replayablesequence.md) ✓ • [WindowSequence](docs/patterns/behavioral/iterator/windowsequence.md) ✓ • [Command](docs/patterns/behavioral/command/command.md) ✓ • [Mediator](docs/patterns/behavioral/mediator/mediator.md) ✓ • [Memento](docs/patterns/behavioral/memento/memento.md) ✓ • Observer (planned) • State (planned) • Template Method (planned) • Visitor (planned) |
