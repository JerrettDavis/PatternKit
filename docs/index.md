# PatternKit Documentation

> **Fluent Design Patterns for Modern .NET**  
> Elegant, declarative, allocation-light implementations of classic patterns—optimized for .NET&nbsp;9.

---

## 🌟 Overview

**PatternKit** is a library that reimagines classic [Gang of Four design patterns](https://en.wikipedia.org/wiki/Design_Patterns)
for the modern .NET ecosystem.  

Instead of heavyweight base classes and verbose boilerplate, PatternKit favors:

- **Fluent builders** and chainable DSLs
- **Source generators** to remove reflection and runtime cost
- **Zero-allocation handlers** with `in` parameters for hot paths
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

PatternKit will grow to cover **Creational**, **Structural**, and **Behavioral** patterns with fluent, discoverable APIs:

| Category       | Patterns ✓ = implemented                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 |
| -------------- |----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Creational** | [Factory](patterns/creational/factory/factory.md) ✓ • [Composer](patterns/creational/builder/composer.md) ✓ • [ChainBuilder](patterns/creational/builder/chainbuilder.md) ✓ • [BranchBuilder](patterns/creational/builder/chainbuilder.md) ✓ • [MutableBuilder](patterns/creational/builder/mutablebuilder.md) ✓ • [Prototype](patterns/creational/prototype/prototype.md) ✓ • [Singleton](patterns/creational/singleton/singleton.md) ✓                                                                                                                 |
| **Structural** | [Adapter](patterns/structural/adapter/fluent-adapter.md) ✓ • [Bridge](patterns/structural/bridge/bridge.md) ✓ • [Composite](patterns/structural/composite/composite.md) ✓ • [Decorator](patterns/structural/decorator/index.md) ✓ • [Facade](patterns/structural/facade/facade.md) ✓ • Flyweight (planned) • Proxy (planned)                                                                                                                                                                                                                             |
| **Behavioral** | [Strategy](patterns/behavioral/strategy/strategy.md) ✓ • [TryStrategy](patterns/behavioral/strategy/trystrategy.md) ✓ • [ActionStrategy](patterns/behavioral/strategy/actionstrategy.md) ✓ • [ActionChain](patterns/behavioral/chain/actionchain.md) ✓ • [ResultChain](patterns/behavioral/chain/resultchain.md) ✓ • [Command](patterns/behavioral/command/command.md) ✓ • [ReplayableSequence](patterns/behavioral/iterator/replayablesequence.md) ✓ • [WindowSequence](patterns/behavioral/iterator/windowsequence.md) ✓ • [Mediator](behavioral/mediator/mediator.md) ✓ • Memento (planned) • Observer (planned) • State (planned) • Template Method (planned) • Visitor (planned) |

Each pattern will ship with:


* **Fluent API**: readable and composable (`.When(...)`, `.Then(...)`, `.Finally(...)`)
* **Strongly-typed handlers** using `in` parameters
* **Performance focus** — no hidden allocations or reflection
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
