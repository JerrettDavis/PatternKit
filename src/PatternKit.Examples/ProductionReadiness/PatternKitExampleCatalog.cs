using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PatternKit.Examples.ProductionReadiness;

/// <summary>
/// Integration surfaces demonstrated by an example entry.
/// </summary>
[Flags]
public enum ExampleIntegrationSurface
{
    None = 0,
    LibraryOnly = 1,
    DependencyInjection = 2,
    Options = 4,
    GenericHost = 8,
    AspNetCore = 16,
    SourceGenerator = 32,
    Messaging = 64,
    ExternalInfrastructure = 128
}

/// <summary>
/// Describes a production-shaped PatternKit example, its tests, and its documentation.
/// </summary>
public sealed record PatternKitExampleDescriptor(
    string Name,
    string SourcePath,
    string TestPath,
    string DocumentationPath,
    ExampleIntegrationSurface Integration,
    IReadOnlyList<string> Patterns,
    IReadOnlyList<string> ProductionChecks);

/// <summary>
/// A validation issue found while auditing example metadata and optional repository files.
/// </summary>
public sealed record PatternKitExampleValidationIssue(
    string ExampleName,
    string Field,
    string Message);

/// <summary>
/// Validation report for the example catalog.
/// </summary>
public sealed record PatternKitExampleValidationReport(
    IReadOnlyList<PatternKitExampleDescriptor> Entries,
    IReadOnlyList<PatternKitExampleValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>
/// Runtime validation options for the examples catalog.
/// </summary>
public sealed class PatternKitExampleCatalogOptions
{
    /// <summary>
    /// Optional repository root. When supplied, validation checks that source, test, and documentation paths exist.
    /// </summary>
    public string? RepositoryRoot { get; set; }

    /// <summary>
    /// Throws during hosted startup when validation fails.
    /// </summary>
    public bool FailOnInvalid { get; set; } = true;
}

/// <summary>
/// Read-only manifest of production-shaped PatternKit examples.
/// </summary>
public interface IPatternKitExampleCatalog
{
    IReadOnlyList<PatternKitExampleDescriptor> Entries { get; }

    PatternKitExampleValidationReport Validate(string? repositoryRoot = null);
}

/// <summary>
/// Default example catalog used by docs, tests, hosts, and ASP.NET Core endpoints.
/// </summary>
public sealed class PatternKitExampleCatalog : IPatternKitExampleCatalog
{
    private static readonly IReadOnlyList<PatternKitExampleDescriptor> Items =
    [
        Descriptor(
            "Production-Ready Example Integrations",
            "src/PatternKit.Examples/ProductionReadiness/PatternKitExampleCatalog.cs",
            "test/PatternKit.Examples.Tests/ProductionReadiness/PatternKitExampleCatalogTests.cs",
            "docs/examples/production-ready-integrations.md",
            ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore,
            ["Catalog", "Facade"],
            ["source/test/docs manifest", "host startup validation", "minimal API diagnostics"]),
        Descriptor(
            "Auth & Logging Chain",
            "src/PatternKit.Examples/Chain/AuthLoggingDemo.cs",
            "test/PatternKit.Examples.Tests/Chain/AuthLoggingDemoTests.cs",
            "docs/examples/auth-logging-chain.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["ActionChain"],
            ["request logging", "authorization short-circuit", "strict stop semantics"]),
        Descriptor(
            "Strategy-Based Data Coercion",
            "src/PatternKit.Examples/Strategies/Coercion/Coercer.cs",
            "test/PatternKit.Examples.Tests/Strategies/Coercion/CoercerTests.cs",
            "docs/examples/coercer.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["TryStrategy", "Strategy"],
            ["culture-safe conversions", "JSON and primitive coercion", "invalid input handling"]),
        Descriptor(
            "Composed Notification Strategy",
            "src/PatternKit.Examples/Strategies/Composed/ComposedStrategies.cs",
            "test/PatternKit.Examples.Tests/Strategies/Composed/ComposedStrategiesTests.cs",
            "docs/examples/composed-notification-strategy.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["AsyncStrategy", "Strategy"],
            ["preference ordering", "fallback channels", "rate and identity gates"]),
        Descriptor(
            "Mediated Transaction Pipeline",
            "src/PatternKit.Examples/Chain/MediatedTransactionPipelineDemo.cs",
            "test/PatternKit.Examples.Tests/Chain/MediatedTransactionPipelineDemoTests.cs",
            "docs/examples/mediated-transaction-pipeline.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["ActionChain", "Strategy", "TryStrategy"],
            ["pre-authorization", "discounting", "tender handling"]),
        Descriptor(
            "Configuration-Driven Transaction Pipeline",
            "src/PatternKit.Examples/Chain/ConfigDriven/TransactionPipelineDemo.cs",
            "test/PatternKit.Examples.Tests/Chain/TransactionPipelineDemoTests.cs",
            "docs/examples/config-driven-transaction-pipeline.md",
            ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.Options,
            ["ActionChain", "BranchBuilder", "Strategy"],
            ["IOptions validation", "configurable rule order", "DI composition"]),
        Descriptor(
            "Enterprise Feature Slices with .NET DI",
            "src/PatternKit.Examples/EnterpriseFeatureSlices/EnterpriseFeatureSlicesDemo.cs",
            "test/PatternKit.Examples.Tests/EnterpriseFeatureSlices/EnterpriseFeatureSlicesDemoTests.cs",
            "docs/examples/enterprise-feature-slices.md",
            ExampleIntegrationSurface.DependencyInjection,
            ["Flyweight", "Factory", "Prototype", "ResultChain", "Strategy", "Decorator", "Proxy", "Facade"],
            ["container-owned artifacts", "typed facade", "payment and fulfillment validation"]),
        Descriptor(
            "Minimal Web Request Router",
            "src/PatternKit.Examples/ApiGateway/MiniRouter.cs",
            "test/PatternKit.Examples.Tests/ApiGateway/ApiGatewayTests.cs",
            "docs/examples/mini-router.md",
            ExampleIntegrationSurface.AspNetCore,
            ["Strategy", "ActionChain"],
            ["middleware ordering", "content negotiation", "route matching"]),
        Descriptor(
            "Payment Processor Decorator",
            "src/PatternKit.Examples/PointOfSale/PaymentProcessorDemo.cs",
            "test/PatternKit.Examples.Tests/PointOfSale/PaymentProcessorTests.cs",
            "docs/examples/payment-processor-decorator.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Decorator"],
            ["input validation", "discount layering", "receipt audit trail"]),
        Descriptor(
            "POS App State Singleton",
            "src/PatternKit.Examples/Singleton/PosAppStateDemo.cs",
            "test/PatternKit.Examples.Tests/Singleton/PosAppStateDemoTests.cs",
            "docs/examples/pos-app-state-singleton.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Singleton"],
            ["state reset", "session identity", "cache reuse"]),
        Descriptor(
            "Pricing Calculator",
            "src/PatternKit.Examples/Pricing/Demo.cs",
            "test/PatternKit.Examples.Tests/Pricing/PricingDemoTests.cs",
            "docs/examples/pricing-calculator.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["AsyncStrategy", "ResultChain"],
            ["async source fallback", "loyalty pricing", "rounding rules"]),
        Descriptor(
            "POS Tender Visitor",
            "src/PatternKit.Examples/VisitorDemo/VisitorDemo.cs",
            "test/PatternKit.Examples.Tests/VisitorDemo/VisitorDemoTests.cs",
            "docs/examples/pos-visitor-routing.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Visitor", "TypeDispatcher"],
            ["receipt rendering", "unknown tender fallback", "routing counters"]),
        Descriptor(
            "API Exception Mapping Visitor",
            "src/PatternKit.Examples/Generators/Visitors/DocumentProcessingDemo.cs",
            "test/PatternKit.Examples.Tests/Generators/VisitorGeneratorExamplesTests.cs",
            "docs/examples/api-exception-mapping-visitor.md",
            ExampleIntegrationSurface.AspNetCore,
            ["Visitor"],
            ["ProblemDetails mapping", "middleware boundary", "default error shape"]),
        Descriptor(
            "Event Processing Visitor",
            "src/PatternKit.Examples/Generators/Visitors/DocumentProcessingDemo.cs",
            "test/PatternKit.Examples.Tests/Generators/VisitorGeneratorExamplesTests.cs",
            "docs/examples/event-processor-visitor.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["AsyncVisitor", "Visitor"],
            ["domain event routing", "orchestration", "projection fallback"]),
        Descriptor(
            "Message Router Visitor",
            "src/PatternKit.Examples/Messaging/MessageRoutingExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/MessageRoutingExampleTests.cs",
            "docs/examples/message-router-visitor.md",
            ExampleIntegrationSurface.Messaging,
            ["Visitor", "ContentRouter"],
            ["message dispatch", "route fallback", "typed handlers"]),
        Descriptor(
            "Patterns Showcase",
            "src/PatternKit.Examples/PatternShowcase/PatternShowcase.cs",
            "test/PatternKit.Examples.Tests/PatternShowcase/PatternShowcaseTests.cs",
            "docs/examples/patterns-showcase.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Strategy", "Factory", "Decorator", "Observer", "StateMachine"],
            ["integrated order flow", "audit events", "state transitions"]),
        Descriptor(
            "Source Generator Application Suite",
            "src/PatternKit.Examples/Generators/Builders/CorporateApplicationBuilderDemo/CorporateApplication.cs",
            "test/PatternKit.Examples.Tests/Generators/CorporateApplicationBuilderDemoTests.cs",
            "docs/examples/source-generator-application-suite.md",
            ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Builder", "Factory", "Facade", "Proxy", "Observer", "Memento", "StateMachine", "Strategy", "Visitor"],
            ["host composition", "module ordering", "generated API shape"]),
        Descriptor(
            "Enterprise Messaging Workflow Suite",
            "src/PatternKit.Examples/Messaging/MessageEnvelopeExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/MessageEnvelopeExampleTests.cs",
            "docs/examples/enterprise-messaging-workflows.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator,
            ["MessageEnvelope", "ContentRouter", "RecipientList", "Splitter", "Aggregator", "RoutingSlip", "Saga", "Mailbox"],
            ["idempotency", "inbox/outbox", "generated envelope contracts", "generated dispatcher"]),
        Descriptor(
            "Generated Message Envelope",
            "src/PatternKit.Examples/Messaging/MessageEnvelopeExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/MessageEnvelopeExampleTests.cs",
            "docs/examples/generated-message-envelope.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["MessageEnvelope"],
            ["required headers", "source-generated factory", "DI composition"]),
        Descriptor(
            "Generated Message Translator",
            "src/PatternKit.Examples/Messaging/PartnerEventTranslatorExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/PartnerEventTranslatorExampleTests.cs",
            "docs/examples/generated-message-translator.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["MessageTranslator"],
            ["partner event normalization", "source-generated translator", "DI composition"]),
        Descriptor(
            "Generated Claim Check",
            "src/PatternKit.Examples/Messaging/LargeDocumentClaimCheckExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/LargeDocumentClaimCheckExampleTests.cs",
            "docs/examples/generated-claim-check.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["ClaimCheck"],
            ["external payload storage", "source-generated claim check", "DI composition"]),
        Descriptor(
            "Generated Dead Letter Channel",
            "src/PatternKit.Examples/Messaging/FulfillmentDeadLetterChannelExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/FulfillmentDeadLetterChannelExampleTests.cs",
            "docs/examples/generated-dead-letter-channel.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["DeadLetterChannel"],
            ["failed message capture", "source-generated dead-letter channel", "DI composition"]),
        Descriptor(
            "Generated Recipient List",
            "src/PatternKit.Examples/Messaging/RecipientListGeneratorExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/RecipientListGeneratorExampleTests.cs",
            "docs/examples/generated-recipient-list.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["RecipientList"],
            ["fan-out routing", "source-generated factory", "DI composition"]),
        Descriptor(
            "Generated Splitter and Aggregator",
            "src/PatternKit.Examples/Messaging/MessageRoutingExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/MessageRoutingExampleTests.cs",
            "docs/examples/generated-splitter-aggregator.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Splitter", "Aggregator"],
            ["split/rejoin routing", "source-generated factories", "DI composition"]),
        Descriptor(
            "CQRS Dispatcher",
            "src/PatternKit.Examples/Messaging/CqrsPatternExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/CqrsPatternExampleTests.cs",
            "docs/examples/cqrs-dispatcher.md",
            ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.GenericHost,
            ["Mediator", "Dispatcher", "CQRS"],
            ["command/query separation", "source-generated dispatcher", "DI composition"]),
        Descriptor(
            "Loan Approval Specifications",
            "src/PatternKit.Examples/SpecificationDemo/LoanApprovalSpecificationDemo.cs",
            "test/PatternKit.Examples.Tests/SpecificationDemo/LoanApprovalSpecificationDemoTests.cs",
            "docs/examples/loan-approval-specifications.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Specification"],
            ["composable business rules", "source-generated registry", "DI composition"]),
        Descriptor(
            "Order Repository Pattern",
            "src/PatternKit.Examples/RepositoryDemo/OrderRepositoryDemo.cs",
            "test/PatternKit.Examples.Tests/RepositoryDemo/OrderRepositoryDemoTests.cs",
            "docs/examples/order-repository-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Repository"],
            ["collection-like persistence boundary", "source-generated repository factory", "DI composition"]),
        Descriptor(
            "Checkout Unit of Work Pattern",
            "src/PatternKit.Examples/UnitOfWorkDemo/CheckoutUnitOfWorkDemo.cs",
            "test/PatternKit.Examples.Tests/UnitOfWorkDemo/CheckoutUnitOfWorkDemoTests.cs",
            "docs/examples/checkout-unit-of-work-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["UnitOfWork"],
            ["ordered commit boundary", "source-generated unit of work", "DI composition"]),
        Descriptor(
            "Order Data Mapper Pattern",
            "src/PatternKit.Examples/DataMapperDemo/OrderDataMapperDemo.cs",
            "test/PatternKit.Examples.Tests/DataMapperDemo/OrderDataMapperDemoTests.cs",
            "docs/examples/order-data-mapper-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["DataMapper"],
            ["domain/data isolation", "source-generated mapper factory", "DI composition"]),
        Descriptor(
            "Order Identity Map Pattern",
            "src/PatternKit.Examples/IdentityMapDemo/OrderIdentityMapDemo.cs",
            "test/PatternKit.Examples.Tests/IdentityMapDemo/OrderIdentityMapDemoTests.cs",
            "docs/examples/order-identity-map-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["IdentityMap"],
            ["request-scoped identity reuse", "source-generated map factory", "DI composition"]),
        Descriptor(
            "Order Transaction Script Pattern",
            "src/PatternKit.Examples/TransactionScriptDemo/OrderTransactionScriptDemo.cs",
            "test/PatternKit.Examples.Tests/TransactionScriptDemo/OrderTransactionScriptDemoTests.cs",
            "docs/examples/order-transaction-script-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["TransactionScript"],
            ["explicit application workflow", "source-generated script factory", "DI composition"]),
        Descriptor(
            "Customer Service Layer Pattern",
            "src/PatternKit.Examples/ServiceLayerDemo/CustomerServiceLayerDemo.cs",
            "test/PatternKit.Examples.Tests/ServiceLayerDemo/CustomerServiceLayerDemoTests.cs",
            "docs/examples/customer-service-layer-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["ServiceLayer"],
            ["application operation boundary", "source-generated operation factory", "DI composition"]),
        Descriptor(
            "Order Domain Event Pattern",
            "src/PatternKit.Examples/DomainEventDemo/OrderDomainEventDemo.cs",
            "test/PatternKit.Examples.Tests/DomainEventDemo/OrderDomainEventDemoTests.cs",
            "docs/examples/order-domain-event-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["DomainEvent"],
            ["aggregate event dispatch", "source-generated dispatcher factory", "DI composition"]),
        Descriptor(
            "Order Table Data Gateway Pattern",
            "src/PatternKit.Examples/TableDataGatewayDemo/OrderTableDataGatewayDemo.cs",
            "test/PatternKit.Examples.Tests/TableDataGatewayDemo/OrderTableDataGatewayDemoTests.cs",
            "docs/examples/order-table-data-gateway-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["TableDataGateway"],
            ["row-oriented table boundary", "source-generated gateway factory", "DI composition"]),
        Descriptor(
            "Generated Mailbox",
            "src/PatternKit.Examples/Messaging/MailboxExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/MailboxExampleTests.cs",
            "docs/examples/generated-mailbox.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Mailbox"],
            ["serialized inbox", "source-generated factory", "DI composition"]),
        Descriptor(
            "Generated Reliability Pipeline",
            "src/PatternKit.Examples/Messaging/ReliabilityExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/ReliabilityExampleTests.cs",
            "docs/examples/generated-reliability-pipeline.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["IdempotentReceiver", "Inbox", "Outbox"],
            ["duplicate suppression", "source-generated factories", "DI composition"]),
        Descriptor(
            "Resilient Checkout and Collaborating Mailboxes",
            "src/PatternKit.Examples/Messaging/ResilientCheckoutDemo.cs",
            "test/PatternKit.Examples.Tests/Messaging/ResilientCheckoutDemoTests.cs",
            "docs/examples/resilient-checkout-and-mailboxes.md",
            ExampleIntegrationSurface.Messaging,
            ["RoutingSlip", "Saga", "Mailbox", "Command"],
            ["compensation", "fallback routing", "correlated messages"]),
        Descriptor(
            "Messaging Backplane Facade",
            "src/PatternKit.Examples/Messaging/BackplaneFacadeDemo.cs",
            "test/PatternKit.Examples.Tests/Messaging/BackplaneFacadeDemoTests.cs",
            "docs/examples/messaging-backplane-facade.md",
            ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.ExternalInfrastructure,
            ["Facade", "Mailbox", "Outbox", "IdempotentReceiver"],
            ["host setup", "generated request/reply topology", "generated pub/sub topology", "transport boundary"]),
        Descriptor(
            "Abstract Factory Widget Families",
            "src/PatternKit.Examples/AbstractFactoryDemo/AbstractFactoryDemo.cs",
            "test/PatternKit.Examples.Tests/AbstractFactoryDemo/AbstractFactoryDemoTests.cs",
            "docs/examples/abstract-factory-widget-families.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["AbstractFactory"],
            ["generated family factory", "platform widgets", "DI composition"]),
        Descriptor(
            "Prototype Game Character Factory",
            "src/PatternKit.Examples/PrototypeDemo/PrototypeDemo.cs",
            "test/PatternKit.Examples.Tests/PrototypeDemo/PrototypeDemoTests.cs",
            "docs/examples/prototype-demo.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Prototype"],
            ["clone registry", "per-call mutation", "default family"]),
        Descriptor(
            "Proxy Pattern Demonstrations",
            "src/PatternKit.Examples/ProxyDemo/ProxyDemo.cs",
            "test/PatternKit.Examples.Tests/ProxyDemo/ProxyDemoTests.cs",
            "docs/examples/proxy-demo.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Proxy"],
            ["virtual proxy", "protection proxy", "caching proxy", "remote proxy"]),
        Descriptor(
            "Flyweight Glyph Cache",
            "src/PatternKit.Examples/FlyweightDemo/FlyweightDemo.cs",
            "test/PatternKit.Examples.Tests/FlyweightDemos/FlyweightDemoTests.cs",
            "docs/examples/flyweight-glyph-cache.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Flyweight"],
            ["identity sharing", "case-insensitive styles", "layout reuse"]),
        Descriptor(
            "Text Editor Memento",
            "src/PatternKit.Examples/MementoDemo/MementoDemo.cs",
            "test/PatternKit.Examples.Tests/MementoDemo/MementoDemoTests.cs",
            "docs/examples/text-editor-memento.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Memento"],
            ["undo", "redo", "jump-to-version"]),
        Descriptor(
            "Observer Event Hub",
            "src/PatternKit.Examples/ObserverDemo/SimpleEventHub.cs",
            "test/PatternKit.Examples.Tests/ObserverDemo/EventHubTests.cs",
            "docs/examples/observer-demo.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Observer"],
            ["subscription routing", "error handling", "event fan-out"]),
        Descriptor(
            "Reactive ViewModel",
            "src/PatternKit.Examples/ObserverDemo/ReactivePrimitives.cs",
            "test/PatternKit.Examples.Tests/ObserverDemo/ReactiveViewModelTests.cs",
            "docs/examples/reactive-viewmodel.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Observer"],
            ["dependent properties", "command enablement", "event ordering"]),
        Descriptor(
            "Reactive Transaction",
            "src/PatternKit.Examples/ObserverDemo/ReactiveTransaction.cs",
            "test/PatternKit.Examples.Tests/ObserverDemo/ReactiveTransactionTests.cs",
            "docs/examples/reactive-transaction.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["Observer", "Strategy"],
            ["dynamic discounts", "tax recomputation", "total projection"]),
        Descriptor(
            "Async Connection State Machine",
            "src/PatternKit.Examples/AsyncStateDemo/AsyncStateDemo.cs",
            "test/PatternKit.Examples.Tests/AsyncStateDemo/AsyncStateDemoTests.cs",
            "docs/examples/async-state-machine.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["AsyncStateMachine"],
            ["cancellation", "async effects", "transition ordering"]),
        Descriptor(
            "Template Method Subclassing",
            "src/PatternKit.Examples/TemplateDemo/TemplateDemo.cs",
            "test/PatternKit.Examples.Tests/TemplateDemo/TemplateDemoTests.cs",
            "docs/examples/template-method-demo.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["TemplateMethod"],
            ["hooks", "validation", "workflow reuse"]),
        Descriptor(
            "Template Method Async",
            "src/PatternKit.Examples/TemplateDemo/TemplateAsyncDemo.cs",
            "test/PatternKit.Examples.Tests/TemplateDemo/TemplateDemoTests.cs",
            "docs/examples/template-method-async-demo.md",
            ExampleIntegrationSurface.LibraryOnly,
            ["AsyncTemplate", "AsyncTemplateMethod"],
            ["cancellation", "async storage", "error observation"]),
        Descriptor(
            "Legacy Order Anti-Corruption Layer",
            "src/PatternKit.Examples/AntiCorruptionDemo/LegacyOrderAntiCorruptionDemo.cs",
            "test/PatternKit.Examples.Tests/AntiCorruptionDemo/LegacyOrderAntiCorruptionDemoTests.cs",
            "docs/examples/legacy-order-anti-corruption-layer.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Anti-Corruption Layer"],
            ["external model validation", "domain normalization", "DI composition"]),
        Descriptor(
            "Inventory Retry Policy",
            "src/PatternKit.Examples/RetryDemo/InventoryRetryDemo.cs",
            "test/PatternKit.Examples.Tests/RetryDemo/InventoryRetryDemoTests.cs",
            "docs/examples/inventory-retry-policy.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Retry"],
            ["transient result retry", "source-generated policy factory", "DI composition"]),
        Descriptor(
            "Fulfillment Circuit Breaker",
            "src/PatternKit.Examples/CircuitBreakerDemo/FulfillmentCircuitBreakerDemo.cs",
            "test/PatternKit.Examples.Tests/CircuitBreakerDemo/FulfillmentCircuitBreakerDemoTests.cs",
            "docs/examples/fulfillment-circuit-breaker.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Circuit Breaker"],
            ["dependency isolation", "source-generated policy factory", "DI composition"]),
        Descriptor(
            "Shipping Bulkhead",
            "src/PatternKit.Examples/BulkheadDemo/ShippingBulkheadDemo.cs",
            "test/PatternKit.Examples.Tests/BulkheadDemo/ShippingBulkheadDemoTests.cs",
            "docs/examples/shipping-bulkhead.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Bulkhead"],
            ["concurrency isolation", "source-generated policy factory", "DI composition"]),
        Descriptor(
            "Product Catalog Cache-Aside",
            "src/PatternKit.Examples/CacheAsideDemo/ProductCatalogCacheAsideDemo.cs",
            "test/PatternKit.Examples.Tests/CacheAsideDemo/ProductCatalogCacheAsideDemoTests.cs",
            "docs/examples/product-catalog-cache-aside.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Cache-Aside"],
            ["read-through cache miss handling", "source-generated policy factory", "DI composition"]),
        Descriptor(
            "Product Search Rate Limiting",
            "src/PatternKit.Examples/RateLimitingDemo/ProductSearchRateLimitingDemo.cs",
            "test/PatternKit.Examples.Tests/RateLimitingDemo/ProductSearchRateLimitingDemoTests.cs",
            "docs/examples/product-search-rate-limiting.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Rate Limiting"],
            ["tenant partitioning", "source-generated policy factory", "DI composition"])
    ];

    public IReadOnlyList<PatternKitExampleDescriptor> Entries => Items;

    public PatternKitExampleValidationReport Validate(string? repositoryRoot = null)
    {
        var issues = new List<PatternKitExampleValidationIssue>();

        foreach (var entry in Entries)
        {
            CheckRequired(entry, entry.Name, nameof(entry.Name), issues);
            CheckRequired(entry, entry.SourcePath, nameof(entry.SourcePath), issues);
            CheckRequired(entry, entry.TestPath, nameof(entry.TestPath), issues);
            CheckRequired(entry, entry.DocumentationPath, nameof(entry.DocumentationPath), issues);

            if (entry.Patterns.Count == 0)
                issues.Add(new(entry.Name, nameof(entry.Patterns), "At least one PatternKit pattern must be listed."));

            if (entry.ProductionChecks.Count == 0)
                issues.Add(new(entry.Name, nameof(entry.ProductionChecks), "At least one production check must be listed."));

            if (entry.Integration == ExampleIntegrationSurface.None)
                issues.Add(new(entry.Name, nameof(entry.Integration), "At least one integration surface must be listed."));

            if (!string.IsNullOrWhiteSpace(repositoryRoot))
            {
                CheckFile(repositoryRoot, entry, nameof(entry.SourcePath), entry.SourcePath, issues);
                CheckFile(repositoryRoot, entry, nameof(entry.TestPath), entry.TestPath, issues);
                CheckFile(repositoryRoot, entry, nameof(entry.DocumentationPath), entry.DocumentationPath, issues);
            }
        }

        return new PatternKitExampleValidationReport(Entries, issues);
    }

    private static PatternKitExampleDescriptor Descriptor(
        string name,
        string sourcePath,
        string testPath,
        string documentationPath,
        ExampleIntegrationSurface integration,
        IReadOnlyList<string> patterns,
        IReadOnlyList<string> productionChecks)
        => new(name, sourcePath, testPath, documentationPath, integration, patterns, productionChecks);

    private static void CheckRequired(
        PatternKitExampleDescriptor entry,
        string value,
        string field,
        ICollection<PatternKitExampleValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            issues.Add(new(entry.Name, field, "Value is required."));
    }

    private static void CheckFile(
        string repositoryRoot,
        PatternKitExampleDescriptor entry,
        string field,
        string relativePath,
        ICollection<PatternKitExampleValidationIssue> issues)
    {
        var fullPath = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(fullPath))
            issues.Add(new(entry.Name, field, $"File does not exist: {relativePath}"));
    }
}

/// <summary>
/// Service registration helpers for importing the examples catalog into standard .NET hosts.
/// </summary>
public static class PatternKitExampleCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddPatternKitExampleCatalog(
        this IServiceCollection services,
        Action<PatternKitExampleCatalogOptions>? configure = null)
    {
        services.AddOptions<PatternKitExampleCatalogOptions>();
        if (configure is not null)
            services.Configure(configure);

        services.AddSingleton<IPatternKitExampleCatalog, PatternKitExampleCatalog>();
        return services;
    }

    public static IHostApplicationBuilder AddPatternKitExampleCatalog(
        this IHostApplicationBuilder builder,
        Action<PatternKitExampleCatalogOptions>? configure = null)
    {
        builder.Services.AddPatternKitExampleCatalog(configure);
        return builder;
    }

    public static IHostApplicationBuilder AddPatternKitExampleHostedValidation(
        this IHostApplicationBuilder builder,
        Action<PatternKitExampleCatalogOptions>? configure = null)
    {
        builder.Services.AddPatternKitExampleCatalog(configure);
        builder.Services.AddHostedService<PatternKitExampleCatalogHostedValidator>();
        return builder;
    }
}

/// <summary>
/// Hosted startup validator for production hosts that want example metadata failures to fail fast.
/// </summary>
public sealed class PatternKitExampleCatalogHostedValidator(
    IPatternKitExampleCatalog catalog,
    IOptions<PatternKitExampleCatalogOptions> options,
    ILogger<PatternKitExampleCatalogHostedValidator> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var report = catalog.Validate(options.Value.RepositoryRoot);

        if (report.IsValid)
        {
            logger.LogInformation("PatternKit example catalog validated {Count} entries.", report.Entries.Count);
            return Task.CompletedTask;
        }

        foreach (var issue in report.Issues)
            logger.LogError("PatternKit example catalog issue in {Example} ({Field}): {Message}", issue.ExampleName, issue.Field, issue.Message);

        if (options.Value.FailOnInvalid)
            throw new InvalidOperationException($"PatternKit example catalog validation failed with {report.Issues.Count} issue(s).");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Minimal API integration for exposing the example catalog in ASP.NET Core applications.
/// </summary>
public static class PatternKitExampleCatalogEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapPatternKitExampleCatalog(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/patternkit/examples")
    {
        endpoints.MapGet(pattern, (IPatternKitExampleCatalog catalog) => Results.Ok(catalog.Entries))
            .WithName("PatternKitExampleCatalog");

        endpoints.MapGet($"{pattern}/validation", ValidateCatalog)
            .WithName("PatternKitExampleCatalogValidation");

        return endpoints;
    }

    private static IResult ValidateCatalog(
        IPatternKitExampleCatalog catalog,
        IOptions<PatternKitExampleCatalogOptions> options)
    {
        var report = catalog.Validate(options.Value.RepositoryRoot);
        return report.IsValid ? Results.Ok(report) : Results.Problem(
            title: "PatternKit example catalog validation failed",
            detail: $"{report.Issues.Count} issue(s) found.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
