# PatternKit Generators

PatternKit includes a Roslyn incremental generator package (`PatternKit.Generators`) that emits pattern implementations at compile time. Use it when you want predictable generated code, design-time diagnostics, and less handwritten pattern scaffolding.

## When to Use

- You already express intent with attributes and want the compiler to write the implementation.
- You need synchronous APIs, `ValueTask`-first async APIs, or both.
- You want deterministic codegen that is friendly to trimming and AOT scenarios.

## Package & Setup

1. Add the analyzer package to your project:

   ```bash
   dotnet add package PatternKit.Generators
   ```

2. Ensure the project targets `netstandard2.0+` (for libraries) or any modern .NET target (for apps). No runtime references are required.

3. Mark your types with the attributes below; the generator produces partial classes at compile time.

## Available Generators

### Creational Patterns

| Generator | Description | Attribute |
|---|---|---|
| [**Builder**](builder.md) | GoF-aligned builders with mutable or state-projection models, sync/async pipelines | `[GenerateBuilder]` |
| [**Factory Method**](factory-method.md) | Keyed dispatcher from a static partial class | `[FactoryMethod]` |
| [**Factory Class**](factory-class.md) | GoF-style factory mapping keys to products | `[FactoryClass]` |
| [**Prototype**](prototype.md) | Clone/deep-copy generation for types | `[Prototype]` |
| [**Singleton**](singleton.md) | Thread-safe singleton accessors with optional factory hooks | `[Singleton]` |

### Structural Patterns

| Generator | Description | Attribute |
|---|---|---|
| [**Decorator**](decorator.md) | Base decorator classes with forwarding and composition helpers | `[GenerateDecorator]` |
| [**Facade**](facade.md) | Simplified interfaces to complex subsystems | `[GenerateFacade]` |
| [**Adapter**](adapter.md) | Adapter implementations from mapping attributes | `[GenerateAdapter]` |
| [**Bridge**](bridge.md) | Abstraction/implementation pairs with forwarding | `[BridgeAbstraction]` / `[BridgeImplementor]` |
| [**Composite**](composite.md) | Component trees and traversal helpers | `[CompositeComponent]` |
| [**Flyweight**](flyweight.md) | Keyed flyweight factories and caches | `[Flyweight]` |
| [**Proxy**](proxy.md) | Proxy implementations with interception support | `[GenerateProxy]` |

### Behavioral Patterns

| Generator | Description | Attribute |
|---|---|---|
| [**Chain**](chain.md) | Chain-of-responsibility pipelines | `[Chain]` |
| [**Command**](command.md) | Command objects and invokers | `[Command]` |
| [**Composer**](composer.md) | Pipeline composition from ordered steps | `[Composer]` |
| [**Iterator**](iterator.md) | Enumerable/async-enumerable iteration helpers | `[Iterator]` |
| [**Memento**](memento.md) | Immutable snapshots with optional undo/redo history | `[Memento]` |
| [**Observer**](observer.md) | Event hubs and observer dispatch | `[ObserverHub]` |
| [**State Machine**](state-machine.md) | Deterministic finite state machines | `[StateMachine]` |
| [**Strategy**](strategy.md) | Predicate-based dispatch with fluent builder | `[GenerateStrategy]` |
| [**Template Method**](template-method-generator.md) | Template method skeletons with hook points | `[Template]` |
| [**Visitor**](visitor-generator.md) | Type-safe visitor implementations | `[GenerateVisitor]` |

### Messaging

| Generator | Description | Attribute |
|---|---|---|
| [**Dispatcher**](dispatcher.md) | Mediator pattern with commands, notifications, and streams | `[GenerateDispatcher]` |
| [**Message Envelope**](messaging.md#generated-message-envelope) | Required message metadata contracts | `[GenerateMessageEnvelope]` |
| [**Content Router**](messaging.md#generated-content-router) | Content-based message routing factories | `[GenerateContentRouter]` |
| [**Recipient List**](messaging.md#generated-recipient-list) | Recipient fan-out factories | `[GenerateRecipientList]` |
| [**Splitter / Aggregator**](messaging.md#generated-splitter-and-aggregator) | Split/rejoin message routing factories | `[GenerateSplitter]` / `[GenerateAggregator]` |
| [**Routing Slip**](messaging.md#generated-routing-slip) | Ordered message itinerary factories | `[GenerateRoutingSlip]` |
| [**Saga**](messaging.md#generated-saga) | Typed process-manager transition factories | `[GenerateSaga]` |

## Quick Reference

### Creational

```csharp
// Builder - fluent object construction
[GenerateBuilder]
public partial class Person { public string Name { get; set; } }

// Factory - keyed product creation
[GenerateFactory(typeof(INotification), typeof(NotificationKind))]
public abstract partial class NotificationFactory { }

// Prototype - cloning
[Prototype]
public partial class Document { }
```

### Structural

```csharp
// Decorator - add behavior via wrapping
[GenerateDecorator]
public interface IRepository { }

// Facade - simplify subsystem access
[GenerateFacade]
public static partial class BillingHost { }

// Proxy - control access/add interception
[GenerateProxy]
public interface IService { }
```

### Behavioral

```csharp
// Composer - pipeline middleware
[Composer]
public partial class Pipeline
{
    [ComposeStep(0)] public T Step(in T x, Func<T, T> next) => next(x);
    [ComposeTerminal] public T End(in T x) => x;
}

// Memento - state snapshots
[Memento(GenerateCaretaker = true)]
public partial class EditorState { }

// Strategy - predicate dispatch
[GenerateStrategy("Router", typeof(Request), StrategyKind.Action)]
public partial class Router { }

// Template Method - algorithm skeleton
[Template]
public abstract partial class DataProcessor { }

// Visitor - type-safe double dispatch
[GenerateVisitor]
public interface IDocumentVisitor { }
```

### Messaging

```csharp
// Dispatcher - mediator pattern
[assembly: GenerateDispatcher(Namespace = "MyApp", Name = "Dispatcher")]

// Message envelope - generated required-header contract
[GenerateMessageEnvelope(typeof(OrderAccepted))]
[MessageEnvelopeHeader("correlation-id", typeof(string))]
public static partial class OrderAcceptedEnvelope { }

// Content router - generated first-match route factory
[GenerateContentRouter(typeof(Order), typeof(string))]
public static partial class OrderRouter { }

// Splitter and aggregator - generated split/rejoin factories
[GenerateSplitter(typeof(Order), typeof(OrderLine))]
public static partial class OrderSplitter { }

[GenerateAggregator(typeof(string), typeof(OrderLine), typeof(decimal))]
public static partial class OrderLineAggregator { }

// Routing slip - generated ordered itinerary factory
[GenerateRoutingSlip(typeof(Order))]
public static partial class OrderSlip { }

// Saga - generated process-manager factory
[GenerateSaga(typeof(OrderSagaState))]
public static partial class OrderSaga { }
```

## Examples

See [Generator Examples](examples.md) and [Source Generator Application Suite](../examples/source-generator-application-suite.md) for:
- DI module wiring and generated host builders
- Orchestrated application startup steps
- Generated facades, proxies, observers, mementos, state machines, strategies, visitors, and messaging factories
- Real-world usage patterns validated by `PatternKit.Examples.Tests`

## Troubleshooting

See [Troubleshooting](troubleshooting.md) for common issues and diagnostic codes.

## See Also

- [Patterns Overview](../patterns/index.md) — Manual pattern implementations
- [Guides](../guides/choosing-patterns.md) — When to use which pattern
