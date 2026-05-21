# Pattern Coverage

PatternKit tracks design, integration, messaging, reliability, and architecture patterns as production surfaces, not just API names. The canonical Gang of Four patterns are one baseline, but the catalog is intentionally broader: Enterprise Integration Patterns, cloud architecture patterns, DDD-adjacent application patterns, and messaging reliability patterns can all be included when PatternKit has a credible runtime or generated integration story.

Each pattern should have:

- a fluent runtime path in `PatternKit.Core`
- TinyBDD coverage for the runtime path
- user documentation and real-world examples
- an importable example path through `Microsoft.Extensions.DependencyInjection` where the example assembly is used
- a source-generated path, or a tracked issue when the generator is still planned

The source of truth is `PatternKitPatternCatalog` in `src/PatternKit.Examples/ProductionReadiness`. The TinyBDD tests in `PatternKitPatternCatalogTests` validate the catalog against the repository so missing files, missing examples, or undocumented generator gaps fail in CI.

## Current GoF Baseline

| Family | Pattern | Fluent path | Source-generated path |
| --- | --- | --- | --- |
| Creational | Abstract Factory | `AbstractFactory<TKey>` | Abstract Factory generator |
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
| Behavioral | Interpreter | `Interpreter<TContext, TResult>` | Interpreter generator |
| Behavioral | Iterator | `Flow<T>` and sequence helpers | Iterator generator |
| Behavioral | Mediator | `Mediator` | Dispatcher generator |
| Behavioral | Memento | `Memento<T>` | Memento generator |
| Behavioral | Observer | Observer primitives | Observer generator |
| Behavioral | State | `StateMachine<TState, TEvent>` | State Machine generator |
| Behavioral | Strategy | `Strategy<TIn, TOut>` and variants | Strategy generator |
| Behavioral | Template Method | `TemplateMethod<T>` and fluent templates | Template Method generator |
| Behavioral | Visitor | `Visitor<TBase, TResult>` and variants | Visitor generator |

## Enterprise And Architecture Coverage

| Family | Pattern | Fluent/runtime path | Source-generated path |
| --- | --- | --- | --- |
| Enterprise Integration | Message Envelope | `Message<TPayload>`, headers, context | Messaging generator |
| Enterprise Integration | Content-Based Router | `ContentRouter<TPayload, TResult>` | Messaging generator |
| Enterprise Integration | Recipient List | `RecipientList<TPayload>` | Messaging generator |
| Enterprise Integration | Splitter | `Splitter<TIn, TOut>` | Messaging generator |
| Enterprise Integration | Aggregator | `Aggregator<TKey, TIn, TOut>` | Messaging generator |
| Enterprise Integration | Routing Slip | `RoutingSlip<TPayload>` | Messaging generator |
| Enterprise Integration | Saga / Process Manager | `Saga<TState>` | Messaging generator |
| Enterprise Integration | Mailbox | `Mailbox<TPayload>` | Messaging generator |
| Messaging Reliability | Idempotent Receiver | `IdempotentReceiver<TPayload, TResult>` | Reliability pipeline generator |
| Messaging Reliability | Inbox | `InboxProcessor<TPayload, TResult>` | Reliability pipeline generator |
| Messaging Reliability | Outbox | `InMemoryOutbox<TPayload>` and dispatcher contracts | Reliability pipeline generator |
| Enterprise Integration | Request-Reply | Messaging backplane facade example | Backplane topology generator |
| Enterprise Integration | Publish-Subscribe | Messaging backplane facade example | Backplane topology generator |
| Cloud Architecture | Retry | `RetryPolicy<T>` | Retry generator |
| Cloud Architecture | Circuit Breaker | `CircuitBreakerPolicy<T>` | Circuit Breaker generator |
| Application Architecture | CQRS | Mediator/dispatcher command-query split | Dispatcher generator |
| Application Architecture | Specification | `Specification<T>` and named registries | Specification generator |

## Research Baselines

The catalog is allowed to grow beyond GoF when an external catalog describes a recurring enterprise pattern that PatternKit can make concrete:

- Enterprise Integration Patterns: message routing, splitter, aggregator, routing slip, idempotent receiver, and related messaging patterns.
- Patterns of Enterprise Application Architecture: application and data-access patterns such as Unit of Work, Identity Map, Repository, and Data Mapper.
- Cloud architecture patterns: retry, circuit breaker, CQRS, external configuration, competing consumers, and related distributed-system patterns.
- Domain-driven design tactical patterns: aggregate, domain event, specification, repository, bounded context, and process manager.

Entries should still be selective. A pattern belongs in the catalog only when PatternKit can demonstrate a runtime path, a source-generated path or tracked generator issue, documentation, TinyBDD coverage, and an importable example.

## Adding Or Extending A Pattern

1. Add or update the fluent runtime implementation and TinyBDD tests.
2. Add or update the source generator, generator attributes, diagnostics, and TinyBDD generator tests.
3. Add a real-world example that can be imported from a normal application.
4. Register the example in `AddPatternKitExamples`.
5. Update the examples catalog and the pattern coverage catalog.
6. Add docs for the runtime path, generated path, and production example.
7. Run the relevant tests and land only when CI, docs, CodeQL, and coverage are green.

If a generator is intentionally deferred, create a GitHub issue and list the issue URL in the catalog. The tests allow only explicit, reviewed gaps.
