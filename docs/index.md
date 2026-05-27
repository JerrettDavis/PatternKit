# PatternKit Documentation

> **Fluent Design Patterns for Modern .NET**  
> Fluent APIs and incremental source generators for classic patterns in modern .NET.

---

## 🌟 Overview

**PatternKit** is a library that implements classic [Gang of Four design patterns](https://en.wikipedia.org/wiki/Design_Patterns)
with fluent runtime helpers and Roslyn incremental source generators.

Instead of heavyweight base classes and repeated handwritten scaffolding, PatternKit favors:

- **Fluent builders** and chainable DSLs
- **Source generators** that emit deterministic compile-time code
- **AOT-friendly generated implementations** where pattern shape is known at design time
- **Strong typing** and discoverable APIs
- **Readable BDD-style tests** using [TinyBDD](https://github.com/JerrettDavis/TinyBDD)

---

## 🚀 Getting Started

Install the NuGet package:

```bash
dotnet add package PatternKit
```

For reusable `IServiceCollection` registrations:

```bash
dotnet add package PatternKit.Hosting.Extensions
```

### Example: `Strategy<TIn, TOut>`

```csharp
using PatternKit.Behavioral.Strategy;

var classify = Strategy<int, string>.Create()
    .When(i => i > 0).Then(i => "positive")
    .When(i => i < 0).Then(i => "negative")
    .Default(_ => "zero")
    .Build();

Console.WriteLine(classify.Execute(42));  // positive
Console.WriteLine(classify.Execute(-7));  // negative
Console.WriteLine(classify.Execute(0));   // zero
```

### Example: `TryStrategy<TIn, TOut>`

```csharp
var parser = TryStrategy<string, int>.Create()
    .Always((in string s, out int r) => int.TryParse(s, out r))
    .Finally((in string _, out int r) => { r = 0; return true; })
    .Build();

if (parser.Execute("123", out var value))
    Console.WriteLine(value); // 123
```

---

## 📚 Available Patterns

PatternKit covers 101 production-readiness patterns with fluent APIs, source-generated routes where applicable, IoC integration examples, TinyBDD coverage, and BenchmarkDotNet coverage-matrix validation:

| Category | Count | Patterns |
| --- | ---: | --- |
| Application Architecture | 16 | Activity Tracker, Anti-Corruption Layer, Audit Log, CQRS, Data Mapper, Domain Event, Event Sourcing, Feature Toggle, Identity Map, Materialized View, Repository, Service Layer, Specification, Table Data Gateway, Transaction Script, Unit of Work |
| Behavioral | 11 | Chain of Responsibility, Command, Interpreter, Iterator, Mediator, Memento, Observer, State, Strategy, Template Method, Visitor |
| Cloud Architecture | 17 | Ambassador, Backends for Frontends, Bulkhead, Cache-Aside, Circuit Breaker, External Configuration Store, Gateway Aggregation, Gateway Routing, Health Endpoint Monitoring, Leader Election, Priority Queue, Queue-Based Load Leveling, Rate Limiting, Retry, Scheduler Agent Supervisor, Sidecar, Strangler Fig |
| Creational | 5 | Abstract Factory, Builder, Factory Method, Prototype, Singleton |
| Enterprise Integration | 41 | Aggregator, Canonical Data Model, Channel Adapter, Channel Purger, Claim Check, Competing Consumers, Content Enricher, Content-Based Router, Control Bus, Correlation Identifier, Dead Letter Channel, Durable Subscriber, Dynamic Router, Event Notification, Event-Carried State Transfer, Event-Driven Consumer, Guaranteed Delivery, Invalid Message Channel, Mailbox, Message Bus, Message Channel, Message Envelope, Message Expiration, Message Filter, Message History, Message Store, Message Translator, Messaging Bridge, Messaging Gateway, Pipes and Filters, Polling Consumer, Publish-Subscribe, Recipient List, Request-Reply, Resequencer, Routing Slip, Saga / Process Manager, Scatter-Gather, Service Activator, Splitter, Wire Tap |
| Messaging Reliability | 3 | Idempotent Receiver, Inbox, Outbox |
| Structural | 7 | Adapter, Bridge, Composite, Decorator, Facade, Flyweight, Proxy |

See [Benchmarks](guides/benchmarks.md) and [Benchmark Results](guides/benchmark-results.md) for published fluent-vs-source-generated timing, allocation snapshots, and the full pattern/generator matrix.

## 🛠️ Source Generators

Prefer compile-time code generation over handwritten boilerplate? See the **[Generators](generators/index.md)** section for:

- **[Builder](generators/builder.md)** — Fluent object construction with validation
- **[Factory](generators/factory-class.md)** — Keyed product creation
- **[Adapter](generators/adapter.md)** — Interface and member adaptation
- **[Bridge](generators/bridge.md)** — Abstraction/implementation separation
- **[Chain](generators/chain.md)** — Request pipelines
- **[Command](generators/command.md)** — Command wrappers and dispatch
- **[Composite](generators/composite.md)** — Tree-shaped object models
- **[Decorator](generators/decorator.md)** — Base classes with forwarding
- **[Facade](generators/facade.md)** — Simplified subsystem interfaces
- **[Flyweight](generators/flyweight.md)** — Keyed flyweight factories
- **[Iterator](generators/iterator.md)** — Enumerable adapters
- **[Observer](generators/observer.md)** — Event hubs and subscriptions
- **[Proxy](generators/proxy.md)** — Access control and interception
- **[Singleton](generators/singleton.md)** — Instance accessors
- **[State Machine](generators/state-machine.md)** — State transitions
- **[Composer](generators/composer.md)** — Pipeline middleware composition
- **[Memento](generators/memento.md)** — State snapshots and undo/redo
- **[Strategy](generators/strategy.md)** — Predicate-based dispatch
- **[Dispatcher](generators/dispatcher.md)** — Mediator pattern (CQRS)
- **[Visitor](generators/visitor-generator.md)** — Type-safe double dispatch

All generators produce deterministic code with no runtime dependency on PatternKit.

Each supported pattern ships with tests, API documentation, and examples where applicable.


* **Fluent API**: readable and composable (`.When(...)`, `.Then(...)`, `.Finally(...)`)
* **Strongly-typed handlers** using `in` parameters
* **Performance focus** — explicit code paths with no reflection-based dispatch in generated implementations
* **XML docs and examples** available in API reference

---

## 🧪 Testing Philosophy

All patterns are tested with [TinyBDD](https://github.com/JerrettDavis/TinyBDD), enabling Gherkin-like,
human-readable scenarios:

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
