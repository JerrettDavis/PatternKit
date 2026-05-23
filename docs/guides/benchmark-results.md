# Benchmark Results

PatternKit publishes two kinds of benchmark results:

- Scenario timings compare fluent and source-generated routes for patterns with dedicated BenchmarkDotNet scenario classes.
- Coverage matrix results prove every catalog pattern and generator source has a reportable BenchmarkDotNet route with source, test, and documentation validation.

The latest measured timings below were captured on Windows 11, Intel Core i9-14900K, .NET SDK 10.0.108, .NET 10.0.8, BenchmarkDotNet 0.15.8, using the `current-tfm` job. Treat them as directional; run the suite on deployment-class hardware before making final hot-path decisions.

## Scenario Timing Results

| Pattern | Phase | Fluent mean | Fluent allocation | Generated mean | Generated allocation | Decision signal |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| Leader Election | Construction | 14.28 ns | 104 B | 15.91 ns | 104 B | Same allocation; fluent was slightly faster in this microbenchmark. |
| Leader Election | Execution | 43.62 ns | 360 B | 144.37 ns | 312 B | Generated allocated about 13% less memory, while fluent was faster in this path. |
| Scheduler Agent Supervisor | Construction | 47.29 ns | 400 B | 45.40 ns | 400 B | Same allocation; generated was slightly faster in this microbenchmark. |
| Scheduler Agent Supervisor | Execution | 177.46 ns | 1,304 B | 180.14 ns | 1,304 B | Effectively equivalent for this scenario. |

## Coverage Matrix Summary

The coverage matrix currently publishes 88 catalog patterns and 352 pattern route results. Each pattern has four BenchmarkDotNet routes: fluent construction, fluent execution, source-generated construction, and source-generated execution.

| Category | Patterns | Published route results |
| --- | ---: | ---: |
| Application Architecture | 15 | 60 |
| Behavioral | 11 | 44 |
| Cloud Architecture | 17 | 68 |
| Creational | 5 | 20 |
| Enterprise Integration | 30 | 120 |
| Messaging Reliability | 3 | 12 |
| Structural | 7 | 28 |

The generator matrix currently publishes 84 generator source route results.

## Pattern Matrix Results

| Category | Pattern | Fluent construction | Fluent execution | Generated construction | Generated execution |
| --- | --- | --- | --- | --- | --- |
| Application Architecture | Anti-Corruption Layer | Covered | Covered | Covered | Covered |
| Application Architecture | Audit Log | Covered | Covered | Covered | Covered |
| Application Architecture | CQRS | Covered | Covered | Covered | Covered |
| Application Architecture | Data Mapper | Covered | Covered | Covered | Covered |
| Application Architecture | Domain Event | Covered | Covered | Covered | Covered |
| Application Architecture | Event Sourcing | Covered | Covered | Covered | Covered |
| Application Architecture | Feature Toggle | Covered | Covered | Covered | Covered |
| Application Architecture | Identity Map | Covered | Covered | Covered | Covered |
| Application Architecture | Materialized View | Covered | Covered | Covered | Covered |
| Application Architecture | Repository | Covered | Covered | Covered | Covered |
| Application Architecture | Service Layer | Covered | Covered | Covered | Covered |
| Application Architecture | Specification | Covered | Covered | Covered | Covered |
| Application Architecture | Table Data Gateway | Covered | Covered | Covered | Covered |
| Application Architecture | Transaction Script | Covered | Covered | Covered | Covered |
| Application Architecture | Unit of Work | Covered | Covered | Covered | Covered |
| Behavioral | Chain of Responsibility | Covered | Covered | Covered | Covered |
| Behavioral | Command | Covered | Covered | Covered | Covered |
| Behavioral | Interpreter | Covered | Covered | Covered | Covered |
| Behavioral | Iterator | Covered | Covered | Covered | Covered |
| Behavioral | Mediator | Covered | Covered | Covered | Covered |
| Behavioral | Memento | Covered | Covered | Covered | Covered |
| Behavioral | Observer | Covered | Covered | Covered | Covered |
| Behavioral | State | Covered | Covered | Covered | Covered |
| Behavioral | Strategy | Covered | Covered | Covered | Covered |
| Behavioral | Template Method | Covered | Covered | Covered | Covered |
| Behavioral | Visitor | Covered | Covered | Covered | Covered |
| Cloud Architecture | Ambassador | Covered | Covered | Covered | Covered |
| Cloud Architecture | Backends for Frontends | Covered | Covered | Covered | Covered |
| Cloud Architecture | Bulkhead | Covered | Covered | Covered | Covered |
| Cloud Architecture | Cache-Aside | Covered | Covered | Covered | Covered |
| Cloud Architecture | Circuit Breaker | Covered | Covered | Covered | Covered |
| Cloud Architecture | External Configuration Store | Covered | Covered | Covered | Covered |
| Cloud Architecture | Gateway Aggregation | Covered | Covered | Covered | Covered |
| Cloud Architecture | Gateway Routing | Covered | Covered | Covered | Covered |
| Cloud Architecture | Health Endpoint Monitoring | Covered | Covered | Covered | Covered |
| Cloud Architecture | Leader Election | Covered | Covered | Covered | Covered |
| Cloud Architecture | Priority Queue | Covered | Covered | Covered | Covered |
| Cloud Architecture | Queue-Based Load Leveling | Covered | Covered | Covered | Covered |
| Cloud Architecture | Rate Limiting | Covered | Covered | Covered | Covered |
| Cloud Architecture | Retry | Covered | Covered | Covered | Covered |
| Cloud Architecture | Scheduler Agent Supervisor | Covered | Covered | Covered | Covered |
| Cloud Architecture | Sidecar | Covered | Covered | Covered | Covered |
| Cloud Architecture | Strangler Fig | Covered | Covered | Covered | Covered |
| Creational | Abstract Factory | Covered | Covered | Covered | Covered |
| Creational | Builder | Covered | Covered | Covered | Covered |
| Creational | Factory Method | Covered | Covered | Covered | Covered |
| Creational | Prototype | Covered | Covered | Covered | Covered |
| Creational | Singleton | Covered | Covered | Covered | Covered |
| Enterprise Integration | Aggregator | Covered | Covered | Covered | Covered |
| Enterprise Integration | Canonical Data Model | Covered | Covered | Covered | Covered |
| Enterprise Integration | Channel Adapter | Covered | Covered | Covered | Covered |
| Enterprise Integration | Claim Check | Covered | Covered | Covered | Covered |
| Enterprise Integration | Competing Consumers | Covered | Covered | Covered | Covered |
| Enterprise Integration | Content-Based Router | Covered | Covered | Covered | Covered |
| Enterprise Integration | Control Bus | Covered | Covered | Covered | Covered |
| Enterprise Integration | Dead Letter Channel | Covered | Covered | Covered | Covered |
| Enterprise Integration | Event Notification | Covered | Covered | Covered | Covered |
| Enterprise Integration | Event-Carried State Transfer | Covered | Covered | Covered | Covered |
| Enterprise Integration | Event-Driven Consumer | Covered | Covered | Covered | Covered |
| Enterprise Integration | Mailbox | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Channel | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Envelope | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Filter | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Store | Covered | Covered | Covered | Covered |
| Enterprise Integration | Message Translator | Covered | Covered | Covered | Covered |
| Enterprise Integration | Messaging Gateway | Covered | Covered | Covered | Covered |
| Enterprise Integration | Pipes and Filters | Covered | Covered | Covered | Covered |
| Enterprise Integration | Polling Consumer | Covered | Covered | Covered | Covered |
| Enterprise Integration | Publish-Subscribe | Covered | Covered | Covered | Covered |
| Enterprise Integration | Recipient List | Covered | Covered | Covered | Covered |
| Enterprise Integration | Request-Reply | Covered | Covered | Covered | Covered |
| Enterprise Integration | Resequencer | Covered | Covered | Covered | Covered |
| Enterprise Integration | Routing Slip | Covered | Covered | Covered | Covered |
| Enterprise Integration | Saga / Process Manager | Covered | Covered | Covered | Covered |
| Enterprise Integration | Scatter-Gather | Covered | Covered | Covered | Covered |
| Enterprise Integration | Service Activator | Covered | Covered | Covered | Covered |
| Enterprise Integration | Splitter | Covered | Covered | Covered | Covered |
| Enterprise Integration | Wire Tap | Covered | Covered | Covered | Covered |
| Messaging Reliability | Idempotent Receiver | Covered | Covered | Covered | Covered |
| Messaging Reliability | Inbox | Covered | Covered | Covered | Covered |
| Messaging Reliability | Outbox | Covered | Covered | Covered | Covered |
| Structural | Adapter | Covered | Covered | Covered | Covered |
| Structural | Bridge | Covered | Covered | Covered | Covered |
| Structural | Composite | Covered | Covered | Covered | Covered |
| Structural | Decorator | Covered | Covered | Covered | Covered |
| Structural | Facade | Covered | Covered | Covered | Covered |
| Structural | Flyweight | Covered | Covered | Covered | Covered |
| Structural | Proxy | Covered | Covered | Covered | Covered |

## Generator Matrix Results

| Generator | Source | Matrix result |
| --- | --- | --- |
| AdapterGenerator | `src/PatternKit.Generators/Adapter/AdapterGenerator.cs` | Covered |
| AmbassadorGenerator | `src/PatternKit.Generators/Ambassador/AmbassadorGenerator.cs` | Covered |
| AntiCorruptionLayerGenerator | `src/PatternKit.Generators/AntiCorruption/AntiCorruptionLayerGenerator.cs` | Covered |
| AuditLogGenerator | `src/PatternKit.Generators/AuditLog/AuditLogGenerator.cs` | Covered |
| BackendsForFrontendsGenerator | `src/PatternKit.Generators/BackendsForFrontends/BackendsForFrontendsGenerator.cs` | Covered |
| BridgeGenerator | `src/PatternKit.Generators/Bridge/BridgeGenerator.cs` | Covered |
| BuilderGenerator | `src/PatternKit.Generators/Builders/BuilderGenerator.cs` | Covered |
| BulkheadPolicyGenerator | `src/PatternKit.Generators/Bulkhead/BulkheadPolicyGenerator.cs` | Covered |
| CacheAsidePolicyGenerator | `src/PatternKit.Generators/CacheAside/CacheAsidePolicyGenerator.cs` | Covered |
| CanonicalDataModelGenerator | `src/PatternKit.Generators/CanonicalDataModel/CanonicalDataModelGenerator.cs` | Covered |
| ChainGenerator | `src/PatternKit.Generators/Chain/ChainGenerator.cs` | Covered |
| CircuitBreakerPolicyGenerator | `src/PatternKit.Generators/CircuitBreaker/CircuitBreakerPolicyGenerator.cs` | Covered |
| ExternalConfigurationStoreGenerator | `src/PatternKit.Generators/Cloud/ExternalConfigurationStoreGenerator.cs` | Covered |
| CommandGenerator | `src/PatternKit.Generators/Command/CommandGenerator.cs` | Covered |
| ComposerGenerator | `src/PatternKit.Generators/ComposerGenerator.cs` | Covered |
| CompositeGenerator | `src/PatternKit.Generators/Composite/CompositeGenerator.cs` | Covered |
| DataMapperGenerator | `src/PatternKit.Generators/DataMapping/DataMapperGenerator.cs` | Covered |
| DecoratorGenerator | `src/PatternKit.Generators/DecoratorGenerator.cs` | Covered |
| DomainEventDispatcherGenerator | `src/PatternKit.Generators/DomainEvents/DomainEventDispatcherGenerator.cs` | Covered |
| EventCarriedStateTransferGenerator | `src/PatternKit.Generators/EventCarriedStateTransfer/EventCarriedStateTransferGenerator.cs` | Covered |
| EventNotificationGenerator | `src/PatternKit.Generators/EventNotification/EventNotificationGenerator.cs` | Covered |
| EventStoreGenerator | `src/PatternKit.Generators/EventSourcing/EventStoreGenerator.cs` | Covered |
| FacadeGenerator | `src/PatternKit.Generators/FacadeGenerator.cs` | Covered |
| AbstractFactoryGenerator | `src/PatternKit.Generators/Factories/AbstractFactoryGenerator.cs` | Covered |
| FactoriesGenerator | `src/PatternKit.Generators/Factories/FactoriesGenerator.cs` | Covered |
| FeatureToggleSetGenerator | `src/PatternKit.Generators/FeatureToggles/FeatureToggleSetGenerator.cs` | Covered |
| FlyweightGenerator | `src/PatternKit.Generators/Flyweight/FlyweightGenerator.cs` | Covered |
| GatewayAggregationGenerator | `src/PatternKit.Generators/GatewayAggregation/GatewayAggregationGenerator.cs` | Covered |
| GatewayRoutingGenerator | `src/PatternKit.Generators/GatewayRouting/GatewayRoutingGenerator.cs` | Covered |
| HealthEndpointMonitoringGenerator | `src/PatternKit.Generators/HealthEndpointMonitoring/HealthEndpointMonitoringGenerator.cs` | Covered |
| IdentityMapGenerator | `src/PatternKit.Generators/IdentityMap/IdentityMapGenerator.cs` | Covered |
| InterpreterGenerator | `src/PatternKit.Generators/Interpreter/InterpreterGenerator.cs` | Covered |
| IteratorGenerator | `src/PatternKit.Generators/Iterator/IteratorGenerator.cs` | Covered |
| LeaderElectionGenerator | `src/PatternKit.Generators/LeaderElection/LeaderElectionGenerator.cs` | Covered |
| MaterializedViewGenerator | `src/PatternKit.Generators/MaterializedViews/MaterializedViewGenerator.cs` | Covered |
| MementoGenerator | `src/PatternKit.Generators/MementoGenerator.cs` | Covered |
| BackplaneTopologyGenerator | `src/PatternKit.Generators/Messaging/BackplaneTopologyGenerator.cs` | Covered |
| ChannelAdapterGenerator | `src/PatternKit.Generators/Messaging/ChannelAdapterGenerator.cs` | Covered |
| ClaimCheckGenerator | `src/PatternKit.Generators/Messaging/ClaimCheckGenerator.cs` | Covered |
| CompetingConsumerGroupGenerator | `src/PatternKit.Generators/Messaging/CompetingConsumerGroupGenerator.cs` | Covered |
| ContentRouterGenerator | `src/PatternKit.Generators/Messaging/ContentRouterGenerator.cs` | Covered |
| ControlBusGenerator | `src/PatternKit.Generators/Messaging/ControlBusGenerator.cs` | Covered |
| DeadLetterChannelGenerator | `src/PatternKit.Generators/Messaging/DeadLetterChannelGenerator.cs` | Covered |
| DispatcherGenerator | `src/PatternKit.Generators/Messaging/DispatcherGenerator.cs` | Covered |
| EventDrivenConsumerGenerator | `src/PatternKit.Generators/Messaging/EventDrivenConsumerGenerator.cs` | Covered |
| MailboxGenerator | `src/PatternKit.Generators/Messaging/MailboxGenerator.cs` | Covered |
| MessageChannelGenerator | `src/PatternKit.Generators/Messaging/MessageChannelGenerator.cs` | Covered |
| MessageEnvelopeGenerator | `src/PatternKit.Generators/Messaging/MessageEnvelopeGenerator.cs` | Covered |
| MessageFilterGenerator | `src/PatternKit.Generators/Messaging/MessageFilterGenerator.cs` | Covered |
| MessageStoreGenerator | `src/PatternKit.Generators/Messaging/MessageStoreGenerator.cs` | Covered |
| MessageTranslatorGenerator | `src/PatternKit.Generators/Messaging/MessageTranslatorGenerator.cs` | Covered |
| MessagingGatewayGenerator | `src/PatternKit.Generators/Messaging/MessagingGatewayGenerator.cs` | Covered |
| PipesAndFiltersPipelineGenerator | `src/PatternKit.Generators/Messaging/PipesAndFiltersPipelineGenerator.cs` | Covered |
| PollingConsumerGenerator | `src/PatternKit.Generators/Messaging/PollingConsumerGenerator.cs` | Covered |
| RecipientListGenerator | `src/PatternKit.Generators/Messaging/RecipientListGenerator.cs` | Covered |
| ReliabilityPipelineGenerator | `src/PatternKit.Generators/Messaging/ReliabilityPipelineGenerator.cs` | Covered |
| ResequencerGenerator | `src/PatternKit.Generators/Messaging/ResequencerGenerator.cs` | Covered |
| RoutingSlipGenerator | `src/PatternKit.Generators/Messaging/RoutingSlipGenerator.cs` | Covered |
| SagaGenerator | `src/PatternKit.Generators/Messaging/SagaGenerator.cs` | Covered |
| ScatterGatherGenerator | `src/PatternKit.Generators/Messaging/ScatterGatherGenerator.cs` | Covered |
| ServiceActivatorGenerator | `src/PatternKit.Generators/Messaging/ServiceActivatorGenerator.cs` | Covered |
| SplitterAggregatorGenerator | `src/PatternKit.Generators/Messaging/SplitterAggregatorGenerator.cs` | Covered |
| WireTapGenerator | `src/PatternKit.Generators/Messaging/WireTapGenerator.cs` | Covered |
| ObserverGenerator | `src/PatternKit.Generators/Observer/ObserverGenerator.cs` | Covered |
| PriorityQueueGenerator | `src/PatternKit.Generators/PriorityQueue/PriorityQueueGenerator.cs` | Covered |
| PrototypeGenerator | `src/PatternKit.Generators/PrototypeGenerator.cs` | Covered |
| ProxyGenerator | `src/PatternKit.Generators/ProxyGenerator.cs` | Covered |
| QueueLoadLevelingPolicyGenerator | `src/PatternKit.Generators/QueueLoadLeveling/QueueLoadLevelingPolicyGenerator.cs` | Covered |
| RateLimitPolicyGenerator | `src/PatternKit.Generators/RateLimiting/RateLimitPolicyGenerator.cs` | Covered |
| RepositoryGenerator | `src/PatternKit.Generators/Repository/RepositoryGenerator.cs` | Covered |
| RetryPolicyGenerator | `src/PatternKit.Generators/Retry/RetryPolicyGenerator.cs` | Covered |
| SchedulerAgentSupervisorGenerator | `src/PatternKit.Generators/SchedulerAgentSupervisor/SchedulerAgentSupervisorGenerator.cs` | Covered |
| ServiceLayerOperationGenerator | `src/PatternKit.Generators/ServiceLayer/ServiceLayerOperationGenerator.cs` | Covered |
| SidecarGenerator | `src/PatternKit.Generators/Sidecar/SidecarGenerator.cs` | Covered |
| SingletonGenerator | `src/PatternKit.Generators/Singleton/SingletonGenerator.cs` | Covered |
| SpecificationGenerator | `src/PatternKit.Generators/Specification/SpecificationGenerator.cs` | Covered |
| StateMachineGenerator | `src/PatternKit.Generators/StateMachineGenerator.cs` | Covered |
| StranglerFigGenerator | `src/PatternKit.Generators/StranglerFig/StranglerFigGenerator.cs` | Covered |
| StrategyGenerator | `src/PatternKit.Generators/StrategyGenerator.cs` | Covered |
| TableDataGatewayGenerator | `src/PatternKit.Generators/TableDataGateway/TableDataGatewayGenerator.cs` | Covered |
| TemplateGenerator | `src/PatternKit.Generators/TemplateGenerator.cs` | Covered |
| TransactionScriptGenerator | `src/PatternKit.Generators/TransactionScript/TransactionScriptGenerator.cs` | Covered |
| UnitOfWorkGenerator | `src/PatternKit.Generators/UnitOfWork/UnitOfWorkGenerator.cs` | Covered |
| VisitorGenerator | `src/PatternKit.Generators/VisitorGenerator.cs` | Covered |

## Reproducing Results

Run the scenario benchmarks:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *LeaderElection* --artifacts artifacts/benchmarks --join
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *SchedulerAgentSupervisor* --artifacts artifacts/benchmarks --join
```

Run the full reportable benchmark suite:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --artifacts artifacts/benchmarks --join
```

Run only the matrix routes when validating benchmark coverage changes:

```powershell
dotnet run -c Release --framework net10.0 --project benchmarks/PatternKit.Benchmarks -- --filter *Matrix* --artifacts artifacts/benchmarks --join --job short
```
