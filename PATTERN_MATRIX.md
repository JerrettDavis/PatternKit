# PatternKit Pattern Matrix

Quick reference for all Gang of Four patterns and their PatternKit implementations.

---

## Creational Patterns

| GoF Pattern | PatternKit Class | Variants | Fluent | Async | Status |
|-------------|------------------|----------|--------|-------|--------|
| Abstract Factory | - | - | - | - | **NOT IMPLEMENTED** |
| Builder | `BranchBuilder<TPred,THandler>` | 4 | Yes | No | Full |
| | `ChainBuilder<T>` | | Yes | No | Full |
| | `Composer<TState,TOut>` | | Yes | No | Full |
| | `MutableBuilder<T>` | | Yes | No | Full |
| Factory Method | `Factory<TKey,TOut>` | 2 | Yes | No | Partial* |
| | `Factory<TKey,TIn,TOut>` | | Yes | No | Partial* |
| Prototype | `Prototype<T>` | 2 | Yes | No | Full |
| | `Prototype<TKey,T>` | | Yes | No | Full |
| Singleton | `Singleton<T>` | 1 | Yes | No | Full |

*Registry-based rather than inheritance-based

---

## Structural Patterns

| GoF Pattern | PatternKit Class | Variants | Fluent | Async | Status |
|-------------|------------------|----------|--------|-------|--------|
| Adapter | `Adapter<TIn,TOut>` | 1 | Yes | No | Full |
| Bridge | `Bridge<TIn,TOut,TImpl>` | 1 | Yes | No | Full |
| Composite | `Composite<TIn,TOut>` | 1 | Yes | No | Full |
| Decorator | `Decorator<TIn,TOut>` | 1 | Yes | No | Full |
| Facade | `Facade<TIn,TOut>` | 2 | Yes | No | Full |
| | `TypedFacade<TInterface>` | | Yes | No | Full |
| Flyweight | `Flyweight<TKey,TValue>` | 1 | Yes | No | Full |
| Proxy | `Proxy<TIn,TOut>` | 7 types | Yes | No | Full |

---

## Behavioral Patterns

| GoF Pattern | PatternKit Class | Variants | Fluent | Async | Status |
|-------------|------------------|----------|--------|-------|--------|
| Chain of Resp. | `ActionChain<TCtx>` | 2 | Yes | No | Full |
| | `ResultChain<TIn,TOut>` | | Yes | No | Full |
| Command | `Command<TCtx>` | 1 | Yes | Yes | Full |
| Interpreter | - | - | - | - | **NOT IMPLEMENTED** |
| Iterator | `ReplayableSequence<T>` | 4 | Yes | Yes | Partial* |
| | `WindowSequence<T>` | | Yes | No | |
| | `Flow<T>` | | Yes | No | |
| | `AsyncFlow<T>` | | Yes | Yes | |
| Mediator | `Mediator` | 1 | Yes | Yes | Full |
| Memento | `Memento<TState>` | 1 | Yes | No | Full |
| Observer | `Observer<TEvent>` | 2 | Yes | Yes | Full |
| | `AsyncObserver<TEvent>` | | Yes | Yes | Full |
| State | `StateMachine<TState,TEvent>` | 2 | Yes | Yes | Full |
| | `AsyncStateMachine<TState,TEvent>` | | Yes | Yes | Full |
| Strategy | `Strategy<TIn,TOut>` | 4 | Yes | Yes | Full |
| | `TryStrategy<TIn,TOut>` | | Yes | No | Full |
| | `ActionStrategy<TIn>` | | Yes | No | Full |
| | `AsyncStrategy<TIn,TOut>` | | Yes | Yes | Full |
| Template Method | `Template<TContext,TResult>` | 4 | Yes | Yes | Full |
| | `TemplateMethod<TContext,TResult>` | | No* | No | Full |
| | `AsyncTemplate<TContext,TResult>` | | Yes | Yes | Full |
| | `AsyncTemplateMethod<TContext,TResult>` | | No* | Yes | Full |
| Visitor | `Visitor<TBase,TResult>` | 4 | Yes | Yes | **WRONG** |
| | `ActionVisitor<TBase>` | | Yes | No | |
| | `AsyncVisitor<TBase,TResult>` | | Yes | Yes | |
| | `AsyncActionVisitor<TBase>` | | Yes | Yes | |

*LINQ-style rather than classic iterator
*TemplateMethod uses inheritance rather than fluent API
*Visitor is actually TypeDispatcher (Strategy-based, not double dispatch)

---

## Quick Lookup by Use Case

### "I need to create objects..."

| Use Case | Pattern | PatternKit Class |
|----------|---------|------------------|
| ...one type, keyed | Factory | `Factory<TKey,TOut>` |
| ...one type, with config | Builder | `MutableBuilder<T>` |
| ...by cloning template | Prototype | `Prototype<T>` |
| ...single instance | Singleton | `Singleton<T>` |
| ...families of related | Abstract Factory | **NOT IMPLEMENTED** |

### "I need to structure code..."

| Use Case | Pattern | PatternKit Class |
|----------|---------|------------------|
| ...convert interfaces | Adapter | `Adapter<TIn,TOut>` |
| ...separate abstraction/impl | Bridge | `Bridge<TIn,TOut,TImpl>` |
| ...treat trees uniformly | Composite | `Composite<TIn,TOut>` |
| ...add behavior dynamically | Decorator | `Decorator<TIn,TOut>` |
| ...simplify subsystem | Facade | `Facade<TIn,TOut>` |
| ...share instances | Flyweight | `Flyweight<TKey,TValue>` |
| ...control access | Proxy | `Proxy<TIn,TOut>` |

### "I need to handle behavior..."

| Use Case | Pattern | PatternKit Class |
|----------|---------|------------------|
| ...pass through handlers | Chain | `ActionChain<TCtx>` |
| ...encapsulate request | Command | `Command<TCtx>` |
| ...iterate elements | Iterator | `Flow<T>` |
| ...centralize communication | Mediator | `Mediator` |
| ...save/restore state | Memento | `Memento<TState>` |
| ...notify subscribers | Observer | `Observer<TEvent>` |
| ...state-based behavior | State | `StateMachine<TState,TEvent>` |
| ...select algorithm | Strategy | `Strategy<TIn,TOut>` |
| ...define skeleton | Template | `Template<TContext,TResult>` |
| ...dispatch by type | "Visitor" | `Visitor<TBase,TResult>` |
| ...parse expressions | Interpreter | **NOT IMPLEMENTED** |

---

## Thread Safety Quick Reference

| Category | Thread-Safe After Build | Notes |
|----------|------------------------|-------|
| All Builders | NO | Mutable during construction |
| Singleton | YES | Double-checked locking |
| Flyweight | YES | Lock-free reads |
| Observer | YES | Copy-on-write |
| Memento | YES | Lock-based |
| Proxy (Virtual/Caching) | YES | Double-checked locking |
| All Others | YES* | Immutable after build |

*Immutable doesn't guarantee thread-safe execution if handlers have side effects

---

## API Consistency Reference

### Entry Points
```csharp
Pattern<TArgs>.Create()              // Returns Builder
Pattern<TArgs>.Create(requiredArg)   // When arg is required
```

### Configuration Methods
```csharp
.When(predicate)                     // Conditional configuration
.Then(handler)                       // Handler after When
.Default(handler)                    // Fallback handler
.With(transform)                     // Apply transformation
.Require(validator)                  // Add validation
.Map(key, value)                     // Key-value mapping
```

### Terminal Methods
```csharp
.Build()                             // Returns immutable instance
```

### Execution Methods
```csharp
.Execute(input)                      // Throws on failure
.TryExecute(input, out result)       // Returns bool
.ExecuteAsync(input, ct)             // Async variant
```

---

## Namespace Structure

```
PatternKit.Core
├── Creational
│   ├── Builder
│   ├── Factory
│   ├── Prototype
│   └── Singleton
├── Structural
│   ├── Adapter
│   ├── Bridge
│   ├── Composite
│   ├── Decorator
│   ├── Facade
│   ├── Flyweight
│   └── Proxy
└── Behavioral
    ├── Chain
    ├── Command
    ├── Iterator
    ├── Mediator
    ├── Memento
    ├── Observer
    ├── State
    ├── Strategy
    ├── Template
    └── Visitor
```

---

## Version Notes

- **Implemented:** 21/23 patterns (91%)
- **Missing:** Abstract Factory, Interpreter
- **Needs Fix:** Visitor (misnamed - actually TypeDispatcher)
- **Total Variants:** 44 classes across 21 patterns

---

*Last Updated: 2025-12-30*
