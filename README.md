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

---

## 📦 Patterns (Planned & In Progress)

PatternKit will grow to cover **Creational**, **Structural**, and **Behavioral** patterns with fluent, discoverable APIs:

| Category       | Patterns ✓ = implemented                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                  |
| -------------- |-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Creational** | [Factory](docs/patterns/creational/factory/factory.md) ✓ • [Composer](docs/patterns/creational/builder/composer.md) ✓ • [ChainBuilder](docs/patterns/creational/builder/chainbuilder.md) ✓ • [BranchBuilder](docs/patterns/creational/builder/chainbuilder.md) ✓ • [MutableBuilder](docs/patterns/creational/builder/mutablebuilder.md) ✓ • [Prototype](docs/patterns/creational/prototype/prototype.md) ✓ • [Singleton](docs/patterns/creational/singleton/singleton.md) ✓                                                                                                               |
| **Structural** | [Adapter](docs/patterns/structural/adapter/fluent-adapter.md) ✓ • [Bridge](docs/patterns/structural/bridge/bridge.md) ✓ • [Composite](docs/patterns/structural/composite/composite.md) ✓ • Decorator (planned) • Facade (planned) • Flyweight (planned) • Proxy (planned)                                                                                                                                                                                                                                                                                                                 |
| **Behavioral** | [Strategy](docs/patterns/behavioral/strategy/strategy.md) ✓ • [TryStrategy](docs/patterns/behavioral/strategy/trystrategy.md) ✓ • [ActionStrategy](docs/patterns/behavioral/strategy/actionstrategy.md) ✓ • [ActionChain](docs/patterns/behavioral/chain/actionchain.md) ✓ • [ResultChain](docs/patterns/behavioral/chain/resultchain.md) ✓ • [Command](docs/patterns/behavioral/command/command.md) ✓ • Iterator (planned) • [Mediator](docs/behavioral/mediator/mediator.md) • Memento (planned) • Observer (planned) • State (planned) • Template Method (planned) • Visitor (planned) |

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
