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
| Enterprise Integration | Message Channel | `MessageChannel<TPayload>` | Message Channel generator |
| Enterprise Integration | Channel Purger | `ChannelPurger<TPayload>` | Channel Purger generator |
| Enterprise Integration | Invalid Message Channel | `InvalidMessageChannel<TPayload>` | Invalid Message Channel generator |
| Enterprise Integration | Polling Consumer | `PollingConsumer<TPayload>` | Polling Consumer generator |
| Enterprise Integration | Event-Driven Consumer | `EventDrivenConsumer<TPayload>` | Event-Driven Consumer generator |
| Enterprise Integration | Channel Adapter | `ChannelAdapter<TExternal, TPayload>` | Channel Adapter generator |
| Enterprise Integration | Messaging Gateway | `MessagingGateway<TRequest, TResponse>` | Messaging Gateway generator |
| Enterprise Integration | Service Activator | `ServiceActivator<TRequest, TResponse>` | Service Activator generator |
| Enterprise Integration | Message Envelope | `Message<TPayload>`, headers, context | Messaging generator |
| Enterprise Integration | Message Translator | `MessageTranslator<TInput, TOutput>` | Message Translator generator |
| Enterprise Integration | Canonical Data Model | `CanonicalDataModel<TCanonical>` | Canonical Data Model generator |
| Enterprise Integration | Event-Carried State Transfer | `EventCarriedStateTransfer<TEvent,TKey,TState>` | Event-Carried State Transfer generator |
| Enterprise Integration | Event Notification | `EventNotification<TEvent,TKey>` | Event Notification generator |
| Enterprise Integration | Claim Check | `ClaimCheck<TPayload>` | Claim Check generator |
| Enterprise Integration | Dead Letter Channel | `DeadLetterChannel<TPayload>` | Dead Letter Channel generator |
| Enterprise Integration | Content-Based Router | `ContentRouter<TPayload, TResult>` | Messaging generator |
| Enterprise Integration | Dynamic Router | `DynamicRouter<TPayload, TResult>` | Dynamic Router generator |
| Enterprise Integration | Message Bus | `MessageBus<TPayload>` | Message Bus generator |
| Enterprise Integration | Messaging Bridge | `MessagingBridge<TInbound,TOutbound>` | Messaging Bridge generator |
| Enterprise Integration | Message Filter | `MessageFilter<TPayload>` | Message Filter generator |
| Enterprise Integration | Message Store | `MessageStore<TPayload>` | Message Store generator |
| Enterprise Integration | Wire Tap | `WireTap<TPayload>` | Wire Tap generator |
| Enterprise Integration | Control Bus | `ControlBus<TCommand>` | Control Bus generator |
| Enterprise Integration | Scatter-Gather | `ScatterGather<TRequest,TResponse,TResult>` | Scatter-Gather generator |
| Enterprise Integration | Resequencer | `Resequencer<TPayload>` | Resequencer generator |
| Enterprise Integration | Recipient List | `RecipientList<TPayload>` | Messaging generator |
| Enterprise Integration | Competing Consumers | `CompetingConsumerGroup<TMessage, TResult>` | Competing Consumers generator |
| Enterprise Integration | Pipes and Filters | `PipesAndFiltersPipeline<TContext>` | Pipes and Filters generator |
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
| Cloud Architecture | Bulkhead | `BulkheadPolicy<T>` | Bulkhead generator |
| Cloud Architecture | Queue-Based Load Leveling | `QueueLoadLevelingPolicy<T>` | Queue Load Leveling generator |
| Cloud Architecture | Health Endpoint Monitoring | `HealthEndpoint<TContext>` | Health Endpoint Monitoring generator |
| Cloud Architecture | Priority Queue | `PriorityQueuePolicy<TItem, TPriority>` | Priority Queue generator |
| Cloud Architecture | Cache-Aside | `CacheAsidePolicy<T>` | Cache-Aside generator |
| Cloud Architecture | Rate Limiting | `RateLimitPolicy<T>` | Rate Limiting generator |
| Cloud Architecture | External Configuration Store | `ExternalConfigurationStore<TSettings>` | External Configuration Store generator |
| Cloud Architecture | Gateway Aggregation | `GatewayAggregation<TRequest,TResponse>` | Gateway Aggregation generator |
| Cloud Architecture | Gateway Routing | `GatewayRouting<TRequest,TResponse>` | Gateway Routing generator |
| Cloud Architecture | Strangler Fig | `StranglerFig<TRequest,TResponse>` | Strangler Fig generator |
| Cloud Architecture | Sidecar | `Sidecar<TRequest,TResponse>` | Sidecar generator |
| Cloud Architecture | Backends for Frontends | `BackendsForFrontends<TRequest,TResponse>` | Backends for Frontends generator |
| Cloud Architecture | Ambassador | `Ambassador<TRequest,TResponse>` | Ambassador generator |
| Cloud Architecture | Leader Election | `LeaderElection<TContext>` | Leader Election generator |
| Cloud Architecture | Scheduler Agent Supervisor | `SchedulerAgentSupervisor<TWork,TResult>` | Scheduler Agent Supervisor generator |
| Application Architecture | CQRS | Mediator/dispatcher command-query split | Dispatcher generator |
| Application Architecture | Specification | `Specification<T>` and named registries | Specification generator |
| Application Architecture | Repository | `IRepository<TEntity,TKey>` and `InMemoryRepository<TEntity,TKey>` | Repository generator |
| Application Architecture | Unit of Work | `UnitOfWork` | Unit of Work generator |
| Application Architecture | Data Mapper | `DataMapper<TDomain,TData>` | Data Mapper generator |
| Application Architecture | Identity Map | `IdentityMap<TEntity,TKey>` | Identity Map generator |
| Application Architecture | Transaction Script | `TransactionScript<TRequest,TResponse>` | Transaction Script generator |
| Application Architecture | Service Layer | `IServiceOperation<TRequest,TResponse>` and `ServiceLayerOperation<TRequest,TResponse>` | Service Layer generator |
| Application Architecture | Domain Event | `IDomainEvent` and `DomainEventDispatcher<TEventBase>` | Domain Event generator |
| Application Architecture | Table Data Gateway | `ITableDataGateway<TRow,TKey>` and `InMemoryTableDataGateway<TRow,TKey>` | Table Data Gateway generator |
| Application Architecture | Event Sourcing | `IEventStore<TEvent,TStreamId>` and `InMemoryEventStore<TEvent,TStreamId>` | Event Sourcing generator |
| Application Architecture | Feature Toggle | `IFeatureToggleSet<TContext>` and `FeatureToggleSet<TContext>` | Feature Toggle generator |
| Application Architecture | Audit Log | `IAuditLog<TEntry,TKey>` and `InMemoryAuditLog<TEntry,TKey>` | Audit Log generator |
| Application Architecture | Materialized View | `IMaterializedView<TState,TEvent>` and `MaterializedView<TState,TEvent>` | Materialized View generator |
| Application Architecture | Anti-Corruption Layer | `AntiCorruptionLayer<TExternal, TDomain>` | Anti-Corruption Layer generator |
| Application Architecture | Activity Tracker | `ActivityTracker` | Activity Tracker generator |

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
