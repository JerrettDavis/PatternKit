# PatternKit Generators

PatternKit includes a Roslyn incremental generator package (`PatternKit.Generators`) that emits pattern implementations at compile time. Use it when you want predictable, allocation-light helpers without writing the boilerplate by hand.

## When to Use

- You already express intent with attributes and want the compiler to write the implementation.
- You need both synchronous and `ValueTask`-first async entry points.
- You want deterministic codegen with no runtime dependency on PatternKit.

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
| [**Factory Method**](factory-method.md) | Keyed dispatcher from a static partial class | `[GenerateFactoryMethod]` |
| [**Factory Class**](factory-class.md) | GoF-style factory mapping keys to products | `[GenerateFactory]` |
| [**Prototype**](prototype.md) | Clone/deep-copy generation for types | `[Prototype]` |

### Structural Patterns

| Generator | Description | Attribute |
|---|---|---|
| [**Decorator**](decorator.md) | Base decorator classes with forwarding and composition helpers | `[GenerateDecorator]` |
| [**Facade**](facade.md) | Simplified interfaces to complex subsystems | `[GenerateFacade]` |
| [**Proxy**](proxy.md) | Proxy implementations with interception support | `[GenerateProxy]` |

### Behavioral Patterns

| Generator | Description | Attribute |
|---|---|---|
| [**Composer**](composer.md) | Pipeline composition from ordered steps | `[Composer]` |
| [**Memento**](memento.md) | Immutable snapshots with optional undo/redo history | `[Memento]` |
| [**Strategy**](strategy.md) | Predicate-based dispatch with fluent builder | `[GenerateStrategy]` |
| [**Template Method**](template-method-generator.md) | Template method skeletons with hook points | `[Template]` |
| [**Visitor**](visitor-generator.md) | Type-safe visitor implementations | `[GenerateVisitor]` |

### Messaging

| Generator | Description | Attribute |
|---|---|---|
| [**Dispatcher**](dispatcher.md) | Mediator pattern with commands, notifications, and streams | `[GenerateDispatcher]` |

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
```

## Examples

See the samples in `PatternKit.Examples/Generators` for:
- DI module wiring
- Orchestrated application steps
- Real-world usage patterns

## Troubleshooting

See [Troubleshooting](troubleshooting.md) for common issues and diagnostic codes.

## See Also

- [Patterns Overview](../patterns/index.md) — Manual pattern implementations
- [Guides](../guides/choosing-patterns.md) — When to use which pattern
