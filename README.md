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

---

## 📦 Patterns (Planned & In Progress)

PatternKit will grow to cover **Creational**, **Structural**, and **Behavioral** patterns with fluent, discoverable APIs:

| Category       | Patterns ✓ = implemented                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| -------------- |----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Creational** | [Factory](docs/patterns/creational/factory/factory.md) ✓ • [Composer](docs/patterns/creational/builder/composer.md) ✓ • [ChainBuilder](docs/patterns/creational/builder/chainbuilder.md) ✓ • [BranchBuilder](docs/patterns/creational/builder/chainbuilder.md) ✓ • [MutableBuilder](docs/patterns/creational/builder/mutablebuilder.md) ✓ • [Prototype](docs/patterns/creational/prototype/prototype.md) ✓ • [Singleton](docs/patterns/creational/singleton/singleton.md) ✓                                                                                                                                                                                                                                                                                                               |
| **Structural** | [Adapter](docs/patterns/structural/adapter/fluent-adapter.md) ✓ • [Bridge](docs/patterns/structural/bridge/bridge.md) ✓ • [Composite](docs/patterns/structural/composite/composite.md) ✓ • Decorator (planned) • Facade (planned) • Flyweight (planned) • Proxy (planned)                                                                                                                                                                                                                                                   |
| **Behavioral** | [Strategy](docs/patterns/behavioral/strategy/strategy.md) ✓ • [TryStrategy](docs/patterns/behavioral/strategy/trystrategy.md) ✓ • [ActionStrategy](docs/patterns/behavioral/strategy/actionstrategy.md) ✓ • [ActionChain](docs/patterns/behavioral/chain/actionchain.md) ✓ • [ResultChain](docs/patterns/behavioral/chain/resultchain.md) ✓ • [ReplayableSequence](docs/patterns/behavioral/iterator/replayablesequence.md) ✓ • [WindowSequence](docs/patterns/behavioral/iterator/windowsequence.md) ✓ • Command (planned) • Mediator (planned) • Memento (planned) • Observer (planned) • State (planned) • Template Method (planned) • Visitor (planned) |

Each pattern will ship with:

* A **fluent API** (`.When(...)`, `.Then(...)`, `.Finally(...)`, etc.)
* **Source-generated boilerplate** where possible.
* **DocFX-ready documentation** and **TinyBDD tests**.

---

## 🧪 Testing Philosophy

All patterns are validated with **[TinyBDD](https://github.com/jerrettdavis/TinyBdd)** and xUnit:

```csharp
[Feature("Strategy")]
public class StrategyTests : TinyBddXunitBase
{
    [Scenario("Positive/negative classification")]
    [Fact]
    public async Task ClassificationWorks()
    {
        await Given("a strategy with three branches", BuildStrategy)
            .When("executing with 5", s => s.Execute(5))
            .Then("result should be 'positive'", r => r == "positive")
            .AssertPassed();
    }
}
```

We keep tests **behavior-driven**, **readable**, and **high coverage**.

---


## 💡 Design Goals

* **Declarative:** Favor expression-based and fluent APIs over imperative setup.
* **Minimalism:** Prefer single-responsibility types and low ceremony.
* **Performance:** Allocation-free handlers, `in` parameters, ahead-of-time friendly.
* **Discoverability:** IntelliSense-first APIs; easy to read, easy to write.
* **Testability:** TinyBDD integration and mocks built-in where applicable.

---

## 🛠 Requirements

* **.NET 9.0 or later** (we use `in` parameters and modern generic features).
* C# 12 features enabled (`readonly struct`, static lambdas, etc.).

---

## 📚 Documentation

Full API documentation is published with **DocFX** (coming soon).
Each type and member ships with XML docs, examples, and cross-links between patterns.

---

## 🤝 Contributing

We welcome issues, discussions, and PRs.
Focus areas:

* Adding new patterns (start with Behavioral for max impact)
* Improving fluent builder syntax and source generator coverage
* Writing TinyBDD test scenarios for edge cases

---

## 📄 License

MIT — see [LICENSE](LICENSE) for details.

---

## ❤️ Inspiration

PatternKit is inspired by:

* The **Gang of Four** design patterns
* Fluent APIs from **ASP.NET Core**, **System.Linq**, and modern libraries
* The desire to make patterns **readable**, **performant**, and **fun** to use in 2025+
