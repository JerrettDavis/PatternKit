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

PatternKit covers **Creational**, **Structural**, and **Behavioral** patterns with fluent, discoverable APIs:

| Category       | Patterns                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| -------------- |-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Creational** | [Factory](patterns/creational/factory/factory.md) • [Composer](patterns/creational/builder/composer.md) • [ChainBuilder](patterns/creational/builder/chainbuilder.md) • [BranchBuilder](patterns/creational/builder/branchbuilder.md) • [MutableBuilder](patterns/creational/builder/mutablebuilder.md) • [Prototype](patterns/creational/prototype/prototype.md) • [Singleton](patterns/creational/singleton/singleton.md)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                |
| **Structural** | [Adapter](patterns/structural/adapter/fluent-adapter.md) • [Bridge](patterns/structural/bridge/bridge.md) • [Composite](patterns/structural/composite/composite.md) • [Decorator](patterns/structural/decorator/index.md) • [Facade](patterns/structural/facade/index.md) • [Flyweight](patterns/structural/flyweight/index.md) • [Proxy](patterns/structural/proxy/index.md)                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| **Behavioral** | [Strategy](patterns/behavioral/strategy/strategy.md) • [TryStrategy](patterns/behavioral/strategy/trystrategy.md) • [ActionStrategy](patterns/behavioral/strategy/actionstrategy.md) • [ActionChain](patterns/behavioral/chain/actionchain.md) • [ResultChain](patterns/behavioral/chain/resultchain.md) • [Command](patterns/behavioral/command/command.md) • [ReplayableSequence](patterns/behavioral/iterator/replayablesequence.md) • [WindowSequence](patterns/behavioral/iterator/windowsequence.md) • [Mediator](patterns/behavioral/mediator/mediator.md) • [Memento](patterns/behavioral/memento/memento.md) • [Observer](patterns/behavioral/observer/observer.md) • [AsyncObserver](patterns/behavioral/observer/asyncobserver.md) • [Visitor](patterns/behavioral/visitor/visitor.md) • [State](patterns/behavioral/state/state.md) • [Template Method](patterns/behavioral/template/template.md) |

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
