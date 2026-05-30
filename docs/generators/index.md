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
| [**Abstract Factory**](abstract-factory.md) | Product-family factories with optional IServiceProvider construction | `[GenerateAbstractFactory]` |
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
| [**Interpreter**](interpreter.md) | DSL rule factories for terminal and non-terminal expressions | `[GenerateInterpreter]` |
| [**Iterator**](iterator.md) | Enumerable/async-enumerable iteration helpers | `[Iterator]` |
| [**Memento**](memento.md) | Immutable snapshots with optional undo/redo history | `[Memento]` |
| [**Observer**](observer.md) | Event hubs and observer dispatch | `[ObserverHub]` |
| [**State Machine**](state-machine.md) | Deterministic finite state machines | `[StateMachine]` |
| [**Strategy**](strategy.md) | Predicate-based dispatch with fluent builder | `[GenerateStrategy]` |
| [**Specification**](specification.md) | Named business-rule registries | `[GenerateSpecificationRegistry]` |
| [**Repository**](repository.md) | In-memory repository factories from key selectors | `[GenerateRepository]` |
| [**Anti-Corruption Layer**](anti-corruption-layer.md) | External-to-domain translation boundaries with validation | `[GenerateAntiCorruptionLayer]` |
| [**Activity Tracker**](activity-tracker.md) | Active-work tracker gates for loading and readiness workflows | `[GenerateActivityTracker]` |
| [**Manual Task Gate**](manual-task-gate.md) | Human approval gates for workflow pauses and manual decisions | `[GenerateManualTaskGate]` |
| [**Timeout Manager**](timeout-manager.md) | Deadline registry for expiring pending workflow work | `[GenerateTimeoutManager]` |
| [**Audit Log**](audit-log.md) | Append-only audit log factories from key selectors | `[GenerateAuditLog]` |
| [**Unit of Work**](unit-of-work.md) | Ordered commit and rollback units | `[GenerateUnitOfWork]` |
| [**Data Mapper**](data-mapper.md) | Domain/data model mapper factories | `[GenerateDataMapper]` |
| [**Identity Map**](identity-map.md) | Scoped object identity caches from key selectors | `[GenerateIdentityMap]` |
| [**Materialized View**](materialized-view.md) | Event projection read-model factories from handlers | `[GenerateMaterializedView]` |
| [**Transaction Script**](transaction-script.md) | Typed application workflow factories | `[GenerateTransactionScript]` |
| [**Service Layer**](service-layer.md) | Application operation boundary factories | `[GenerateServiceLayerOperation]` |
| [**Domain Event**](domain-event.md) | Domain event dispatcher factories | `[GenerateDomainEventDispatcher]` |
| [**Table Data Gateway**](table-data-gateway.md) | Row gateway factories from key selectors | `[GenerateTableDataGateway]` |
| [**Event Sourcing**](event-sourcing.md) | Append-only event store factories | `[GenerateEventStore]` |
| [**Feature Toggle**](feature-toggle.md) | Contextual feature toggle factories | `[GenerateFeatureToggleSet]` |
| [**Template Method**](template-method-generator.md) | Template method skeletons with hook points | `[Template]` |
| [**Visitor**](visitor-generator.md) | Type-safe visitor implementations | `[GenerateVisitor]` |

### Messaging

| Generator | Description | Attribute |
|---|---|---|
| [**Dispatcher**](dispatcher.md) | Mediator pattern with commands, notifications, and streams | `[GenerateDispatcher]` |
| [**Message Channel**](message-channel.md) | Typed channel factories for in-process message queues | `[GenerateMessageChannel]` |
| [**Message Bus**](message-bus.md) | Topic bus topology factories over message channels | `[GenerateMessageBus]` |
| [**Messaging Bridge**](messaging-bridge.md) | Channel-to-bus bridge factories for topology boundaries | `[GenerateMessagingBridge]` |
| [**Correlation Identifier**](correlation-identifier.md) | Correlation factories for request/reply and workflow message traces | `[GenerateCorrelationIdentifier]` |
| [**Message History**](message-history.md) | Message handling history factories for auditable envelopes | `[GenerateMessageHistory]` |
| [**Channel Purger**](channel-purger.md) | Channel maintenance purger factories for in-process message queues | `[GenerateChannelPurger]` |
| [**Invalid Message Channel**](invalid-message-channel.md) | Invalid-message channel builder factories for validation boundaries | `[GenerateInvalidMessageChannel]` |
| [**Polling Consumer**](polling-consumer.md) | Pull-based message consumer factories | `[GeneratePollingConsumer]` |
| [**Event-Driven Consumer**](event-driven-consumer.md) | Push-based message consumer factories | `[GenerateEventDrivenConsumer]` |
| [**Durable Subscriber**](durable-subscriber.md) | Checkpointed replay subscriber factories | `[GenerateDurableSubscriber]` |
| [**Channel Adapter**](channel-adapter.md) | External DTO to message-channel adapter factories | `[GenerateChannelAdapter]` |
| [**Messaging Gateway**](messaging-gateway.md) | Typed request/response gateway factories | `[GenerateMessagingGateway]` |
| [**Service Activator**](service-activator.md) | Message-to-service operation factories | `[GenerateServiceActivator]` |
| [**Message Envelope**](messaging.md#generated-message-envelope) | Required message metadata contracts | `[GenerateMessageEnvelope]` |
| [**Message Translator**](message-translator.md) | Partner and transport event normalization | `[GenerateMessageTranslator]` |
| [**Content Enricher**](content-enricher.md) | Ordered async payload enrichment pipelines | `[GenerateContentEnricher]` |
| [**Canonical Data Model**](canonical-data-model.md) | Source-to-canonical contract normalization | `[GenerateCanonicalDataModel]` |
| [**Event-Carried State Transfer**](event-carried-state-transfer.md) | State-rich event projection factories | `[GenerateEventCarriedStateTransfer]` |
| [**Event Notification**](event-notification.md) | Compact event notification factories | `[GenerateEventNotification]` |
| [**Claim Check**](claim-check.md) | External payload storage references | `[GenerateClaimCheck]` |
| [**Dead Letter Channel**](dead-letter-channel.md) | Failed-message capture and replay handoff | `[GenerateDeadLetterChannel]` |
| [**Content Router**](messaging.md#generated-content-router) | Content-based message routing factories | `[GenerateContentRouter]` |
| [**Message Filter**](message-filter.md) | Named allow-rule filters for message consumers | `[GenerateMessageFilter]` |
| [**Message Expiration**](message-expiration.md) | Deadline stamping and stale-message evaluation policies | `[GenerateMessageExpiration]` |
| [**Guaranteed Delivery**](guaranteed-delivery.md) | Durable delivery queue factories with lease and retry settings | `[GenerateGuaranteedDelivery]` |
| [**Message Store**](message-store.md) | Message audit, lookup, and replay store factories | `[GenerateMessageStore]` |
| [**Wire Tap**](wire-tap.md) | Side-channel message observability factories | `[GenerateWireTap]` |
| [**Control Bus**](control-bus.md) | Operational command bus factories for message processors | `[GenerateControlBus]` |
| [**Scatter-Gather**](scatter-gather.md) | Fan-out request and reply aggregation factories | `[GenerateScatterGather]` |
| [**Resequencer**](resequencer.md) | Sequence-aware buffering factories for out-of-order messages | `[GenerateResequencer]` |
| [**Recipient List**](messaging.md#generated-recipient-list) | Recipient fan-out factories | `[GenerateRecipientList]` |
| [**Splitter / Aggregator**](messaging.md#generated-splitter-and-aggregator) | Split/rejoin message routing factories | `[GenerateSplitter]` / `[GenerateAggregator]` |
| [**Routing Slip**](messaging.md#generated-routing-slip) | Ordered message itinerary factories | `[GenerateRoutingSlip]` |
| [**Saga**](messaging.md#generated-saga) | Typed process-manager transition factories | `[GenerateSaga]` |
| [**Mailbox**](messaging.md#generated-mailbox) | Serialized in-process inbox factories | `[GenerateMailbox]` |
| [**Reliability Pipeline**](messaging.md#generated-reliability-pipeline) | Idempotent receiver, inbox, and outbox factories | `[GenerateReliabilityPipeline]` |
| [**Backplane Topology**](messaging.md#generated-backplane-topology) | Request/reply routes and publish/subscribe endpoint topology | `[GenerateBackplaneTopology]` |

### Cloud And Resilience

| Generator | Description | Attribute |
|---|---|---|
| [**Retry**](retry.md) | Bounded retry policy factories for transient results and exceptions | `[GenerateRetryPolicy]` |
| [**Circuit Breaker**](circuit-breaker.md) | Dependency isolation policy factories with open and half-open states | `[GenerateCircuitBreakerPolicy]` |
| [**Bulkhead**](bulkhead.md) | Bounded concurrency and queue isolation policy factories | `[GenerateBulkheadPolicy]` |
| [**Queue Load Leveling**](queue-load-leveling.md) | Bounded worker queue policy factories | `[GenerateQueueLoadLevelingPolicy]` |
| [**Health Endpoint Monitoring**](health-endpoint-monitoring.md) | Typed service health endpoint factories | `[GenerateHealthEndpoint]` |
| [**Priority Queue**](priority-queue.md) | Business-priority queue factories | `[GeneratePriorityQueue]` |
| [**Cache-Aside**](cache-aside.md) | Read-through cache policy factories with TTL and cache predicates | `[GenerateCacheAsidePolicy]` |
| [**Cache Stampede Protection**](cache-stampede-protection.md) | Keyed single-flight policy factories for suppressing duplicate cache-miss loads | `[GenerateCacheStampedeProtection]` |
| [**Rate Limiting**](rate-limiting.md) | Key-partitioned fixed-window rate limit policy factories | `[GenerateRateLimitPolicy]` |
| [**External Configuration Store**](external-configuration-store.md) | Typed centralized configuration loaders | `[GenerateExternalConfigurationStore]` |
| [**Gateway Aggregation**](gateway-aggregation.md) | API gateway response composition factories | `[GenerateGatewayAggregation]` |
| [**Gateway Routing**](gateway-routing.md) | API gateway route dispatch factories | `[GenerateGatewayRouting]` |
| [**Strangler Fig**](strangler-fig.md) | Legacy-to-modern migration routing factories | `[GenerateStranglerFig]` |
| [**Sidecar**](sidecar.md) | Companion behavior pipeline factories | `[GenerateSidecar]` |
| [**Backends for Frontends**](backends-for-frontends.md) | Client-specific facade factories | `[GenerateBackendsForFrontends]` |
| [**Ambassador**](ambassador.md) | Outbound connectivity wrapper factories | `[GenerateAmbassador]` |
| [**Leader Election**](leader-election.md) | Lease-backed active worker factories | `[GenerateLeaderElection]` |
| [**Scheduler Agent Supervisor**](scheduler-agent-supervisor.md) | Scheduled worker supervision factories | `[GenerateSchedulerAgentSupervisor]` |

## Quick Reference

### Creational

```csharp
// Builder - fluent object construction
[GenerateBuilder]
public partial class Person { public string Name { get; set; } }

// Factory - keyed product creation
[GenerateFactory(typeof(INotification), typeof(NotificationKind))]
public abstract partial class NotificationFactory { }

// Abstract factory - generated product families
[GenerateAbstractFactory(typeof(Platform))]
[AbstractFactoryProduct(Platform.Windows, typeof(IButton), typeof(WindowsButton))]
[AbstractFactoryProduct(Platform.Linux, typeof(IButton), typeof(LinuxButton))]
public static partial class PlatformWidgets { }

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

// Interpreter - generated DSL rule factory
[GenerateInterpreter(typeof(PricingContext), typeof(decimal))]
public static partial class PricingRules { }

// Template Method - algorithm skeleton
[Template]
public abstract partial class DataProcessor { }

// Anti-corruption layer - external model boundary
[GenerateAntiCorruptionLayer(typeof(LegacyOrderDto), typeof(CommerceOrder))]
public static partial class LegacyOrderAcl { }

// Message translator - partner event normalization
[GenerateMessageTranslator(typeof(PartnerOrderAccepted), typeof(CommerceOrderAccepted))]
public static partial class PartnerOrderTranslator { }

// Event-carried state transfer - state-rich projection events
[GenerateEventCarriedStateTransfer(typeof(InventoryAdjustedEvent), typeof(string), typeof(InventoryReadModel))]
public static partial class InventoryStateTransfer { }

// Event notification - compact event signals
[GenerateEventNotification(typeof(OrderAccepted), typeof(string))]
public static partial class OrderAcceptedNotification { }

// Claim check - external payload storage reference
[GenerateClaimCheck(typeof(LargeOrderDocument), StoreName = "document-archive")]
public static partial class LargeDocumentClaims { }

// Dead-letter channel - failed message capture and replay handoff
[GenerateDeadLetterChannel(typeof(FulfillmentCommand), ChannelName = "fulfillment-dead-letter")]
public static partial class FulfillmentDeadLetters { }

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

// Competing consumers - generated shared work group builder
[GenerateCompetingConsumerGroup(typeof(OrderWork), typeof(OrderResult), MaxConcurrentDeliveries = 4)]
public static partial class OrderConsumers { }

// Pipes and filters - generated ordered pipeline builder
[GeneratePipesAndFiltersPipeline(typeof(OrderContext), PipelineName = "fulfillment")]
public static partial class OrderPipeline { }

// Splitter and aggregator - generated split/rejoin factories
[GenerateSplitter(typeof(Order), typeof(OrderLine))]
public static partial class OrderSplitter { }

[GenerateAggregator(typeof(string), typeof(OrderLine), typeof(decimal))]
public static partial class OrderLineAggregator { }

// Mailbox - generated serialized inbox factory
[GenerateMailbox(typeof(OrderWork), Capacity = 32, BackpressurePolicy = "Wait")]
public static partial class OrderMailbox { }

// Reliability pipeline - generated idempotent receiver, inbox, and outbox factories
[GenerateReliabilityPipeline(typeof(AcceptOrder), typeof(string), typeof(OrderAccepted))]
public static partial class OrderReliability { }

// Durable subscriber - generated checkpointed replay subscriber
[GenerateDurableSubscriber(typeof(OrderShipmentEvent), SubscriberName = "shipment-projection")]
public static partial class OrderShipmentSubscriber { }

// Backplane topology - generated request/reply and pub/sub host wiring
[GenerateBackplaneTopology(typeof(OrderBackplaneServices), HostBuilderType = typeof(OrderBackplaneHostBuilder))]
[BackplaneRequestReply(typeof(SubmitOrder), typeof(OrderAccepted), "orders", nameof(OrderBackplaneServices.AcceptAsync))]
[BackplaneSubscription(typeof(OrderSubmitted), "orders.submitted", "audit-service", nameof(OrderBackplaneServices.AuditAsync))]
public static partial class OrderBackplane { }

// Routing slip - generated ordered itinerary factory
[GenerateRoutingSlip(typeof(Order))]
public static partial class OrderSlip { }

// Saga - generated process-manager factory
[GenerateSaga(typeof(OrderSagaState))]
public static partial class OrderSaga { }
```

### Cloud And Resilience

```csharp
// Retry - generated bounded retry policy
[GenerateRetryPolicy(typeof(InventoryResponse), MaxAttempts = 3, BackoffFactor = 2)]
public static partial class InventoryRetryPolicy { }

// Circuit breaker - generated dependency isolation policy
[GenerateCircuitBreakerPolicy(typeof(FulfillmentResponse), FailureThreshold = 2, BreakDurationMilliseconds = 30000)]
public static partial class FulfillmentCircuitBreakerPolicy { }

// Bulkhead - generated bounded concurrency policy
[GenerateBulkheadPolicy(typeof(ShippingAllocation), MaxConcurrency = 4, MaxQueueLength = 16, QueueTimeoutMilliseconds = 250)]
public static partial class ShippingBulkheadPolicy { }

// Cache-aside - generated read-through cache policy
[GenerateCacheAsidePolicy(typeof(ProductReadModel), TimeToLiveMilliseconds = 300000)]
public static partial class ProductCatalogCachePolicy { }

// Rate limiting - generated tenant or key budget policy
[GenerateRateLimitPolicy(typeof(SearchResponse), PermitLimit = 2, WindowMilliseconds = 60000)]
public static partial class ProductSearchRateLimitPolicy { }

// Gateway aggregation - generated downstream response composition
[GenerateGatewayAggregation(typeof(CustomerDashboardRequest), typeof(CustomerDashboardResponse))]
public static partial class CustomerDashboardGateway { }

// Gateway routing - generated route dispatch
[GenerateGatewayRouting(typeof(ProductGatewayRequest), typeof(ProductGatewayResponse))]
public static partial class ProductGatewayRouting { }

// Strangler Fig - generated legacy-to-modern migration routing
[GenerateStranglerFig(typeof(CheckoutMigrationRequest), typeof(CheckoutMigrationResponse))]
public static partial class CheckoutMigration { }

// Sidecar - generated companion behavior around a primary handler
[GenerateSidecar(typeof(OrderTelemetryRequest), typeof(OrderTelemetryResponse))]
public static partial class OrderTelemetrySidecar { }
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
