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

PatternKit will ultimately support the full spectrum of **creational**, **structural**, and **behavioral** patterns:

| Category       | Patterns ✓ = Implemented                                                                                                                                                                                                                                                                                                                       |
| -------------- |------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **Creational** | [Factory](xref:PatternKit.Creational) (planned) • [Builder](xref:PatternKit.Creational.Builder) (planned) • Prototype (planned) • Singleton (planned)                                                                                                                                                                                          |
| **Structural** | Adapter (planned) • Bridge (planned) • Composite (planned) • Decorator (planned) • Facade (planned) • Flyweight (planned) • Proxy (planned)                                                                                                                                                                                                    |
| **Behavioral** | [Strategy](xref:PatternKit.Behavioral.Strategy.Strategy`2) ✓ • [TryStrategy](xref:PatternKit.Behavioral.Strategy.TryStrategy`2) ✓ • Chain of Responsibility (planned) • Command (planned) • Iterator (planned) • Mediator (planned) • Memento (planned) • Observer (planned) • State (planned) • Template Method (planned) • Visitor (planned) |

Each pattern ships with:

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

---

## 🔗 Explore the API

* [Behavioral Patterns](xref:PatternKit.Behavioral)
* [Creational Patterns](xref:PatternKit.Creational)
* [Structural Patterns](xref:PatternKit.Structural)
* [Common Utilities](xref:PatternKit.Common)

> **Tip:** Use the search bar in the left navigation panel to quickly find classes, methods, and examples.

