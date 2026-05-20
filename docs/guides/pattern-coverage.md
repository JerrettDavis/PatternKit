# Pattern Coverage

PatternKit tracks the canonical Gang of Four patterns as production surfaces, not just API names. Each pattern should have:

- a fluent runtime path in `PatternKit.Core`
- TinyBDD coverage for the runtime path
- user documentation and real-world examples
- an importable example path through `Microsoft.Extensions.DependencyInjection` where the example assembly is used
- a source-generated path, or a tracked issue when the generator is still planned

The source of truth is `PatternKitPatternCatalog` in `src/PatternKit.Examples/ProductionReadiness`. The TinyBDD tests in `PatternKitPatternCatalogTests` validate the catalog against the repository so missing files, missing examples, or undocumented generator gaps fail in CI.

## Current GoF Coverage

| Family | Pattern | Fluent path | Source-generated path |
| --- | --- | --- | --- |
| Creational | Abstract Factory | `AbstractFactory<,>` | Tracked in [#207](https://github.com/JerrettDavis/PatternKit/issues/207) |
| Creational | Builder | Builder helpers | Builder generator |
| Creational | Factory Method | `Factory<TKey, TValue>` | Factory Method generator |
| Creational | Prototype | `Prototype<TKey, TValue>` | Prototype generator |
| Creational | Singleton | `Singleton<T>` | Singleton generator |
| Structural | Adapter | `Adapter<TIn, TOut>` | Adapter generator |
| Structural | Bridge | `Bridge<TAbstraction, TImplementation>` | Bridge generator |
| Structural | Composite | `Composite<TNode, TResult>` | Composite generator |
| Structural | Decorator | `Decorator<TIn, TOut>` | Decorator generator |
| Structural | Facade | `Facade<TIn, TOut>` and `TypedFacade<T>` | Facade generator |
| Structural | Flyweight | `Flyweight<TKey, TValue>` | Flyweight generator |
| Structural | Proxy | `Proxy<TIn, TOut>` | Proxy generator |
| Behavioral | Chain of Responsibility | `ActionChain<T>` and `ResultChain<T>` | Chain generator |
| Behavioral | Command | `Command<T>` | Command generator |
| Behavioral | Interpreter | `Interpreter<TContext, TResult>` | Tracked in [#206](https://github.com/JerrettDavis/PatternKit/issues/206) |
| Behavioral | Iterator | `Flow<T>` and sequence helpers | Iterator generator |
| Behavioral | Mediator | `Mediator` | Dispatcher generator |
| Behavioral | Memento | `Memento<T>` | Memento generator |
| Behavioral | Observer | Observer primitives | Observer generator |
| Behavioral | State | `StateMachine<TState, TEvent>` | State Machine generator |
| Behavioral | Strategy | `Strategy<TIn, TOut>` and variants | Strategy generator |
| Behavioral | Template Method | `TemplateMethod<T>` and fluent templates | Template Method generator |
| Behavioral | Visitor | `Visitor<TBase, TResult>` and variants | Visitor generator |

## Adding Or Extending A Pattern

1. Add or update the fluent runtime implementation and TinyBDD tests.
2. Add or update the source generator, generator attributes, diagnostics, and TinyBDD generator tests.
3. Add a real-world example that can be imported from a normal application.
4. Register the example in `AddPatternKitExamples`.
5. Update the examples catalog and the pattern coverage catalog.
6. Add docs for the runtime path, generated path, and production example.
7. Run the relevant tests and land only when CI, docs, CodeQL, and coverage are green.

If a generator is intentionally deferred, create a GitHub issue and list the issue URL in the catalog. The tests allow only explicit, reviewed gaps.
