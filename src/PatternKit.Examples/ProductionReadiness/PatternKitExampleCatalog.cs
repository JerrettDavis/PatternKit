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
            "Inventory Message Channel",
            "src/PatternKit.Examples/Messaging/InventoryMessageChannelExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/InventoryMessageChannelExampleTests.cs",
            "docs/examples/inventory-message-channel.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["MessageChannel"],
            ["typed queue boundary", "source-generated channel factory", "DI composition"]),
        Descriptor(
            "Inventory Channel Purger",
            "src/PatternKit.Examples/Messaging/InventoryChannelPurgerExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/InventoryChannelPurgerExampleTests.cs",
            "docs/examples/inventory-channel-purger.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["ChannelPurger", "MessageChannel"],
            ["operational backlog purge", "source-generated purger factory", "DI composition"]),
        Descriptor(
            "Order Invalid Message Channel",
            "src/PatternKit.Examples/Messaging/OrderInvalidMessageChannelExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/OrderInvalidMessageChannelExampleTests.cs",
            "docs/examples/order-invalid-message-channel.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["InvalidMessageChannel", "MessageChannel"],
            ["validation failure routing", "source-generated invalid channel builder", "DI composition"]),
        Descriptor(
            "Warehouse Polling Consumer",
            "src/PatternKit.Examples/Messaging/WarehousePollingConsumerExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/WarehousePollingConsumerExampleTests.cs",
            "docs/examples/warehouse-polling-consumer.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["PollingConsumer", "MessageChannel"],
            ["pull-based replenishment workflow", "source-generated polling consumer factory", "DI composition"]),
        Descriptor(
            "Order Event-Driven Consumer",
            "src/PatternKit.Examples/Messaging/OrderEventDrivenConsumerExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/OrderEventDrivenConsumerExampleTests.cs",
            "docs/examples/order-event-driven-consumer.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["EventDrivenConsumer"],
            ["push-based order workflow", "source-generated event handler factory", "DI composition"]),
        Descriptor(
            "ERP Channel Adapter",
            "src/PatternKit.Examples/Messaging/ErpChannelAdapterExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/ErpChannelAdapterExampleTests.cs",
            "docs/examples/erp-channel-adapter.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["ChannelAdapter", "MessageChannel"],
            ["external ERP DTO bridge", "source-generated adapter factory", "DI composition"]),
        Descriptor(
            "Payment Messaging Gateway",
            "src/PatternKit.Examples/Messaging/PaymentMessagingGatewayExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/PaymentMessagingGatewayExampleTests.cs",
            "docs/examples/payment-messaging-gateway.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["MessagingGateway", "MessageChannel"],
            ["typed authorization gateway", "source-generated gateway factory", "DI composition"]),
        Descriptor(
            "Inventory Service Activator",
            "src/PatternKit.Examples/Messaging/InventoryServiceActivatorExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/InventoryServiceActivatorExampleTests.cs",
            "docs/examples/inventory-service-activator.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["ServiceActivator"],
            ["message-to-service operation", "source-generated activator factory", "DI composition"]),
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
            "Order Canonical Data Model",
            "src/PatternKit.Examples/CanonicalDataModelDemo/OrderCanonicalDataModelDemo.cs",
            "test/PatternKit.Examples.Tests/CanonicalDataModelDemo/OrderCanonicalDataModelDemoTests.cs",
            "docs/examples/order-canonical-data-model.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Canonical Data Model"],
            ["partner order normalization", "source-generated canonical adapter", "DI composition"]),
        Descriptor(
            "Inventory Event-Carried State Transfer",
            "src/PatternKit.Examples/EventCarriedStateTransferDemo/InventoryEventCarriedStateTransferDemo.cs",
            "test/PatternKit.Examples.Tests/EventCarriedStateTransferDemo/InventoryEventCarriedStateTransferDemoTests.cs",
            "docs/examples/inventory-event-carried-state-transfer.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Event-Carried State Transfer"],
            ["inventory read-model projection", "source-generated state transfer", "DI composition"]),
        Descriptor(
            "Order Event Notification",
            "src/PatternKit.Examples/EventNotificationDemo/OrderEventNotificationDemo.cs",
            "test/PatternKit.Examples.Tests/EventNotificationDemo/OrderEventNotificationDemoTests.cs",
            "docs/examples/order-event-notification.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Event Notification"],
            ["compact order notification", "source-generated notification", "DI composition"]),
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
            "Order Message Filter",
            "src/PatternKit.Examples/Messaging/OrderMessageFilterExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/OrderMessageFilterExampleTests.cs",
            "docs/examples/order-message-filter.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["MessageFilter"],
            ["fraud-screening allow rules", "source-generated filter", "DI composition"]),
        Descriptor(
            "Order Message Store",
            "src/PatternKit.Examples/Messaging/OrderMessageStoreExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/OrderMessageStoreExampleTests.cs",
            "docs/examples/order-message-store.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["MessageStore"],
            ["audit persistence", "source-generated identity and retention", "DI composition"]),
        Descriptor(
            "Order Durable Subscriber",
            "src/PatternKit.Examples/Messaging/OrderDurableSubscriberExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/OrderDurableSubscriberExampleTests.cs",
            "docs/examples/order-durable-subscriber.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["DurableSubscriber", "MessageStore"],
            ["checkpointed replay", "source-generated subscription handler", "DI composition"]),
        Descriptor(
            "Order Dynamic Router",
            "src/PatternKit.Examples/Messaging/OrderDynamicRouterExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/OrderDynamicRouterExampleTests.cs",
            "docs/examples/order-dynamic-router.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["DynamicRouter"],
            ["runtime route replacement", "source-generated initial route table", "DI composition"]),
        Descriptor(
            "Order Message Bus",
            "src/PatternKit.Examples/Messaging/OrderMessageBusExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/OrderMessageBusExampleTests.cs",
            "docs/examples/order-message-bus.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["MessageBus", "MessageChannel"],
            ["topic bus", "source-generated topology", "DI composition"]),
        Descriptor(
            "Order Wire Tap",
            "src/PatternKit.Examples/Messaging/OrderWireTapExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/OrderWireTapExampleTests.cs",
            "docs/examples/order-wire-tap.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["WireTap"],
            ["audit side channel", "metrics side channel", "source-generated tap factory", "DI composition"]),
        Descriptor(
            "Fulfillment Control Bus",
            "src/PatternKit.Examples/Messaging/FulfillmentControlBusExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/FulfillmentControlBusExampleTests.cs",
            "docs/examples/fulfillment-control-bus.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["ControlBus"],
            ["operational command surface", "source-generated bus factory", "DI composition"]),
        Descriptor(
            "Supplier Quote Scatter-Gather",
            "src/PatternKit.Examples/Messaging/SupplierQuoteScatterGatherExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/SupplierQuoteScatterGatherExampleTests.cs",
            "docs/examples/supplier-quote-scatter-gather.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["ScatterGather"],
            ["supplier quote fan-out", "source-generated scatter-gather factory", "DI composition"]),
        Descriptor(
            "Shipment Resequencer",
            "src/PatternKit.Examples/Messaging/ShipmentResequencerExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/ShipmentResequencerExampleTests.cs",
            "docs/examples/shipment-resequencer.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Resequencer"],
            ["out-of-order shipment buffering", "source-generated resequencer factory", "DI composition"]),
        Descriptor(
            "Fulfillment Competing Consumers",
            "src/PatternKit.Examples/Messaging/FulfillmentCompetingConsumersExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/FulfillmentCompetingConsumersExampleTests.cs",
            "docs/examples/fulfillment-competing-consumers.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["CompetingConsumers"],
            ["shared work stream", "source-generated builder", "DI composition"]),
        Descriptor(
            "Fulfillment Pipes and Filters",
            "src/PatternKit.Examples/Messaging/FulfillmentPipesAndFiltersExample.cs",
            "test/PatternKit.Examples.Tests/Messaging/FulfillmentPipesAndFiltersExampleTests.cs",
            "docs/examples/fulfillment-pipes-and-filters.md",
            ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["PipesAndFilters"],
            ["ordered workflow filters", "source-generated builder", "DI composition"]),
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
            "Order Event Sourcing Pattern",
            "src/PatternKit.Examples/EventSourcingDemo/OrderEventSourcingDemo.cs",
            "test/PatternKit.Examples.Tests/EventSourcingDemo/OrderEventSourcingDemoTests.cs",
            "docs/examples/order-event-sourcing-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["EventSourcing"],
            ["append-only stream", "source-generated event store factory", "DI composition"]),
        Descriptor(
            "Checkout Feature Toggle Pattern",
            "src/PatternKit.Examples/FeatureToggleDemo/CheckoutFeatureToggleDemo.cs",
            "test/PatternKit.Examples.Tests/FeatureToggleDemo/CheckoutFeatureToggleDemoTests.cs",
            "docs/examples/checkout-feature-toggle-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["FeatureToggle"],
            ["contextual rollout rules", "source-generated toggle set factory", "DI composition"]),
        Descriptor(
            "Order Audit Log Pattern",
            "src/PatternKit.Examples/AuditLogDemo/OrderAuditLogDemo.cs",
            "test/PatternKit.Examples.Tests/AuditLogDemo/OrderAuditLogDemoTests.cs",
            "docs/examples/order-audit-log-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["AuditLog"],
            ["append-only order audit trail", "source-generated audit log factory", "DI composition"]),
        Descriptor(
            "Order Materialized View Pattern",
            "src/PatternKit.Examples/MaterializedViewDemo/OrderMaterializedViewDemo.cs",
            "test/PatternKit.Examples.Tests/MaterializedViewDemo/OrderMaterializedViewDemoTests.cs",
            "docs/examples/order-materialized-view-pattern.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["MaterializedView"],
            ["event-sourced read model", "source-generated projection factory", "DI composition"]),
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
            "Fulfillment Queue Load Leveling",
            "src/PatternKit.Examples/QueueLoadLevelingDemo/FulfillmentQueueLoadLevelingDemo.cs",
            "test/PatternKit.Examples.Tests/QueueLoadLevelingDemo/FulfillmentQueueLoadLevelingDemoTests.cs",
            "docs/examples/fulfillment-queue-load-leveling.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection,
            ["Queue-Based Load Leveling"],
            ["bounded worker queue", "source-generated policy factory", "DI composition"]),
        Descriptor(
            "Fulfillment Health Endpoint Monitoring",
            "src/PatternKit.Examples/HealthEndpointMonitoringDemo/FulfillmentHealthEndpointDemo.cs",
            "test/PatternKit.Examples.Tests/HealthEndpointMonitoringDemo/FulfillmentHealthEndpointDemoTests.cs",
            "docs/examples/fulfillment-health-endpoint-monitoring.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore,
            ["Health Endpoint Monitoring"],
            ["dependency health report", "source-generated endpoint factory", "DI and ASP.NET Core composition"]),
        Descriptor(
            "Fulfillment Priority Queue",
            "src/PatternKit.Examples/PriorityQueueDemo/FulfillmentPriorityQueueDemo.cs",
            "test/PatternKit.Examples.Tests/PriorityQueueDemo/FulfillmentPriorityQueueDemoTests.cs",
            "docs/examples/fulfillment-priority-queue.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Priority Queue"],
            ["business priority ordering", "source-generated priority queue factory", "DI composition"]),
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
            ["tenant partitioning", "source-generated policy factory", "DI composition"]),
        Descriptor(
            "Tenant External Configuration Store",
            "src/PatternKit.Examples/ExternalConfigurationStoreDemo/TenantExternalConfigurationStoreDemo.cs",
            "test/PatternKit.Examples.Tests/ExternalConfigurationStoreDemo/TenantExternalConfigurationStoreDemoTests.cs",
            "docs/examples/tenant-external-configuration-store.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["External Configuration Store"],
            ["central settings provider", "typed validation", "source-generated store factory", "DI composition"]),
        Descriptor(
            "Customer Dashboard Gateway Aggregation",
            "src/PatternKit.Examples/GatewayAggregationDemo/CustomerDashboardGatewayAggregationDemo.cs",
            "test/PatternKit.Examples.Tests/GatewayAggregationDemo/CustomerDashboardGatewayAggregationDemoTests.cs",
            "docs/examples/customer-dashboard-gateway-aggregation.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore,
            ["Gateway Aggregation"],
            ["downstream dashboard composition", "source-generated gateway aggregation", "ASP.NET Core endpoint mapping"]),
        Descriptor(
            "Checkout Strangler Fig Migration",
            "src/PatternKit.Examples/StranglerFigDemo/CheckoutStranglerFigDemo.cs",
            "test/PatternKit.Examples.Tests/StranglerFigDemo/CheckoutStranglerFigDemoTests.cs",
            "docs/examples/checkout-strangler-fig.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore,
            ["Strangler Fig"],
            ["legacy fallback", "source-generated migration router", "ASP.NET Core endpoint mapping"]),
        Descriptor(
            "Product Gateway Routing",
            "src/PatternKit.Examples/GatewayRoutingDemo/ProductGatewayRoutingDemo.cs",
            "test/PatternKit.Examples.Tests/GatewayRoutingDemo/ProductGatewayRoutingDemoTests.cs",
            "docs/examples/product-gateway-routing.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore,
            ["Gateway Routing"],
            ["path-based downstream dispatch", "source-generated gateway router", "ASP.NET Core endpoint mapping"]),
        Descriptor(
            "Order Telemetry Sidecar",
            "src/PatternKit.Examples/SidecarDemo/OrderTelemetrySidecarDemo.cs",
            "test/PatternKit.Examples.Tests/SidecarDemo/OrderTelemetrySidecarDemoTests.cs",
            "docs/examples/order-telemetry-sidecar.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore,
            ["Sidecar"],
            ["trace context enrichment", "source-generated sidecar factory", "ASP.NET Core endpoint mapping"]),
        Descriptor(
            "Commerce Backends for Frontends",
            "src/PatternKit.Examples/BackendsForFrontendsDemo/CommerceBackendsForFrontendsDemo.cs",
            "test/PatternKit.Examples.Tests/BackendsForFrontendsDemo/CommerceBackendsForFrontendsDemoTests.cs",
            "docs/examples/commerce-backends-for-frontends.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore,
            ["Backends for Frontends"],
            ["client-specific response shaping", "source-generated BFF factory", "ASP.NET Core endpoint mapping"]),
        Descriptor(
            "Inventory Ambassador",
            "src/PatternKit.Examples/AmbassadorDemo/InventoryAmbassadorDemo.cs",
            "test/PatternKit.Examples.Tests/AmbassadorDemo/InventoryAmbassadorDemoTests.cs",
            "docs/examples/inventory-ambassador.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore,
            ["Ambassador"],
            ["outbound call wrapping", "source-generated ambassador factory", "ASP.NET Core endpoint mapping"]),
        Descriptor(
            "Warehouse Leader Election",
            "src/PatternKit.Examples/LeaderElectionDemo/WarehouseLeaderElectionDemo.cs",
            "test/PatternKit.Examples.Tests/LeaderElectionDemo/WarehouseLeaderElectionDemoTests.cs",
            "docs/examples/warehouse-leader-election.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Leader Election"],
            ["single active worker lease", "source-generated candidate factory", "Generic Host hosted service"]),
        Descriptor(
            "Warehouse Scheduler Agent Supervisor",
            "src/PatternKit.Examples/SchedulerAgentSupervisorDemo/WarehouseSchedulerAgentSupervisorDemo.cs",
            "test/PatternKit.Examples.Tests/SchedulerAgentSupervisorDemo/WarehouseSchedulerAgentSupervisorDemoTests.cs",
            "docs/examples/warehouse-scheduler-agent-supervisor.md",
            ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost,
            ["Scheduler Agent Supervisor"],
            ["scheduled work dispatch", "source-generated supervisor factory", "Generic Host hosted service"])
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
