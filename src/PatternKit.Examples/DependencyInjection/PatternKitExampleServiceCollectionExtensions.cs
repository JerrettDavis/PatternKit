using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PatternKit.Application.AntiCorruption;
using PatternKit.Application.Specification;
using PatternKit.Behavioral.Chain;
using PatternKit.Behavioral.Interpreter;
using PatternKit.Behavioral.Strategy;
using PatternKit.Behavioral.TypeDispatcher;
using PatternKit.Cloud.Bulkhead;
using PatternKit.Cloud.CacheAside;
using PatternKit.Cloud.CircuitBreaker;
using PatternKit.Cloud.HealthEndpointMonitoring;
using PatternKit.Cloud.PriorityQueue;
using PatternKit.Cloud.RateLimiting;
using PatternKit.Cloud.QueueLoadLeveling;
using PatternKit.Cloud.Retry;
using PatternKit.Creational.AbstractFactory;
using PatternKit.Creational.Prototype;
using PatternKit.Creational.Singleton;
using PatternKit.Examples.ApiGateway;
using PatternKit.Examples.AntiCorruptionDemo;
using PatternKit.Examples.AsyncStateDemo;
using PatternKit.Examples.AuditLogDemo;
using PatternKit.Examples.BulkheadDemo;
using PatternKit.Examples.CacheAsideDemo;
using PatternKit.Examples.CanonicalDataModelDemo;
using PatternKit.Examples.Chain;
using PatternKit.Examples.Chain.ConfigDriven;
using PatternKit.Examples.CircuitBreakerDemo;
using PatternKit.Examples.DataMapperDemo;
using PatternKit.Examples.DomainEventDemo;
using PatternKit.Examples.EnterpriseFeatureSlices;
using PatternKit.Examples.EventSourcingDemo;
using PatternKit.Examples.ExternalConfigurationStoreDemo;
using PatternKit.Examples.FeatureToggleDemo;
using PatternKit.Examples.FlyweightDemo;
using PatternKit.Examples.Generators.Builders.CorporateApplicationBuilderDemo;
using PatternKit.Examples.Generators.Visitors;
using PatternKit.Examples.HealthEndpointMonitoringDemo;
using PatternKit.Examples.IdentityMapDemo;
using PatternKit.Examples.MaterializedViewDemo;
using PatternKit.Examples.MementoDemo;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.ObserverDemo;
using PatternKit.Examples.PatternShowcase;
using PatternKit.Examples.PointOfSale;
using PatternKit.Examples.Pricing;
using PatternKit.Examples.ProductionReadiness;
using PatternKit.Examples.PriorityQueueDemo;
using PatternKit.Examples.PrototypeDemo;
using PatternKit.Examples.ProxyDemo;
using PatternKit.Examples.QueueLoadLevelingDemo;
using PatternKit.Examples.RateLimitingDemo;
using PatternKit.Examples.RepositoryDemo;
using PatternKit.Examples.RetryDemo;
using PatternKit.Examples.ServiceLayerDemo;
using PatternKit.Examples.Singleton;
using PatternKit.Examples.SpecificationDemo;
using PatternKit.Examples.Strategies.Coercion;
using PatternKit.Examples.Strategies.Composed;
using PatternKit.Examples.TableDataGatewayDemo;
using PatternKit.Examples.TemplateDemo;
using PatternKit.Examples.TransactionScriptDemo;
using PatternKit.Examples.UnitOfWorkDemo;
using PatternKit.Examples.VisitorDemo;
using PatternKit.Messaging.Channels;
using PatternKit.Messaging.Consumers;
using PatternKit.Messaging.Adapters;
using PatternKit.Messaging.Activation;
using PatternKit.Messaging.Gateways;
using PatternKit.Messaging.Routing;
using PatternKit.Messaging.Storage;
using PatternKit.Messaging.ControlBus;
using PatternKit.Messaging.CompetingConsumers;
using PatternKit.Messaging.PipesAndFilters;
using PatternKit.Messaging.Transformation;
using PatternKit.Structural.Decorator;
using PatternKit.Structural.Proxy;
using CheckoutRequest = PatternKit.Examples.Messaging.CheckoutRequest;
using ConfigTenderHandler = PatternKit.Examples.Chain.ConfigDriven.ITenderHandler;
using ConfigPaymentPipeline = PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.PaymentPipeline;
using DocumentValidationResult = PatternKit.Examples.Generators.Visitors.DocumentProcessingDemo.ValidationResult;
using EnterpriseCheckout = PatternKit.Examples.EnterpriseFeatureSlices.EnterpriseFeatureSlicesDemo.IEnterpriseCheckout;
using EnterpriseCheckoutRequest = PatternKit.Examples.EnterpriseFeatureSlices.EnterpriseFeatureSlicesDemo.CheckoutRequest;
using EnterpriseCheckoutResult = PatternKit.Examples.EnterpriseFeatureSlices.EnterpriseFeatureSlicesDemo.CheckoutResult;
using PosPaymentKind = PatternKit.Examples.ObserverDemo.PaymentKind;
using ShowcaseFacade = PatternKit.Examples.PatternShowcase.PatternShowcase.IOrderProcessingFacade;
using TransactionPipeline = PatternKit.Examples.Chain.TransactionPipeline;
using VisitorTender = PatternKit.Examples.VisitorDemo.Tender;
using WidgetDemo = PatternKit.Examples.AbstractFactoryDemo.AbstractFactoryDemo;
using InterpreterRulesDemo = PatternKit.Examples.InterpreterDemo.InterpreterDemo;

namespace PatternKit.Examples.DependencyInjection;

/// <summary>
/// Describes the concrete service registered for an example-level PatternKit integration.
/// </summary>
public sealed record PatternKitExampleServiceDescriptor(
    string ExampleName,
    Type ServiceType,
    ExampleIntegrationSurface Integration);

/// <summary>
/// Open-generic coercion adapter so consumers can inject coercers through standard IoC.
/// </summary>
public interface ICoercer<T>
{
    T? From(object? value);
}

/// <summary>
/// Default open-generic coercion adapter backed by <see cref="Coercer{T}"/>.
/// </summary>
public sealed class CoercerService<T> : ICoercer<T>
{
    public T? From(object? value) => Coercer<T>.From(value);
}

public sealed record ProductionReadyExampleIntegrations(IPatternKitExampleCatalog ExampleCatalog, IPatternKitPatternCatalog PatternCatalog);
public sealed record AbstractFactoryWidgetExample(AbstractFactory<WidgetDemo.Platform> Factory);
public sealed record AuthLoggingChainExample(ActionChain<HttpRequest> Chain, List<string> Log);
public sealed record CoercionExample(ICoercer<int> Integers, ICoercer<bool> Booleans, ICoercer<string> Strings);
public sealed record ComposedNotificationStrategyExample(AsyncStrategy<SendContext, SendResult> Strategy);
public sealed record MediatedTransactionPipelineExample(TransactionPipeline Pipeline);
public sealed record ConfigDrivenTransactionPipelineExample(ConfigPaymentPipeline Pipeline);
public sealed record EnterpriseFeatureSlicesExample(EnterpriseCheckout Checkout, Func<EnterpriseCheckoutRequest> CreateRequest);
public sealed record MinimalWebRequestRouterExample(MiniRouter Router);
public sealed record PaymentProcessorDecoratorExample(Decorator<PurchaseOrder, PaymentReceipt> Processor);
public sealed record PosAppStateSingletonExample(Singleton<PosAppState> State);
public sealed record PricingCalculatorExample(PricingDemo.DemoArtifacts Artifacts);
public sealed record PosTenderVisitorExample(TypeDispatcher<VisitorTender, string> Renderer, ActionTypeDispatcher<VisitorTender> Router);
public sealed record ApiExceptionMappingVisitorExample(Func<Task> RunAsync);
public sealed record EventProcessingVisitorExample(Func<Task> RunAsync);
public sealed record MessageRouterVisitorExample(Func<RoutingSummary> Run);
public sealed record InventoryMessageChannelExampleService(MessageChannel<InventoryAdjustment> Channel, InventoryMessageChannelService Service);
public sealed record WarehousePollingConsumerExampleService(PollingConsumer<ReplenishmentRequest> Consumer, WarehousePollingConsumerService Service);
public sealed record OrderEventDrivenConsumerExampleService(EventDrivenConsumer<OrderAcceptedEvent> Consumer, OrderEventDrivenConsumerService Service);
public sealed record ErpChannelAdapterExampleService(ChannelAdapter<ErpOrderDocument, OrderIntegrationMessage> Adapter, ErpChannelAdapterService Service);
public sealed record PaymentMessagingGatewayExampleService(MessagingGateway<PaymentAuthorizationRequest, PaymentAuthorizationDecision> Gateway, PaymentMessagingGatewayService Service);
public sealed record InventoryServiceActivatorExampleService(ServiceActivator<InventoryReservationRequest, InventoryReservationResult> Activator, InventoryServiceActivatorService Service);
public sealed record GeneratedMessageEnvelopeExample(MessageEnvelopeExampleRunner Runner);
public sealed record GeneratedMessageTranslatorExample(PartnerEventTranslatorExampleRunner Runner, PartnerOrderImportService Service);
public sealed record CanonicalOrderDataModelExample(CanonicalOrderDemoRunner Runner, CanonicalOrderImportService Service);
public sealed record GeneratedClaimCheckExample(LargeDocumentClaimCheckExampleRunner Runner, LargeDocumentWorkflow Workflow);
public sealed record GeneratedDeadLetterChannelExample(FulfillmentDeadLetterChannelExampleRunner Runner, FulfillmentDeadLetterWorkflow Workflow);
public sealed record GeneratedRecipientListExample(RecipientListGeneratorExampleRunner Runner);
public sealed record GeneratedSplitterAggregatorExample(MessageRoutingExampleRunner Runner);
public sealed record OrderMessageFilterExampleService(MessageFilter<OrderMessageFilterCommand> Filter, OrderMessageFilterService Service);
public sealed record OrderMessageStoreExampleService(MessageStore<OrderMessageStoreEvent> Store, OrderMessageStoreService Service);
public sealed record OrderWireTapExampleService(WireTap<OrderWireTapEvent> Tap, OrderWireTapService Service);
public sealed record FulfillmentControlBusExampleService(ControlBus<FulfillmentControlCommand> Bus, FulfillmentControlBusService Service);
public sealed record SupplierQuoteScatterGatherExampleService(ScatterGather<SupplierQuoteRequest, SupplierQuote, SupplierQuoteSummary> ScatterGather, SupplierQuoteService Service);
public sealed record ShipmentResequencerExampleService(Resequencer<ShipmentEvent> Resequencer, ShipmentResequencerService Service);
public sealed record FulfillmentCompetingConsumersExampleService(CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult> Group, FulfillmentCompetingConsumerService Service);
public sealed record FulfillmentPipesAndFiltersExampleService(PipesAndFiltersPipeline<FulfillmentPipelineContext> Pipeline, FulfillmentPipesAndFiltersService Service);
public sealed record PatternsShowcaseExample(ShowcaseFacade Facade);
public sealed record SourceGeneratorApplicationSuiteExample(Func<ValueTask<CorporateApp>> BuildProductionAsync);
public sealed record EnterpriseMessagingWorkflowSuiteExample(Func<Summary> Run);
public sealed record CqrsDispatcherExample(Func<CancellationToken, ValueTask<CqrsSummary>> RunFluentAsync, Func<IServiceProvider, CancellationToken, ValueTask<CqrsSummary>> RunSourceGeneratedAsync);
public sealed record GeneratedMailboxExample(MailboxExampleRunner Runner);
public sealed record GeneratedReliabilityPipelineExample(ReliabilityExampleRunner Runner);
public sealed record ResilientCheckoutMailboxesExample(Func<CheckoutRequest, CheckoutServices, CheckoutResult> Run);
public sealed record MessagingBackplaneFacadeExample(Func<CancellationToken, ValueTask<BackplaneDemoSummary>> RunAsync);
public sealed record GeneratedInterpreterRulesExample(Interpreter<InterpreterRulesDemo.PricingContext, decimal> Pricing, Interpreter<InterpreterRulesDemo.PricingContext, bool> Eligibility);
public sealed record LoanApprovalSpecificationsExample(SpecificationRegistry<LoanApprovalSpecificationDemo.LoanApplication> Registry, LoanApprovalService Service);
public sealed record OrderRepositoryPatternExample(OrderRepositoryDemoRunner Runner, OrderRepositoryWorkflow Workflow);
public sealed record CheckoutUnitOfWorkPatternExample(CheckoutUnitOfWorkDemoRunner Runner, CheckoutUnitOfWorkWorkflow Workflow);
public sealed record OrderDataMapperPatternExample(OrderDataMapperDemoRunner Runner, OrderDataMapperWorkflow Workflow);
public sealed record OrderIdentityMapPatternExample(OrderIdentityMapDemoRunner Runner);
public sealed record OrderTransactionScriptPatternExample(OrderTransactionScriptDemoRunner Runner);
public sealed record CustomerServiceLayerPatternExample(CustomerServiceLayerDemoRunner Runner);
public sealed record OrderDomainEventPatternExample(OrderDomainEventDemoRunner Runner);
public sealed record OrderTableDataGatewayPatternExample(OrderTableDataGatewayDemoRunner Runner);
public sealed record OrderEventSourcingPatternExample(OrderEventSourcingDemoRunner Runner);
public sealed record CheckoutFeatureTogglePatternExample(CheckoutFeatureToggleDemoRunner Runner);
public sealed record OrderAuditLogPatternExample(OrderAuditLogDemoRunner Runner);
public sealed record OrderMaterializedViewPatternExample(OrderMaterializedViewDemoRunner Runner, OrderMaterializedViewWorkflow Workflow);
public sealed record PrototypeGameCharacterFactoryExample(Prototype<string, PrototypeDemo.PrototypeDemo.GameCharacter> Factory);
public sealed record ProxyPatternDemonstrationsExample(Proxy<int, string> RemoteProxy, Proxy<(string To, string Subject, string Body), bool> EmailProxy);
public sealed record FlyweightGlyphCacheExample(Func<string, IReadOnlyList<(FlyweightDemo.FlyweightDemo.Glyph Glyph, int X)>> RenderSentence);
public sealed record TextEditorMementoExample(MementoDemo.MementoDemo.TextEditor Editor);
public sealed record ObserverEventHubExample(EventHub<UserEvent> Hub);
public sealed record ReactiveViewModelExample(ProfileViewModel ViewModel);
public sealed record ReactiveTransactionExample(ReactiveTransaction Transaction);
public sealed record AsyncConnectionStateMachineExample(Func<string[], ValueTask<(ConnectionStateDemo.Mode Final, List<string> Log)>> RunAsync);
public sealed record TemplateMethodSubclassingExample(DataProcessor Processor);
public sealed record TemplateMethodAsyncExample(AsyncDataPipeline Pipeline);
public sealed record LegacyOrderAntiCorruptionExample(AntiCorruptionLayer<LegacyOrderDto, CommerceOrder> Layer, LegacyOrderImportService Service);
public sealed record InventoryRetryExample(RetryPolicy<InventoryResponse> Policy, InventoryLookupService Service);
public sealed record FulfillmentCircuitBreakerExample(CircuitBreakerPolicy<FulfillmentResponse> Policy, FulfillmentCircuitBreakerService Service);
public sealed record ShippingBulkheadExample(BulkheadPolicy<ShippingAllocation> Policy, ShippingBulkheadService Service);
public sealed record FulfillmentQueueLoadLevelingExample(QueueLoadLevelingPolicy<FulfillmentQueueResult> Policy, FulfillmentQueueLoadLevelingService Service);
public sealed record FulfillmentHealthEndpointExample(HealthEndpoint<FulfillmentHealthSnapshot> Endpoint, FulfillmentHealthEndpointService Service);
public sealed record FulfillmentPriorityQueueExample(PriorityQueuePolicy<FulfillmentPriorityWork, int> Queue, FulfillmentPriorityQueueService Service);
public sealed record ProductCatalogCacheAsideExample(CacheAsidePolicy<ProductReadModel> Policy, ProductCatalogCacheAsideService Service);
public sealed record ProductSearchRateLimitingExample(RateLimitPolicy<SearchResponse> Policy, ProductSearchRateLimitService Service);
public sealed record TenantExternalConfigurationStoreExample(TenantExternalConfigurationStoreDemoRunner Runner, TenantExternalConfigurationService Service);

/// <summary>
/// Fluent registration helpers for importing every documented PatternKit example into Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class PatternKitExampleServiceCollectionExtensions
{
    public static IServiceCollection AddPatternKitExamples(this IServiceCollection services, IConfiguration? configuration = null)
        => services
            .AddProductionReadyExampleIntegrations()
            .AddAbstractFactoryWidgetExample()
            .AddAuthLoggingChainExample()
            .AddStrategyBasedDataCoercionExample()
            .AddComposedNotificationStrategyExample()
            .AddMediatedTransactionPipelineExample()
            .AddConfigurationDrivenTransactionPipelineExample(configuration)
            .AddEnterpriseFeatureSlicesExample()
            .AddMinimalWebRequestRouterExample()
            .AddPaymentProcessorDecoratorExample()
            .AddPosAppStateSingletonExample()
            .AddPricingCalculatorExample()
            .AddPosTenderVisitorExample()
            .AddApiExceptionMappingVisitorExample()
            .AddEventProcessingVisitorExample()
            .AddMessageRouterVisitorExample()
            .AddInventoryMessageChannelExample()
            .AddWarehousePollingConsumerExample()
            .AddOrderEventDrivenConsumerExample()
            .AddErpChannelAdapterExample()
            .AddPaymentMessagingGatewayExample()
            .AddInventoryServiceActivatorExample()
            .AddGeneratedMessageEnvelopeExample()
            .AddGeneratedMessageTranslatorExample()
            .AddCanonicalOrderDataModelExample()
            .AddGeneratedClaimCheckExample()
            .AddGeneratedDeadLetterChannelExample()
            .AddGeneratedRecipientListExample()
            .AddGeneratedSplitterAggregatorExample()
            .AddOrderMessageFilterExample()
            .AddOrderMessageStoreExample()
            .AddOrderWireTapExample()
            .AddFulfillmentControlBusExample()
            .AddSupplierQuoteScatterGatherExample()
            .AddShipmentResequencerExample()
            .AddFulfillmentCompetingConsumersExample()
            .AddFulfillmentPipesAndFiltersExample()
            .AddPatternsShowcaseExample()
            .AddSourceGeneratorApplicationSuiteExample()
            .AddEnterpriseMessagingWorkflowSuiteExample()
            .AddCqrsDispatcherExample()
            .AddGeneratedMailboxExample()
            .AddGeneratedReliabilityPipelineExample()
            .AddResilientCheckoutMailboxesExample()
            .AddMessagingBackplaneFacadeExample()
            .AddGeneratedInterpreterRulesExample()
            .AddLoanApprovalSpecificationsExample()
            .AddOrderRepositoryPatternExample()
            .AddCheckoutUnitOfWorkPatternExample()
            .AddOrderDataMapperPatternExample()
            .AddOrderIdentityMapPatternExample()
            .AddOrderTransactionScriptPatternExample()
            .AddCustomerServiceLayerPatternExample()
            .AddOrderDomainEventPatternExample()
            .AddOrderTableDataGatewayPatternExample()
            .AddOrderEventSourcingPatternExample()
            .AddCheckoutFeatureTogglePatternExample()
            .AddOrderAuditLogPatternExample()
            .AddOrderMaterializedViewPatternExample()
            .AddPrototypeGameCharacterFactoryExample()
            .AddProxyPatternDemonstrationsExample()
            .AddFlyweightGlyphCacheExample()
            .AddTextEditorMementoExample()
            .AddObserverEventHubExample()
            .AddReactiveViewModelExample()
            .AddReactiveTransactionExample()
            .AddAsyncConnectionStateMachineExample()
            .AddTemplateMethodSubclassingExample()
            .AddTemplateMethodAsyncExample()
            .AddLegacyOrderAntiCorruptionExample()
            .AddInventoryRetryExample()
            .AddFulfillmentCircuitBreakerExample()
            .AddShippingBulkheadExample()
            .AddFulfillmentQueueLoadLevelingExample()
            .AddFulfillmentHealthEndpointExample()
            .AddFulfillmentPriorityQueueExample()
            .AddProductCatalogCacheAsideExample()
            .AddProductSearchRateLimitingExample()
            .AddTenantExternalConfigurationStoreExample();

    public static IServiceCollection AddProductionReadyExampleIntegrations(this IServiceCollection services)
    {
        services.AddPatternKitExampleCatalog();
        services.AddPatternKitPatternCatalog();
        services.AddSingleton<ProductionReadyExampleIntegrations>(sp =>
            new(
                sp.GetRequiredService<IPatternKitExampleCatalog>(),
                sp.GetRequiredService<IPatternKitPatternCatalog>()));
        return services.RegisterExample<ProductionReadyExampleIntegrations>("Production-Ready Example Integrations", ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore);
    }

    public static IServiceCollection AddAbstractFactoryWidgetExample(this IServiceCollection services)
    {
        services.AddSingleton(static sp => WidgetDemo.CreateUIFactory(sp));
        services.AddSingleton<AbstractFactoryWidgetExample>(sp => new(sp.GetRequiredService<AbstractFactory<WidgetDemo.Platform>>()));
        return services.RegisterExample<AbstractFactoryWidgetExample>("Abstract Factory Widget Families", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddAuthLoggingChainExample(this IServiceCollection services)
    {
        services.AddSingleton<AuthLoggingChainExample>(_ =>
        {
            var log = new List<string>();
            var chain = ActionChain<HttpRequest>.Create()
                .When(static (in r) => r.Headers.ContainsKey("X-Request-Id"))
                .ThenContinue(r => log.Add($"reqid={r.Headers["X-Request-Id"]}"))
                .When(static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal) && !r.Headers.ContainsKey("Authorization"))
                .ThenStop(_ => log.Add("deny: missing auth"))
                .Finally((in r, next) =>
                {
                    log.Add($"{r.Method} {r.Path}");
                    next(r);
                })
                .Build();
            return new AuthLoggingChainExample(chain, log);
        });
        return services.RegisterExample<AuthLoggingChainExample>("Auth & Logging Chain", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddStrategyBasedDataCoercionExample(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ICoercer<>), typeof(CoercerService<>));
        services.AddSingleton<CoercionExample>(sp => new(
            sp.GetRequiredService<ICoercer<int>>(),
            sp.GetRequiredService<ICoercer<bool>>(),
            sp.GetRequiredService<ICoercer<string>>()));
        return services.RegisterExample<CoercionExample>("Strategy-Based Data Coercion", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddComposedNotificationStrategyExample(this IServiceCollection services)
    {
        services.TryAddSingleton<IIdentityService, DemoIdentityService>();
        services.TryAddSingleton<IPresenceService, DemoPresenceService>();
        services.TryAddSingleton<IRateLimiter, DemoRateLimiter>();
        services.TryAddSingleton<IPreferenceService, DemoPreferenceService>();
        services.TryAddSingleton<IEmailSender, DemoEmailSender>();
        services.TryAddSingleton<ISmsSender, DemoSmsSender>();
        services.TryAddSingleton<IPushSender, DemoPushSender>();
        services.TryAddSingleton<IImSender, DemoImSender>();
        services.AddSingleton<ComposedNotificationStrategyExample>(sp => new(
            ComposedStrategies.BuildPreferenceAware(
                sp.GetRequiredService<IIdentityService>(),
                sp.GetRequiredService<IPresenceService>(),
                sp.GetRequiredService<IRateLimiter>(),
                sp.GetRequiredService<IPreferenceService>(),
                sp.GetRequiredService<IEmailSender>(),
                sp.GetRequiredService<ISmsSender>(),
                sp.GetRequiredService<IPushSender>(),
                sp.GetRequiredService<IImSender>())));
        return services.RegisterExample<ComposedNotificationStrategyExample>("Composed Notification Strategy", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddMediatedTransactionPipelineExample(this IServiceCollection services)
    {
        services.TryAddSingleton<IDeviceBus, DeviceBus>();
        services.TryAddSingleton<CardProcessors>(_ => new(new()
        {
            [CardVendor.Visa] = new GenericProcessor("VisaNet"),
            [CardVendor.Mastercard] = new GenericProcessor("MC"),
            [CardVendor.Amex] = new GenericProcessor("Amex"),
            [CardVendor.Chase] = new GenericProcessor("ChaseNet"),
            [CardVendor.InHouse] = new GenericProcessor("InHouse"),
            [CardVendor.Unknown] = new GenericProcessor("FallbackNet")
        }));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ConfigTenderHandler, CashTender>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ConfigTenderHandler, CardTender>());
        services.AddSingleton<MediatedTransactionPipelineExample>(sp =>
        {
            var pipeline = TransactionPipelineBuilder.New()
                .WithDeviceBus(sp.GetRequiredService<IDeviceBus>())
                .WithTenderHandlers(sp.GetServices<ConfigTenderHandler>().ToArray())
                .AddPreauth()
                .AddDiscountsAndTax()
                .AddRounding()
                .AddTenderHandling()
                .AddFinalize()
                .Build();

            return new MediatedTransactionPipelineExample(pipeline);
        });
        return services.RegisterExample<MediatedTransactionPipelineExample>("Mediated Transaction Pipeline", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddConfigurationDrivenTransactionPipelineExample(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddPaymentPipeline(configuration ?? new ConfigurationBuilder().Build());
        services.AddSingleton<ConfigDrivenTransactionPipelineExample>(sp => new(sp.GetRequiredService<ConfigPaymentPipeline>()));
        return services.RegisterExample<ConfigDrivenTransactionPipelineExample>("Configuration-Driven Transaction Pipeline", ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.Options);
    }

    public static IServiceCollection AddEnterpriseFeatureSlicesExample(this IServiceCollection services)
    {
        services.AddEnterpriseFeatureSlices();
        services.AddSingleton<EnterpriseFeatureSlicesExample>(sp => new(
            sp.GetRequiredService<EnterpriseCheckout>(),
            () => EnterpriseFeatureSlicesDemo.CreateRetailRequest()));
        return services.RegisterExample<EnterpriseFeatureSlicesExample>("Enterprise Feature Slices with .NET DI", ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddMinimalWebRequestRouterExample(this IServiceCollection services)
    {
        services.AddSingleton(_ => MiniRouter.Create()
            .Map(static (in r) => string.Equals(r.Path, "/health", StringComparison.OrdinalIgnoreCase), static (in _) => Responses.Text(200, "ok"))
            .Map(static (in r) => string.Equals(r.Path, "/orders", StringComparison.OrdinalIgnoreCase), static (in _) => Responses.Json(200, """{"items":[]}"""))
            .NotFound(static (in _) => Responses.NotFound())
            .Build());
        services.AddSingleton<MinimalWebRequestRouterExample>(sp => new(sp.GetRequiredService<MiniRouter>()));
        return services.RegisterExample<MinimalWebRequestRouterExample>("Minimal Web Request Router", ExampleIntegrationSurface.AspNetCore | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddPaymentProcessorDecoratorExample(this IServiceCollection services)
    {
        services.AddSingleton(_ => PaymentProcessorDemo.CreateEcommerceProcessor([]));
        services.AddSingleton<PaymentProcessorDecoratorExample>(sp => new(sp.GetRequiredService<Decorator<PurchaseOrder, PaymentReceipt>>()));
        return services.RegisterExample<PaymentProcessorDecoratorExample>("Payment Processor Decorator", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddPosAppStateSingletonExample(this IServiceCollection services)
    {
        services.AddSingleton(_ => Singleton<PosAppState>
            .Create(static () => new PosAppState
            {
                Config = StoreConfig.Load(),
                Pricing = new PricingCache(),
                Devices = new DeviceRegistry()
            })
            .Init(static state => state.Pricing.Prewarm(["SKU-1", "SKU-2", "SKU-3"]))
            .Init(static state => state.Devices.ConnectAll())
            .Build());
        services.AddSingleton<PosAppStateSingletonExample>(sp => new(sp.GetRequiredService<Singleton<PosAppState>>()));
        return services.RegisterExample<PosAppStateSingletonExample>("POS App State Singleton", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddPricingCalculatorExample(this IServiceCollection services)
    {
        services.AddSingleton(_ => PricingDemo.BuildDefault());
        services.AddSingleton<PricingCalculatorExample>(sp => new(sp.GetRequiredService<PricingDemo.DemoArtifacts>()));
        return services.RegisterExample<PricingCalculatorExample>("Pricing Calculator", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddPosTenderVisitorExample(this IServiceCollection services)
    {
        services.AddSingleton<CountersHandler>();
        services.AddSingleton(_ => ReceiptRendering.CreateRenderer());
        services.AddSingleton(sp => Routing.CreateRouter(sp.GetRequiredService<CountersHandler>()));
        services.AddSingleton<PosTenderVisitorExample>(sp => new(
            sp.GetRequiredService<TypeDispatcher<VisitorTender, string>>(),
            sp.GetRequiredService<ActionTypeDispatcher<VisitorTender>>()));
        return services.RegisterExample<PosTenderVisitorExample>("POS Tender Visitor", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddApiExceptionMappingVisitorExample(this IServiceCollection services)
    {
        services.AddSingleton(new ApiExceptionMappingVisitorExample(DocumentProcessingDemo.RunAsync));
        return services.RegisterExample<ApiExceptionMappingVisitorExample>("API Exception Mapping Visitor", ExampleIntegrationSurface.AspNetCore | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddEventProcessingVisitorExample(this IServiceCollection services)
    {
        services.AddSingleton(new EventProcessingVisitorExample(DocumentProcessingDemo.RunAsync));
        return services.RegisterExample<EventProcessingVisitorExample>("Event Processing Visitor", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddMessageRouterVisitorExample(this IServiceCollection services)
    {
        services.AddSingleton(new MessageRouterVisitorExample(MessageRoutingExample.Run));
        return services.RegisterExample<MessageRouterVisitorExample>("Message Router Visitor", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddMessageRoutingExample(this IServiceCollection services)
    {
        services.AddSingleton(new MessageRoutingExampleRunner(MessageRoutingExample.RunFluent, MessageRoutingExample.RunGenerated));
        return services;
    }

    public static IServiceCollection AddInventoryMessageChannelExample(this IServiceCollection services)
    {
        services.AddInventoryMessageChannelDemo();
        services.AddSingleton<InventoryMessageChannelExampleService>(sp => new(
            sp.GetRequiredService<MessageChannel<InventoryAdjustment>>(),
            sp.GetRequiredService<InventoryMessageChannelService>()));
        return services.RegisterExample<InventoryMessageChannelExampleService>("Inventory Message Channel", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddWarehousePollingConsumerExample(this IServiceCollection services)
    {
        services.AddWarehousePollingConsumerDemo();
        services.AddSingleton<WarehousePollingConsumerExampleService>(sp => new(
            sp.GetRequiredService<PollingConsumer<ReplenishmentRequest>>(),
            sp.GetRequiredService<WarehousePollingConsumerService>()));
        return services.RegisterExample<WarehousePollingConsumerExampleService>("Warehouse Polling Consumer", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderEventDrivenConsumerExample(this IServiceCollection services)
    {
        services.AddOrderEventDrivenConsumerDemo();
        services.AddSingleton<OrderEventDrivenConsumerExampleService>(sp => new(
            sp.GetRequiredService<EventDrivenConsumer<OrderAcceptedEvent>>(),
            sp.GetRequiredService<OrderEventDrivenConsumerService>()));
        return services.RegisterExample<OrderEventDrivenConsumerExampleService>("Order Event-Driven Consumer", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddErpChannelAdapterExample(this IServiceCollection services)
    {
        services.AddErpChannelAdapterDemo();
        services.AddSingleton<ErpChannelAdapterExampleService>(sp => new(
            sp.GetRequiredService<ChannelAdapter<ErpOrderDocument, OrderIntegrationMessage>>(),
            sp.GetRequiredService<ErpChannelAdapterService>()));
        return services.RegisterExample<ErpChannelAdapterExampleService>("ERP Channel Adapter", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddPaymentMessagingGatewayExample(this IServiceCollection services)
    {
        services.AddPaymentMessagingGatewayDemo();
        services.AddSingleton<PaymentMessagingGatewayExampleService>(sp => new(
            sp.GetRequiredService<MessagingGateway<PaymentAuthorizationRequest, PaymentAuthorizationDecision>>(),
            sp.GetRequiredService<PaymentMessagingGatewayService>()));
        return services.RegisterExample<PaymentMessagingGatewayExampleService>("Payment Messaging Gateway", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddInventoryServiceActivatorExample(this IServiceCollection services)
    {
        services.AddInventoryServiceActivatorDemo();
        services.AddSingleton<InventoryServiceActivatorExampleService>(sp => new(
            sp.GetRequiredService<ServiceActivator<InventoryReservationRequest, InventoryReservationResult>>(),
            sp.GetRequiredService<InventoryServiceActivatorService>()));
        return services.RegisterExample<InventoryServiceActivatorExampleService>("Inventory Service Activator", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddGeneratedMessageEnvelopeExample(this IServiceCollection services)
    {
        services.AddMessageEnvelopeExample();
        services.AddSingleton<GeneratedMessageEnvelopeExample>(sp => new(sp.GetRequiredService<MessageEnvelopeExampleRunner>()));
        return services.RegisterExample<GeneratedMessageEnvelopeExample>("Generated Message Envelope", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddGeneratedMessageTranslatorExample(this IServiceCollection services)
    {
        services.AddPartnerEventTranslatorExample();
        services.AddSingleton<GeneratedMessageTranslatorExample>(sp => new(
            sp.GetRequiredService<PartnerEventTranslatorExampleRunner>(),
            sp.GetRequiredService<PartnerOrderImportService>()));
        return services.RegisterExample<GeneratedMessageTranslatorExample>("Generated Message Translator", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddCanonicalOrderDataModelExample(this IServiceCollection services)
    {
        services.AddCanonicalOrderDataModelDemo();
        services.AddSingleton<CanonicalOrderDataModelExample>(sp => new(
            sp.GetRequiredService<CanonicalOrderDemoRunner>(),
            sp.GetRequiredService<CanonicalOrderImportService>()));
        return services.RegisterExample<CanonicalOrderDataModelExample>("Order Canonical Data Model", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddGeneratedClaimCheckExample(this IServiceCollection services)
    {
        services.AddLargeDocumentClaimCheckExample();
        services.AddSingleton<GeneratedClaimCheckExample>(sp => new(
            sp.GetRequiredService<LargeDocumentClaimCheckExampleRunner>(),
            sp.GetRequiredService<LargeDocumentWorkflow>()));
        return services.RegisterExample<GeneratedClaimCheckExample>("Generated Claim Check", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddGeneratedDeadLetterChannelExample(this IServiceCollection services)
    {
        services.AddFulfillmentDeadLetterChannelExample();
        services.AddSingleton<GeneratedDeadLetterChannelExample>(sp => new(
            sp.GetRequiredService<FulfillmentDeadLetterChannelExampleRunner>(),
            sp.GetRequiredService<FulfillmentDeadLetterWorkflow>()));
        return services.RegisterExample<GeneratedDeadLetterChannelExample>("Generated Dead Letter Channel", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddGeneratedRecipientListExample(this IServiceCollection services)
    {
        services.AddRecipientListGeneratorExample();
        services.AddSingleton<GeneratedRecipientListExample>(sp => new(sp.GetRequiredService<RecipientListGeneratorExampleRunner>()));
        return services.RegisterExample<GeneratedRecipientListExample>("Generated Recipient List", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddGeneratedSplitterAggregatorExample(this IServiceCollection services)
    {
        services.AddMessageRoutingExample();
        services.AddSingleton<GeneratedSplitterAggregatorExample>(sp => new(sp.GetRequiredService<MessageRoutingExampleRunner>()));
        return services.RegisterExample<GeneratedSplitterAggregatorExample>("Generated Splitter and Aggregator", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddOrderMessageFilterExample(this IServiceCollection services)
    {
        services.AddOrderMessageFilterDemo();
        services.AddSingleton<OrderMessageFilterExampleService>(sp => new(
            sp.GetRequiredService<MessageFilter<OrderMessageFilterCommand>>(),
            sp.GetRequiredService<OrderMessageFilterService>()));
        return services.RegisterExample<OrderMessageFilterExampleService>("Order Message Filter", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddOrderMessageStoreExample(this IServiceCollection services)
    {
        services.AddOrderMessageStoreDemo();
        services.AddSingleton<OrderMessageStoreExampleService>(sp => new(
            sp.GetRequiredService<MessageStore<OrderMessageStoreEvent>>(),
            sp.GetRequiredService<OrderMessageStoreService>()));
        return services.RegisterExample<OrderMessageStoreExampleService>("Order Message Store", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddOrderWireTapExample(this IServiceCollection services)
    {
        services.AddOrderWireTapDemo();
        services.AddSingleton<OrderWireTapExampleService>(sp => new(
            sp.GetRequiredService<WireTap<OrderWireTapEvent>>(),
            sp.GetRequiredService<OrderWireTapService>()));
        return services.RegisterExample<OrderWireTapExampleService>("Order Wire Tap", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddFulfillmentControlBusExample(this IServiceCollection services)
    {
        services.AddFulfillmentControlBusDemo();
        services.AddSingleton<FulfillmentControlBusExampleService>(sp => new(
            sp.GetRequiredService<ControlBus<FulfillmentControlCommand>>(),
            sp.GetRequiredService<FulfillmentControlBusService>()));
        return services.RegisterExample<FulfillmentControlBusExampleService>("Fulfillment Control Bus", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddSupplierQuoteScatterGatherExample(this IServiceCollection services)
    {
        services.AddSupplierQuoteScatterGatherDemo();
        services.AddSingleton<SupplierQuoteScatterGatherExampleService>(sp => new(
            sp.GetRequiredService<ScatterGather<SupplierQuoteRequest, SupplierQuote, SupplierQuoteSummary>>(),
            sp.GetRequiredService<SupplierQuoteService>()));
        return services.RegisterExample<SupplierQuoteScatterGatherExampleService>("Supplier Quote Scatter-Gather", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddShipmentResequencerExample(this IServiceCollection services)
    {
        services.AddShipmentResequencerDemo();
        services.AddSingleton<ShipmentResequencerExampleService>(sp => new(
            sp.GetRequiredService<Resequencer<ShipmentEvent>>(),
            sp.GetRequiredService<ShipmentResequencerService>()));
        return services.RegisterExample<ShipmentResequencerExampleService>("Shipment Resequencer", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddFulfillmentCompetingConsumersExample(this IServiceCollection services)
    {
        services.AddFulfillmentCompetingConsumersDemo();
        services.AddSingleton<FulfillmentCompetingConsumersExampleService>(sp => new(
            sp.GetRequiredService<CompetingConsumerGroup<FulfillmentConsumerWork, FulfillmentConsumerResult>>(),
            sp.GetRequiredService<FulfillmentCompetingConsumerService>()));
        return services.RegisterExample<FulfillmentCompetingConsumersExampleService>("Fulfillment Competing Consumers", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddFulfillmentPipesAndFiltersExample(this IServiceCollection services)
    {
        services.AddFulfillmentPipesAndFiltersDemo();
        services.AddSingleton<FulfillmentPipesAndFiltersExampleService>(sp => new(
            sp.GetRequiredService<PipesAndFiltersPipeline<FulfillmentPipelineContext>>(),
            sp.GetRequiredService<FulfillmentPipesAndFiltersService>()));
        return services.RegisterExample<FulfillmentPipesAndFiltersExampleService>("Fulfillment Pipes and Filters", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddPatternsShowcaseExample(this IServiceCollection services)
    {
        services.AddSingleton(_ => PatternShowcase.PatternShowcase.Build());
        services.AddSingleton<PatternsShowcaseExample>(sp => new(sp.GetRequiredService<ShowcaseFacade>()));
        return services.RegisterExample<PatternsShowcaseExample>("Patterns Showcase", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddSourceGeneratorApplicationSuiteExample(this IServiceCollection services)
    {
        services.AddSingleton(new SourceGeneratorApplicationSuiteExample(CorporateApplicationDemo.BuildProductionAsync));
        return services.RegisterExample<SourceGeneratorApplicationSuiteExample>("Source Generator Application Suite", ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddEnterpriseMessagingWorkflowSuiteExample(this IServiceCollection services)
    {
        services.AddSingleton(new EnterpriseMessagingWorkflowSuiteExample(MessageEnvelopeExample.Run));
        return services.RegisterExample<EnterpriseMessagingWorkflowSuiteExample>("Enterprise Messaging Workflow Suite", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddCqrsDispatcherExample(this IServiceCollection services)
    {
        services.AddSourceGeneratedCqrsServices();
        services.AddSingleton(new CqrsDispatcherExample(CqrsPatternExample.RunFluentAsync, CqrsPatternExample.RunSourceGeneratedAsync));
        return services.RegisterExample<CqrsDispatcherExample>("CQRS Dispatcher", ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddGeneratedMailboxExample(this IServiceCollection services)
    {
        services.AddSingleton(new MailboxExampleRunner(MailboxExample.RunFluentAsync, MailboxExample.RunGeneratedAsync));
        services.AddSingleton<GeneratedMailboxExample>(sp => new(sp.GetRequiredService<MailboxExampleRunner>()));
        return services.RegisterExample<GeneratedMailboxExample>("Generated Mailbox", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddGeneratedReliabilityPipelineExample(this IServiceCollection services)
    {
        services.AddSingleton(new ReliabilityExampleRunner(ReliabilityExample.RunFluentAsync, ReliabilityExample.RunGeneratedAsync));
        services.AddSingleton<GeneratedReliabilityPipelineExample>(sp => new(sp.GetRequiredService<ReliabilityExampleRunner>()));
        return services.RegisterExample<GeneratedReliabilityPipelineExample>("Generated Reliability Pipeline", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddResilientCheckoutMailboxesExample(this IServiceCollection services)
    {
        services.AddSingleton<CheckoutServices>();
        services.AddSingleton(new ResilientCheckoutMailboxesExample(ResilientCheckoutDemo.Run));
        return services.RegisterExample<ResilientCheckoutMailboxesExample>("Resilient Checkout and Collaborating Mailboxes", ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddMessagingBackplaneFacadeExample(this IServiceCollection services)
    {
        services.AddSingleton(new MessagingBackplaneFacadeExample(BackplaneFacadeDemo.RunAsync));
        return services.RegisterExample<MessagingBackplaneFacadeExample>("Messaging Backplane Facade", ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.Messaging | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.ExternalInfrastructure | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddGeneratedInterpreterRulesExample(this IServiceCollection services)
    {
        services.AddSingleton(static _ => InterpreterRulesDemo.CreateGeneratedPricingInterpreter());
        services.AddSingleton(static _ => InterpreterRulesDemo.CreateGeneratedEligibilityInterpreter());
        services.AddSingleton<GeneratedInterpreterRulesExample>(sp => new(
            sp.GetRequiredService<Interpreter<InterpreterRulesDemo.PricingContext, decimal>>(),
            sp.GetRequiredService<Interpreter<InterpreterRulesDemo.PricingContext, bool>>()));
        return services.RegisterExample<GeneratedInterpreterRulesExample>("Generated Interpreter Rules", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddLoanApprovalSpecificationsExample(this IServiceCollection services)
    {
        services.AddLoanApprovalSpecifications();
        services.AddSingleton<LoanApprovalSpecificationsExample>(sp => new(
            sp.GetRequiredService<SpecificationRegistry<LoanApprovalSpecificationDemo.LoanApplication>>(),
            sp.GetRequiredService<LoanApprovalService>()));
        return services.RegisterExample<LoanApprovalSpecificationsExample>("Loan Approval Specifications", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddOrderRepositoryPatternExample(this IServiceCollection services)
    {
        services.AddOrderRepositoryDemo();
        services.AddSingleton<OrderRepositoryPatternExample>(sp => new(
            sp.GetRequiredService<OrderRepositoryDemoRunner>(),
            sp.GetRequiredService<OrderRepositoryWorkflow>()));
        return services.RegisterExample<OrderRepositoryPatternExample>("Order Repository Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddCheckoutUnitOfWorkPatternExample(this IServiceCollection services)
    {
        services.AddCheckoutUnitOfWorkDemo();
        services.AddSingleton<CheckoutUnitOfWorkPatternExample>(sp => new(
            sp.GetRequiredService<CheckoutUnitOfWorkDemoRunner>(),
            sp.GetRequiredService<CheckoutUnitOfWorkWorkflow>()));
        return services.RegisterExample<CheckoutUnitOfWorkPatternExample>("Checkout Unit of Work Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderDataMapperPatternExample(this IServiceCollection services)
    {
        services.AddOrderDataMapperDemo();
        services.AddSingleton<OrderDataMapperPatternExample>(sp => new(
            sp.GetRequiredService<OrderDataMapperDemoRunner>(),
            sp.GetRequiredService<OrderDataMapperWorkflow>()));
        return services.RegisterExample<OrderDataMapperPatternExample>("Order Data Mapper Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderIdentityMapPatternExample(this IServiceCollection services)
    {
        services.AddOrderIdentityMapDemo();
        services.AddSingleton<OrderIdentityMapPatternExample>(sp => new(sp.GetRequiredService<OrderIdentityMapDemoRunner>()));
        return services.RegisterExample<OrderIdentityMapPatternExample>("Order Identity Map Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderTransactionScriptPatternExample(this IServiceCollection services)
    {
        services.AddOrderTransactionScriptDemo();
        services.AddSingleton<OrderTransactionScriptPatternExample>(sp => new(sp.GetRequiredService<OrderTransactionScriptDemoRunner>()));
        return services.RegisterExample<OrderTransactionScriptPatternExample>("Order Transaction Script Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddCustomerServiceLayerPatternExample(this IServiceCollection services)
    {
        services.AddCustomerServiceLayerDemo();
        services.AddSingleton<CustomerServiceLayerPatternExample>(sp => new(sp.GetRequiredService<CustomerServiceLayerDemoRunner>()));
        return services.RegisterExample<CustomerServiceLayerPatternExample>("Customer Service Layer Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderDomainEventPatternExample(this IServiceCollection services)
    {
        services.AddOrderDomainEventDemo();
        services.AddSingleton<OrderDomainEventPatternExample>(sp => new(sp.GetRequiredService<OrderDomainEventDemoRunner>()));
        return services.RegisterExample<OrderDomainEventPatternExample>("Order Domain Event Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderTableDataGatewayPatternExample(this IServiceCollection services)
    {
        services.AddOrderTableDataGatewayDemo();
        services.AddSingleton<OrderTableDataGatewayPatternExample>(sp => new(sp.GetRequiredService<OrderTableDataGatewayDemoRunner>()));
        return services.RegisterExample<OrderTableDataGatewayPatternExample>("Order Table Data Gateway Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderEventSourcingPatternExample(this IServiceCollection services)
    {
        services.AddOrderEventSourcingDemo();
        services.AddSingleton<OrderEventSourcingPatternExample>(sp => new(sp.GetRequiredService<OrderEventSourcingDemoRunner>()));
        return services.RegisterExample<OrderEventSourcingPatternExample>("Order Event Sourcing Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddCheckoutFeatureTogglePatternExample(this IServiceCollection services)
    {
        services.AddCheckoutFeatureToggleDemo();
        services.AddSingleton<CheckoutFeatureTogglePatternExample>(sp => new(sp.GetRequiredService<CheckoutFeatureToggleDemoRunner>()));
        return services.RegisterExample<CheckoutFeatureTogglePatternExample>("Checkout Feature Toggle Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderAuditLogPatternExample(this IServiceCollection services)
    {
        services.AddOrderAuditLogDemo();
        services.AddSingleton<OrderAuditLogPatternExample>(sp => new(sp.GetRequiredService<OrderAuditLogDemoRunner>()));
        return services.RegisterExample<OrderAuditLogPatternExample>("Order Audit Log Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddOrderMaterializedViewPatternExample(this IServiceCollection services)
    {
        services.AddOrderMaterializedViewDemo();
        services.AddSingleton<OrderMaterializedViewPatternExample>(sp => new(
            sp.GetRequiredService<OrderMaterializedViewDemoRunner>(),
            sp.GetRequiredService<OrderMaterializedViewWorkflow>()));
        return services.RegisterExample<OrderMaterializedViewPatternExample>("Order Materialized View Pattern", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddPrototypeGameCharacterFactoryExample(this IServiceCollection services)
    {
        services.AddSingleton(_ => PrototypeDemo.PrototypeDemo.CreateCharacterFactory());
        services.AddSingleton<PrototypeGameCharacterFactoryExample>(sp => new(sp.GetRequiredService<Prototype<string, PrototypeDemo.PrototypeDemo.GameCharacter>>()));
        return services.RegisterExample<PrototypeGameCharacterFactoryExample>("Prototype Game Character Factory", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddProxyPatternDemonstrationsExample(this IServiceCollection services)
    {
        services.AddSingleton(_ => Proxy<int, string>.Create(id => $"Remote data for ID {id}").CachingProxy().Build());
        services.AddSingleton(_ =>
        {
            var mock = ProxyDemo.ProxyDemo.MockFramework.CreateMock<(string To, string Subject, string Body), bool>()
                .Setup(input => input.To.Contains("@example.com", StringComparison.OrdinalIgnoreCase), true)
                .Returns(true);
            return mock.Build();
        });
        services.AddSingleton<ProxyPatternDemonstrationsExample>(sp => new(
            sp.GetRequiredService<Proxy<int, string>>(),
            sp.GetRequiredService<Proxy<(string To, string Subject, string Body), bool>>()));
        return services.RegisterExample<ProxyPatternDemonstrationsExample>("Proxy Pattern Demonstrations", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddFlyweightGlyphCacheExample(this IServiceCollection services)
    {
        services.AddSingleton(new FlyweightGlyphCacheExample(FlyweightDemo.FlyweightDemo.RenderSentence));
        return services.RegisterExample<FlyweightGlyphCacheExample>("Flyweight Glyph Cache", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddTextEditorMementoExample(this IServiceCollection services)
    {
        services.AddTransient<MementoDemo.MementoDemo.TextEditor>();
        services.AddTransient<TextEditorMementoExample>(sp => new(sp.GetRequiredService<MementoDemo.MementoDemo.TextEditor>()));
        return services.RegisterExample<TextEditorMementoExample>("Text Editor Memento", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddObserverEventHubExample(this IServiceCollection services)
    {
        services.AddSingleton(_ => EventHub<UserEvent>.CreateDefault());
        services.AddSingleton<ObserverEventHubExample>(sp => new(sp.GetRequiredService<EventHub<UserEvent>>()));
        return services.RegisterExample<ObserverEventHubExample>("Observer Event Hub", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddReactiveViewModelExample(this IServiceCollection services)
    {
        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ReactiveViewModelExample>(sp => new(sp.GetRequiredService<ProfileViewModel>()));
        return services.RegisterExample<ReactiveViewModelExample>("Reactive ViewModel", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddReactiveTransactionExample(this IServiceCollection services)
    {
        services.AddTransient<ReactiveTransaction>();
        services.AddTransient<ReactiveTransactionExample>(sp => new(sp.GetRequiredService<ReactiveTransaction>()));
        return services.RegisterExample<ReactiveTransactionExample>("Reactive Transaction", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddAsyncConnectionStateMachineExample(this IServiceCollection services)
    {
        services.AddSingleton(new AsyncConnectionStateMachineExample(events => ConnectionStateDemo.RunAsync(events)));
        return services.RegisterExample<AsyncConnectionStateMachineExample>("Async Connection State Machine", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddTemplateMethodSubclassingExample(this IServiceCollection services)
    {
        services.AddTransient<DataProcessor>();
        services.AddTransient<TemplateMethodSubclassingExample>(sp => new(sp.GetRequiredService<DataProcessor>()));
        return services.RegisterExample<TemplateMethodSubclassingExample>("Template Method Subclassing", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddTemplateMethodAsyncExample(this IServiceCollection services)
    {
        services.AddTransient<AsyncDataPipeline>();
        services.AddTransient<TemplateMethodAsyncExample>(sp => new(sp.GetRequiredService<AsyncDataPipeline>()));
        return services.RegisterExample<TemplateMethodAsyncExample>("Template Method Async", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddLegacyOrderAntiCorruptionExample(this IServiceCollection services)
    {
        services.AddLegacyOrderAntiCorruptionDemo();
        services.AddSingleton<LegacyOrderAntiCorruptionExample>(sp => new(
            sp.GetRequiredService<AntiCorruptionLayer<LegacyOrderDto, CommerceOrder>>(),
            sp.GetRequiredService<LegacyOrderImportService>()));
        return services.RegisterExample<LegacyOrderAntiCorruptionExample>("Legacy Order Anti-Corruption Layer", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddInventoryRetryExample(this IServiceCollection services)
    {
        services.AddInventoryRetryDemo();
        services.AddSingleton<InventoryRetryExample>(sp => new(
            sp.GetRequiredService<RetryPolicy<InventoryResponse>>(),
            sp.GetRequiredService<InventoryLookupService>()));
        return services.RegisterExample<InventoryRetryExample>("Inventory Retry Policy", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddFulfillmentCircuitBreakerExample(this IServiceCollection services)
    {
        services.AddFulfillmentCircuitBreakerDemo();
        services.AddSingleton<FulfillmentCircuitBreakerExample>(sp => new(
            sp.GetRequiredService<CircuitBreakerPolicy<FulfillmentResponse>>(),
            sp.GetRequiredService<FulfillmentCircuitBreakerService>()));
        return services.RegisterExample<FulfillmentCircuitBreakerExample>("Fulfillment Circuit Breaker", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddShippingBulkheadExample(this IServiceCollection services)
    {
        services.AddShippingBulkheadDemo();
        services.AddSingleton<ShippingBulkheadExample>(sp => new(
            sp.GetRequiredService<BulkheadPolicy<ShippingAllocation>>(),
            sp.GetRequiredService<ShippingBulkheadService>()));
        return services.RegisterExample<ShippingBulkheadExample>("Shipping Bulkhead", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddFulfillmentQueueLoadLevelingExample(this IServiceCollection services)
    {
        services.AddFulfillmentQueueLoadLevelingDemo();
        services.AddSingleton<FulfillmentQueueLoadLevelingExample>(sp => new(
            sp.GetRequiredService<QueueLoadLevelingPolicy<FulfillmentQueueResult>>(),
            sp.GetRequiredService<FulfillmentQueueLoadLevelingService>()));
        return services.RegisterExample<FulfillmentQueueLoadLevelingExample>("Fulfillment Queue Load Leveling", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddFulfillmentPriorityQueueExample(this IServiceCollection services)
    {
        services.AddFulfillmentPriorityQueueDemo();
        services.AddSingleton<FulfillmentPriorityQueueExample>(sp => new(
            sp.GetRequiredService<PriorityQueuePolicy<FulfillmentPriorityWork, int>>(),
            sp.GetRequiredService<FulfillmentPriorityQueueService>()));
        return services.RegisterExample<FulfillmentPriorityQueueExample>("Fulfillment Priority Queue", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    public static IServiceCollection AddFulfillmentHealthEndpointExample(this IServiceCollection services)
    {
        services.AddFulfillmentHealthEndpointDemo();
        services.AddSingleton<FulfillmentHealthEndpointExample>(sp => new(
            sp.GetRequiredService<HealthEndpoint<FulfillmentHealthSnapshot>>(),
            sp.GetRequiredService<FulfillmentHealthEndpointService>()));
        return services.RegisterExample<FulfillmentHealthEndpointExample>("Fulfillment Health Endpoint Monitoring", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost | ExampleIntegrationSurface.AspNetCore);
    }

    public static IServiceCollection AddProductCatalogCacheAsideExample(this IServiceCollection services)
    {
        services.AddProductCatalogCacheAsideDemo();
        services.AddSingleton<ProductCatalogCacheAsideExample>(sp => new(
            sp.GetRequiredService<CacheAsidePolicy<ProductReadModel>>(),
            sp.GetRequiredService<ProductCatalogCacheAsideService>()));
        return services.RegisterExample<ProductCatalogCacheAsideExample>("Product Catalog Cache-Aside", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddProductSearchRateLimitingExample(this IServiceCollection services)
    {
        services.AddProductSearchRateLimitingDemo();
        services.AddSingleton<ProductSearchRateLimitingExample>(sp => new(
            sp.GetRequiredService<RateLimitPolicy<SearchResponse>>(),
            sp.GetRequiredService<ProductSearchRateLimitService>()));
        return services.RegisterExample<ProductSearchRateLimitingExample>("Product Search Rate Limiting", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection);
    }

    public static IServiceCollection AddTenantExternalConfigurationStoreExample(this IServiceCollection services)
    {
        services.AddTenantExternalConfigurationStoreDemo();
        services.AddSingleton<TenantExternalConfigurationStoreExample>(sp => new(
            sp.GetRequiredService<TenantExternalConfigurationStoreDemoRunner>(),
            sp.GetRequiredService<TenantExternalConfigurationService>()));
        return services.RegisterExample<TenantExternalConfigurationStoreExample>("Tenant External Configuration Store", ExampleIntegrationSurface.LibraryOnly | ExampleIntegrationSurface.SourceGenerator | ExampleIntegrationSurface.DependencyInjection | ExampleIntegrationSurface.GenericHost);
    }

    private static IServiceCollection RegisterExample<T>(
        this IServiceCollection services,
        string name,
        ExampleIntegrationSurface integration)
        where T : class
    {
        services.AddSingleton(new PatternKitExampleServiceDescriptor(name, typeof(T), integration));
        return services;
    }

    private sealed class DemoIdentityService : IIdentityService
    {
        public ValueTask<bool> HasVerifiedEmailAsync(Guid userId, CancellationToken ct) => new(true);
        public ValueTask<bool> HasSmsOptInAsync(Guid userId, CancellationToken ct) => new(true);
        public ValueTask<bool> HasPushTokenAsync(Guid userId, CancellationToken ct) => new(true);
    }

    private sealed class DemoPresenceService : IPresenceService
    {
        public ValueTask<bool> IsOnlineInImAsync(Guid userId, CancellationToken ct) => new(true);
        public ValueTask<bool> IsDoNotDisturbAsync(Guid userId, CancellationToken ct) => new(false);
    }

    private sealed class DemoRateLimiter : IRateLimiter
    {
        public ValueTask<bool> CanSendAsync(Channel channel, Guid userId, CancellationToken ct) => new(true);
    }

    private sealed class DemoPreferenceService : IPreferenceService
    {
        public ValueTask<Channel[]> GetPreferredOrderAsync(Guid userId, CancellationToken ct)
            => new([Channel.Push, Channel.Email, Channel.Sms]);
    }

    private sealed class DemoEmailSender : IEmailSender
    {
        public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) => new ValueTask<SendResult>(new SendResult(Channel.Email, true, "email:accepted"));
    }

    private sealed class DemoSmsSender : ISmsSender
    {
        public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) => new ValueTask<SendResult>(new SendResult(Channel.Sms, true, "sms:accepted"));
    }

    private sealed class DemoPushSender : IPushSender
    {
        public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) => new ValueTask<SendResult>(new SendResult(Channel.Push, true, "push:accepted"));
    }

    private sealed class DemoImSender : IImSender
    {
        public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) => new ValueTask<SendResult>(new SendResult(Channel.Im, true, "im:accepted"));
    }
}
