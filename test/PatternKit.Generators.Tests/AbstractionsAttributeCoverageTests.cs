using PatternKit.Generators.Adapter;
using PatternKit.Generators.Bridge;
using PatternKit.Generators.Bulkhead;
using PatternKit.Generators.CacheAside;
using PatternKit.Generators.CanonicalDataModel;
using PatternKit.Generators.Chain;
using PatternKit.Generators.CircuitBreaker;
using PatternKit.Generators.Cloud;
using PatternKit.Generators.Command;
using PatternKit.Generators.Composite;
using PatternKit.Generators.Composer;
using PatternKit.Generators.DataMapping;
using PatternKit.Generators.Decorator;
using PatternKit.Generators.DomainEvents;
using PatternKit.Generators.EventCarriedStateTransfer;
using PatternKit.Generators.EventNotification;
using PatternKit.Generators.EventSourcing;
using PatternKit.Generators.Facade;
using PatternKit.Generators.FeatureToggles;
using PatternKit.Generators.Flyweight;
using PatternKit.Generators.Factories;
using PatternKit.Generators.GatewayAggregation;
using PatternKit.Generators.GatewayRouting;
using PatternKit.Generators.IdentityMap;
using PatternKit.Generators.Interpreter;
using PatternKit.Generators.Iterator;
using PatternKit.Generators.HealthEndpointMonitoring;
using PatternKit.Generators.LeaderElection;
using PatternKit.Generators.MaterializedViews;
using PatternKit.Generators.Messaging;
using PatternKit.Generators.Observer;
using PatternKit.Generators.Prototype;
using PatternKit.Generators.Proxy;
using PatternKit.Generators.PriorityQueue;
using PatternKit.Generators.QueueLoadLeveling;
using PatternKit.Generators.RateLimiting;
using PatternKit.Generators.Repository;
using PatternKit.Generators.Retry;
using PatternKit.Generators.SchedulerAgentSupervisor;
using PatternKit.Generators.ServiceLayer;
using PatternKit.Generators.Sidecar;
using PatternKit.Generators.Singleton;
using PatternKit.Generators.Specification;
using PatternKit.Generators.State;
using PatternKit.Generators.StranglerFig;
using PatternKit.Generators.TableDataGateway;
using PatternKit.Generators.Template;
using PatternKit.Generators.TransactionScript;
using PatternKit.Generators.UnitOfWork;
using PatternKit.Generators.Visitors;
using PatternKit.Generators;
using PatternKit.Generators.AntiCorruption;
using PatternKit.Generators.ActivityTracking;
using PatternKit.Generators.AuditLog;
using PatternKit.Generators.Ambassador;
using PatternKit.Generators.BackendsForFrontends;
using TinyBDD;

namespace PatternKit.Generators.Tests;

public sealed class AbstractionsAttributeCoverageTests
{
    private enum TestState
    {
        Draft,
        Published
    }

    private enum TestTrigger
    {
        Publish
    }

    public static TheoryData<Type, AttributeTargets, bool, bool> AttributeUsageCases => new()
    {
        { typeof(GenerateAntiCorruptionLayerAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(AntiCorruptionTranslatorAttribute), AttributeTargets.Method, false, false },
        { typeof(AntiCorruptionExternalRuleAttribute), AttributeTargets.Method, true, false },
        { typeof(AntiCorruptionDomainRuleAttribute), AttributeTargets.Method, true, false },
        { typeof(GenerateActivityTrackerAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateAuditLogAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(AuditLogKeySelectorAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateAdapterAttribute), AttributeTargets.Class, true, false },
        { typeof(AdapterMapAttribute), AttributeTargets.Method, false, false },
        { typeof(BridgeImplementorAttribute), AttributeTargets.Interface | AttributeTargets.Class, false, false },
        { typeof(BridgeAbstractionAttribute), AttributeTargets.Class, false, false },
        { typeof(BridgeIgnoreAttribute), AttributeTargets.Method | AttributeTargets.Property, false, false },
        { typeof(GenerateBulkheadPolicyAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateCacheAsidePolicyAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(CacheAsidePredicateAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateCanonicalDataModelAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(CanonicalDataModelMapperAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateEventCarriedStateTransferAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(EventCarriedStateKeyAttribute), AttributeTargets.Method, false, false },
        { typeof(EventCarriedStateVersionAttribute), AttributeTargets.Method, false, false },
        { typeof(EventCarriedStateMapperAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateEventNotificationAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(EventNotificationKeyAttribute), AttributeTargets.Method, false, false },
        { typeof(EventNotificationCorrelationAttribute), AttributeTargets.Method, false, false },
        { typeof(EventNotificationRuleAttribute), AttributeTargets.Method, false, false },
        { typeof(EventNotificationMetadataAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateExternalConfigurationStoreAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ExternalConfigurationLoaderAttribute), AttributeTargets.Method, false, false },
        { typeof(ExternalConfigurationValidatorAttribute), AttributeTargets.Method, false, false },
        { typeof(ChainAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ChainHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(ChainDefaultAttribute), AttributeTargets.Method, false, false },
        { typeof(ChainTerminalAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateCircuitBreakerPolicyAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(CircuitBreakerResultPredicateAttribute), AttributeTargets.Method, false, false },
        { typeof(CircuitBreakerExceptionPredicateAttribute), AttributeTargets.Method, false, false },
        { typeof(CommandAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(CommandHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(CommandHostAttribute), AttributeTargets.Class, false, false },
        { typeof(CommandCaseAttribute), AttributeTargets.Method, false, false },
        { typeof(CommandUndoAttribute), AttributeTargets.Method, false, false },
        { typeof(CompositeComponentAttribute), AttributeTargets.Interface | AttributeTargets.Class, false, false },
        { typeof(CompositeIgnoreAttribute), AttributeTargets.Property | AttributeTargets.Method, false, false },
        { typeof(ComposerAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ComposeStepAttribute), AttributeTargets.Method, false, false },
        { typeof(ComposeTerminalAttribute), AttributeTargets.Method, false, false },
        { typeof(ComposeIgnoreAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateDecoratorAttribute), AttributeTargets.Interface | AttributeTargets.Class, false, false },
        { typeof(DecoratorIgnoreAttribute), AttributeTargets.Method | AttributeTargets.Property, false, false },
        { typeof(GenerateDataMapperAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(DataMapperToDataAttribute), AttributeTargets.Method, false, false },
        { typeof(DataMapperToDomainAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateDomainEventDispatcherAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(DomainEventHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateEventStoreAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateFacadeAttribute), AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, true, false },
        { typeof(FacadeExposeAttribute), AttributeTargets.Method, false, false },
        { typeof(FacadeMapAttribute), AttributeTargets.Method, false, false },
        { typeof(FacadeIgnoreAttribute), AttributeTargets.Method | AttributeTargets.Property, false, false },
        { typeof(GenerateFeatureToggleSetAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(FeatureToggleRuleAttribute), AttributeTargets.Method, false, false },
        { typeof(FlyweightAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(FlyweightFactoryAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateAbstractFactoryAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(AbstractFactoryProductAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false },
        { typeof(FactoryMethodAttribute), AttributeTargets.Class, false, false },
        { typeof(FactoryCaseAttribute), AttributeTargets.Method, true, false },
        { typeof(FactoryDefaultAttribute), AttributeTargets.Method, false, false },
        { typeof(FactoryClassAttribute), AttributeTargets.Interface | AttributeTargets.Class, false, false },
        { typeof(FactoryClassKeyAttribute), AttributeTargets.Class, false, false },
        { typeof(GenerateInterpreterAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(InterpreterTerminalAttribute), AttributeTargets.Method, true, false },
        { typeof(InterpreterNonTerminalAttribute), AttributeTargets.Method, true, false },
        { typeof(GenerateIdentityMapAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(IdentityMapKeySelectorAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateMaterializedViewAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MaterializedViewHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(IteratorAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(IteratorStepAttribute), AttributeTargets.Method, false, false },
        { typeof(TraversalIteratorAttribute), AttributeTargets.Class, false, false },
        { typeof(DepthFirstAttribute), AttributeTargets.Method, false, false },
        { typeof(BreadthFirstAttribute), AttributeTargets.Method, false, false },
        { typeof(TraversalChildrenAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateDispatcherAttribute), AttributeTargets.Assembly, false, true },
        { typeof(GenerateMessageChannelAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateMessageBusAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MessageBusRouteAttribute), AttributeTargets.Method, true, false },
        { typeof(GenerateMessagingBridgeAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateCorrelationIdentifierAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateMessageHistoryAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateChannelPurgerAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateInvalidMessageChannelAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GeneratePollingConsumerAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(PollingConsumerSourceAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateEventDrivenConsumerAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(EventDrivenConsumerHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateChannelAdapterAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ChannelAdapterInboundAttribute), AttributeTargets.Method, false, false },
        { typeof(ChannelAdapterOutboundAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateMessagingGatewayAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MessagingGatewayHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateServiceActivatorAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ServiceActivatorHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateRoutingSlipAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateCompetingConsumerGroupAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GeneratePipesAndFiltersPipelineAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(RoutingSlipStepAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateSagaAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(SagaStepAttribute), AttributeTargets.Method, false, false },
        { typeof(SagaCompleteWhenAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateContentRouterAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ContentRouteAttribute), AttributeTargets.Method, false, false },
        { typeof(ContentRouteDefaultAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateMessageFilterAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MessageFilterRuleAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateMessageExpirationAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateContentEnricherAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ContentEnrichmentStepAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateMessageStoreAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MessageStoreIdentityAttribute), AttributeTargets.Method, false, false },
        { typeof(MessageStoreRetentionAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateWireTapAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(WireTapHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateControlBusAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ControlBusCommandAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateScatterGatherAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ScatterGatherRecipientAttribute), AttributeTargets.Method, false, false },
        { typeof(ScatterGatherAggregatorAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateResequencerAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ResequencerSequenceAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateClaimCheckAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ClaimCheckStoreFactoryAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateDeadLetterChannelAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(DeadLetterStoreFactoryAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateSplitterAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(SplitterProjectionAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateAggregatorAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(AggregatorCorrelationAttribute), AttributeTargets.Method, false, false },
        { typeof(AggregatorCompletionAttribute), AttributeTargets.Method, false, false },
        { typeof(AggregatorProjectionAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateMailboxAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MailboxHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(MailboxErrorHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(MailboxEventSinkAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateReliabilityPipelineAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ReliabilityHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(ReliabilityKeySelectorAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateBackplaneTopologyAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(BackplaneRequestReplyAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false },
        { typeof(BackplaneSubscriptionAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false },
        { typeof(GenerateMessageEnvelopeAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MessageEnvelopeHeaderAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false },
        { typeof(GenerateMessageTranslatorAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MessageTranslatorHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(MessageTranslatorDropHeaderAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false },
        { typeof(MessageTranslatorHeaderAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false },
        { typeof(ObserverAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ObserverHubAttribute), AttributeTargets.Class, false, false },
        { typeof(ObservedEventAttribute), AttributeTargets.Property, false, false },
        { typeof(PrototypeAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(PrototypeIgnoreAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false },
        { typeof(PrototypeIncludeAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false },
        { typeof(PrototypeStrategyAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false },
        { typeof(GenerateProxyAttribute), AttributeTargets.Interface | AttributeTargets.Class, false, false },
        { typeof(ProxyIgnoreAttribute), AttributeTargets.Method | AttributeTargets.Property, false, false },
        { typeof(GenerateHealthEndpointAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(HealthEndpointCheckAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateGatewayAggregationAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GatewayAggregationFetchAttribute), AttributeTargets.Method, false, false },
        { typeof(GatewayAggregationComposerAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateBackendsForFrontendsAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(FrontendSelectorAttribute), AttributeTargets.Method, false, false },
        { typeof(FrontendHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(FrontendFallbackAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateAmbassadorAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(AmbassadorTransformAttribute), AttributeTargets.Method, false, false },
        { typeof(AmbassadorConnectionPolicyAttribute), AttributeTargets.Method, false, false },
        { typeof(AmbassadorTelemetryAttribute), AttributeTargets.Method, false, false },
        { typeof(AmbassadorCallAttribute), AttributeTargets.Method, false, false },
        { typeof(AmbassadorFallbackAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateLeaderElectionAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(LeaderCandidateIdAttribute), AttributeTargets.Method, false, false },
        { typeof(LeaderAcquiredAttribute), AttributeTargets.Method, false, false },
        { typeof(LeaderRenewedAttribute), AttributeTargets.Method, false, false },
        { typeof(LeaderReleasedAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateSchedulerAgentSupervisorAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(SchedulerAgentAttribute), AttributeTargets.Method, false, false },
        { typeof(SchedulerRetryWhenAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateGatewayRoutingAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GatewayRouteAttribute), AttributeTargets.Method, false, false },
        { typeof(GatewayRouteHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(GatewayRouteFallbackAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateSidecarAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(SidecarBeforeAttribute), AttributeTargets.Method, false, false },
        { typeof(SidecarAfterAttribute), AttributeTargets.Method, false, false },
        { typeof(SidecarHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateStranglerFigAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(StranglerFigRouteAttribute), AttributeTargets.Method, false, false },
        { typeof(StranglerFigLegacyAttribute), AttributeTargets.Method, false, false },
        { typeof(StranglerFigModernAttribute), AttributeTargets.Method, false, false },
        { typeof(GeneratePriorityQueueAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(PriorityQueuePrioritySelectorAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateQueueLoadLevelingPolicyAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateRateLimitPolicyAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(GenerateRepositoryAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(RepositoryKeySelectorAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateRetryPolicyAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(RetryResultPredicateAttribute), AttributeTargets.Method, false, false },
        { typeof(RetryExceptionPredicateAttribute), AttributeTargets.Method, false, false },
        { typeof(SingletonAttribute), AttributeTargets.Class, false, false },
        { typeof(SingletonFactoryAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateSpecificationRegistryAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(SpecificationRuleAttribute), AttributeTargets.Method, false, false },
        { typeof(StateMachineAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(StateTransitionAttribute), AttributeTargets.Method, true, false },
        { typeof(StateGuardAttribute), AttributeTargets.Method, true, false },
        { typeof(StateEntryAttribute), AttributeTargets.Method, true, false },
        { typeof(StateExitAttribute), AttributeTargets.Method, true, false },
        { typeof(GenerateTableDataGatewayAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(TableGatewayKeySelectorAttribute), AttributeTargets.Method, false, false },
        { typeof(TemplateAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(TemplateStepAttribute), AttributeTargets.Method, false, false },
        { typeof(TemplateHookAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateTransactionScriptAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(TransactionScriptHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(TransactionScriptValidatorAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateServiceLayerOperationAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ServiceLayerHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(ServiceLayerRuleAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateUnitOfWorkAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(UnitOfWorkStepAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateVisitorAttribute), AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, false, false }
    };

    [Scenario("AttributeUsage Is Declared As Expected")]
    [Theory]
    [MemberData(nameof(AttributeUsageCases))]
    public void AttributeUsage_Is_Declared_As_Expected(
        Type attributeType,
        AttributeTargets validOn,
        bool allowMultiple,
        bool inherited)
    {
        var usage = ScenarioExpect.Single(attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>());

        ScenarioExpect.Equal(validOn, usage.ValidOn);
        ScenarioExpect.Equal(allowMultiple, usage.AllowMultiple);
        ScenarioExpect.Equal(inherited, usage.Inherited);
    }

    [Scenario("Cache Aside Attributes Expose Defaults And Configuration")]
    [Fact]
    public void CacheAside_Attributes_Expose_Defaults_And_Configuration()
    {
        var cacheAside = new GenerateCacheAsidePolicyAttribute(typeof(string))
        {
            FactoryMethodName = "BuildProductCache",
            PolicyName = "products",
            TimeToLiveMilliseconds = 250
        };
        var externalConfig = new GenerateExternalConfigurationStoreAttribute(typeof(string))
        {
            FactoryName = "BuildTenantConfig",
            StoreName = "tenant-config",
            CacheMilliseconds = 1000
        };
        var externalValidator = new ExternalConfigurationValidatorAttribute("Endpoint is required.", 10);

        ScenarioExpect.Equal(typeof(string), cacheAside.ResultType);
        ScenarioExpect.Equal("BuildProductCache", cacheAside.FactoryMethodName);
        ScenarioExpect.Equal("products", cacheAside.PolicyName);
        ScenarioExpect.Equal(250, cacheAside.TimeToLiveMilliseconds);
        ScenarioExpect.Equal(typeof(string), externalConfig.SettingsType);
        ScenarioExpect.Equal("BuildTenantConfig", externalConfig.FactoryName);
        ScenarioExpect.Equal("tenant-config", externalConfig.StoreName);
        ScenarioExpect.Equal(1000, externalConfig.CacheMilliseconds);
        ScenarioExpect.Equal("Endpoint is required.", externalValidator.RejectionReason);
        ScenarioExpect.Equal(10, externalValidator.Order);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateCacheAsidePolicyAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateExternalConfigurationStoreAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new ExternalConfigurationValidatorAttribute("", 1));
        ScenarioExpect.IsType<CacheAsidePredicateAttribute>(new CacheAsidePredicateAttribute());
        ScenarioExpect.IsType<ExternalConfigurationLoaderAttribute>(new ExternalConfigurationLoaderAttribute());
    }

    [Scenario("Anti Corruption Attributes Expose Defaults And Validation")]
    [Fact]
    public void AntiCorruption_Attributes_Expose_Defaults_And_Validation()
    {
        var generator = new GenerateAntiCorruptionLayerAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildAcl",
            LayerName = "orders",
            SourceSystem = "legacy-erp"
        };
        var external = new AntiCorruptionExternalRuleAttribute("external invalid");
        var domain = new AntiCorruptionDomainRuleAttribute("domain invalid");

        ScenarioExpect.Equal(typeof(string), generator.ExternalType);
        ScenarioExpect.Equal(typeof(int), generator.DomainType);
        ScenarioExpect.Equal("BuildAcl", generator.FactoryMethodName);
        ScenarioExpect.Equal("orders", generator.LayerName);
        ScenarioExpect.Equal("legacy-erp", generator.SourceSystem);
        ScenarioExpect.Equal("external invalid", external.RejectionReason);
        ScenarioExpect.Equal("domain invalid", domain.RejectionReason);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAntiCorruptionLayerAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAntiCorruptionLayerAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new AntiCorruptionExternalRuleAttribute(""));
        ScenarioExpect.Throws<ArgumentException>(() => new AntiCorruptionDomainRuleAttribute(" "));
        ScenarioExpect.IsType<AntiCorruptionTranslatorAttribute>(new AntiCorruptionTranslatorAttribute());
    }

    [Scenario("Activity Tracker Attributes Expose Defaults And Configuration")]
    [Fact]
    public void ActivityTracker_Attributes_Expose_Defaults_And_Configuration()
    {
        var tracker = new GenerateActivityTrackerAttribute
        {
            FactoryMethodName = "BuildTracker",
            TrackerName = "loading-wheel"
        };

        ScenarioExpect.Equal("BuildTracker", tracker.FactoryMethodName);
        ScenarioExpect.Equal("loading-wheel", tracker.TrackerName);
    }

    [Scenario("Rate Limiting Attributes Expose Defaults And Configuration")]
    [Fact]
    public void RateLimiting_Attributes_Expose_Defaults_And_Configuration()
    {
        var rateLimit = new GenerateRateLimitPolicyAttribute(typeof(string))
        {
            FactoryMethodName = "BuildSearchLimit",
            PolicyName = "product-search",
            PermitLimit = 10,
            WindowMilliseconds = 1000
        };
        var repository = new GenerateRepositoryAttribute(typeof(string), typeof(Guid))
        {
            FactoryName = "BuildRepository"
        };
        var dataMapper = new GenerateDataMapperAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildMapper"
        };
        var identityMap = new GenerateIdentityMapAttribute(typeof(string), typeof(Guid))
        {
            FactoryName = "BuildIdentityMap"
        };

        ScenarioExpect.Equal(typeof(string), rateLimit.ResultType);
        ScenarioExpect.Equal("BuildSearchLimit", rateLimit.FactoryMethodName);
        ScenarioExpect.Equal("product-search", rateLimit.PolicyName);
        ScenarioExpect.Equal(10, rateLimit.PermitLimit);
        ScenarioExpect.Equal(1000, rateLimit.WindowMilliseconds);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateRateLimitPolicyAttribute(null!));
        ScenarioExpect.Equal(typeof(string), repository.EntityType);
        ScenarioExpect.Equal(typeof(Guid), repository.KeyType);
        ScenarioExpect.Equal("BuildRepository", repository.FactoryName);
        ScenarioExpect.Equal(typeof(string), dataMapper.DomainType);
        ScenarioExpect.Equal(typeof(int), dataMapper.DataType);
        ScenarioExpect.Equal("BuildMapper", dataMapper.FactoryName);
        ScenarioExpect.Equal(typeof(string), identityMap.EntityType);
        ScenarioExpect.Equal(typeof(Guid), identityMap.KeyType);
        ScenarioExpect.Equal("BuildIdentityMap", identityMap.FactoryName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateRepositoryAttribute(null!, typeof(Guid)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateRepositoryAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateDataMapperAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateDataMapperAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateIdentityMapAttribute(null!, typeof(Guid)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateIdentityMapAttribute(typeof(string), null!));
        ScenarioExpect.IsType<RepositoryKeySelectorAttribute>(new RepositoryKeySelectorAttribute());
        ScenarioExpect.IsType<DataMapperToDataAttribute>(new DataMapperToDataAttribute());
        ScenarioExpect.IsType<DataMapperToDomainAttribute>(new DataMapperToDomainAttribute());
        ScenarioExpect.IsType<IdentityMapKeySelectorAttribute>(new IdentityMapKeySelectorAttribute());
    }

    [Scenario("Bulkhead Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Bulkhead_Attributes_Expose_Defaults_And_Configuration()
    {
        var bulkhead = new GenerateBulkheadPolicyAttribute(typeof(string))
        {
            FactoryMethodName = "BuildFulfillmentPolicy",
            PolicyName = "fulfillment",
            MaxConcurrency = 4,
            MaxQueueLength = 8,
            QueueTimeoutMilliseconds = 250
        };

        ScenarioExpect.Equal(typeof(string), bulkhead.ResultType);
        ScenarioExpect.Equal("BuildFulfillmentPolicy", bulkhead.FactoryMethodName);
        ScenarioExpect.Equal("fulfillment", bulkhead.PolicyName);
        ScenarioExpect.Equal(4, bulkhead.MaxConcurrency);
        ScenarioExpect.Equal(8, bulkhead.MaxQueueLength);
        ScenarioExpect.Equal(250, bulkhead.QueueTimeoutMilliseconds);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateBulkheadPolicyAttribute(null!));
    }

    [Scenario("Canonical Data Model Attributes Expose Defaults And Configuration")]
    [Fact]
    public void CanonicalDataModel_Attributes_Expose_Defaults_And_Configuration()
    {
        var model = new GenerateCanonicalDataModelAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildCanonicalOrders",
            ModelName = "commerce-orders",
            AdapterName = "partner-orders"
        };

        ScenarioExpect.Equal(typeof(string), model.SourceType);
        ScenarioExpect.Equal(typeof(int), model.CanonicalType);
        ScenarioExpect.Equal("BuildCanonicalOrders", model.FactoryMethodName);
        ScenarioExpect.Equal("commerce-orders", model.ModelName);
        ScenarioExpect.Equal("partner-orders", model.AdapterName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateCanonicalDataModelAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateCanonicalDataModelAttribute(typeof(string), null!));
        ScenarioExpect.IsType<CanonicalDataModelMapperAttribute>(new CanonicalDataModelMapperAttribute());
    }

    [Scenario("Event-Carried State Transfer Attributes Expose Defaults And Configuration")]
    [Fact]
    public void EventCarriedStateTransfer_Attributes_Expose_Defaults_And_Configuration()
    {
        var transfer = new GenerateEventCarriedStateTransferAttribute(typeof(string), typeof(Guid), typeof(int))
        {
            FactoryMethodName = "BuildInventoryState",
            TransferName = "inventory-state"
        };

        ScenarioExpect.Equal(typeof(string), transfer.EventType);
        ScenarioExpect.Equal(typeof(Guid), transfer.KeyType);
        ScenarioExpect.Equal(typeof(int), transfer.StateType);
        ScenarioExpect.Equal("BuildInventoryState", transfer.FactoryMethodName);
        ScenarioExpect.Equal("inventory-state", transfer.TransferName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateEventCarriedStateTransferAttribute(null!, typeof(Guid), typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateEventCarriedStateTransferAttribute(typeof(string), null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateEventCarriedStateTransferAttribute(typeof(string), typeof(Guid), null!));
        ScenarioExpect.IsType<EventCarriedStateKeyAttribute>(new EventCarriedStateKeyAttribute());
        ScenarioExpect.IsType<EventCarriedStateVersionAttribute>(new EventCarriedStateVersionAttribute());
        ScenarioExpect.IsType<EventCarriedStateMapperAttribute>(new EventCarriedStateMapperAttribute());
    }

    [Scenario("Event Notification Attributes Expose Defaults And Configuration")]
    [Fact]
    public void EventNotification_Attributes_Expose_Defaults_And_Configuration()
    {
        var notification = new GenerateEventNotificationAttribute(typeof(string), typeof(Guid))
        {
            FactoryMethodName = "BuildOrderAccepted",
            NotificationName = "order-accepted"
        };
        var metadata = new EventNotificationMetadataAttribute("source");

        ScenarioExpect.Equal(typeof(string), notification.EventType);
        ScenarioExpect.Equal(typeof(Guid), notification.KeyType);
        ScenarioExpect.Equal("BuildOrderAccepted", notification.FactoryMethodName);
        ScenarioExpect.Equal("order-accepted", notification.NotificationName);
        ScenarioExpect.Equal("source", metadata.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateEventNotificationAttribute(null!, typeof(Guid)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateEventNotificationAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new EventNotificationMetadataAttribute(""));
        ScenarioExpect.IsType<EventNotificationKeyAttribute>(new EventNotificationKeyAttribute());
        ScenarioExpect.IsType<EventNotificationCorrelationAttribute>(new EventNotificationCorrelationAttribute());
        ScenarioExpect.IsType<EventNotificationRuleAttribute>(new EventNotificationRuleAttribute());
    }

    [Scenario("Gateway Aggregation Attributes Expose Defaults And Configuration")]
    [Fact]
    public void GatewayAggregation_Attributes_Expose_Defaults_And_Configuration()
    {
        var gateway = new GenerateGatewayAggregationAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildDashboard",
            GatewayName = "customer-dashboard"
        };
        var fetch = new GatewayAggregationFetchAttribute("profile");

        ScenarioExpect.Equal(typeof(string), gateway.RequestType);
        ScenarioExpect.Equal(typeof(int), gateway.ResponseType);
        ScenarioExpect.Equal("BuildDashboard", gateway.FactoryMethodName);
        ScenarioExpect.Equal("customer-dashboard", gateway.GatewayName);
        ScenarioExpect.Equal("profile", fetch.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateGatewayAggregationAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateGatewayAggregationAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new GatewayAggregationFetchAttribute(""));
        ScenarioExpect.IsType<GatewayAggregationComposerAttribute>(new GatewayAggregationComposerAttribute());
    }

    [Scenario("Gateway Routing Attributes Expose Defaults And Configuration")]
    [Fact]
    public void GatewayRouting_Attributes_Expose_Defaults_And_Configuration()
    {
        var gateway = new GenerateGatewayRoutingAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildGateway",
            GatewayName = "product-gateway"
        };
        var route = new GatewayRouteAttribute("inventory");
        var handler = new GatewayRouteHandlerAttribute("inventory");
        var fallback = new GatewayRouteFallbackAttribute("not-found");

        ScenarioExpect.Equal(typeof(string), gateway.RequestType);
        ScenarioExpect.Equal(typeof(int), gateway.ResponseType);
        ScenarioExpect.Equal("BuildGateway", gateway.FactoryMethodName);
        ScenarioExpect.Equal("product-gateway", gateway.GatewayName);
        ScenarioExpect.Equal("inventory", route.Name);
        ScenarioExpect.Equal("inventory", handler.Name);
        ScenarioExpect.Equal("not-found", fallback.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateGatewayRoutingAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateGatewayRoutingAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new GatewayRouteAttribute(""));
        ScenarioExpect.Throws<ArgumentException>(() => new GatewayRouteHandlerAttribute(""));
        ScenarioExpect.Throws<ArgumentException>(() => new GatewayRouteFallbackAttribute(""));
    }

    [Scenario("Sidecar Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Sidecar_Attributes_Expose_Defaults_And_Configuration()
    {
        var sidecar = new GenerateSidecarAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildOrderSidecar",
            SidecarName = "order-sidecar"
        };
        var before = new SidecarBeforeAttribute("trace");
        var after = new SidecarAfterAttribute("metrics");

        ScenarioExpect.Equal(typeof(string), sidecar.RequestType);
        ScenarioExpect.Equal(typeof(int), sidecar.ResponseType);
        ScenarioExpect.Equal("BuildOrderSidecar", sidecar.FactoryMethodName);
        ScenarioExpect.Equal("order-sidecar", sidecar.SidecarName);
        ScenarioExpect.Equal("trace", before.Name);
        ScenarioExpect.Equal("metrics", after.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSidecarAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSidecarAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new SidecarBeforeAttribute(""));
        ScenarioExpect.Throws<ArgumentException>(() => new SidecarAfterAttribute(""));
        ScenarioExpect.IsType<SidecarHandlerAttribute>(new SidecarHandlerAttribute());
    }

    [Scenario("Backends for Frontends Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Backends_For_Frontends_Attributes_Expose_Defaults_And_Configuration()
    {
        var bff = new GenerateBackendsForFrontendsAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildCommerceBff",
            GatewayName = "commerce-bff"
        };
        var selector = new FrontendSelectorAttribute("mobile");
        var handler = new FrontendHandlerAttribute("mobile");

        ScenarioExpect.Equal(typeof(string), bff.RequestType);
        ScenarioExpect.Equal(typeof(int), bff.ResponseType);
        ScenarioExpect.Equal("BuildCommerceBff", bff.FactoryMethodName);
        ScenarioExpect.Equal("commerce-bff", bff.GatewayName);
        ScenarioExpect.Equal("mobile", selector.Name);
        ScenarioExpect.Equal("mobile", handler.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateBackendsForFrontendsAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateBackendsForFrontendsAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new FrontendSelectorAttribute(""));
        ScenarioExpect.Throws<ArgumentException>(() => new FrontendHandlerAttribute(""));
        ScenarioExpect.IsType<FrontendFallbackAttribute>(new FrontendFallbackAttribute());
    }

    [Scenario("Ambassador Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Ambassador_Attributes_Expose_Defaults_And_Configuration()
    {
        var ambassador = new GenerateAmbassadorAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildInventoryAmbassador",
            AmbassadorName = "inventory-ambassador"
        };
        var telemetry = new AmbassadorTelemetryAttribute("trace");

        ScenarioExpect.Equal(typeof(string), ambassador.RequestType);
        ScenarioExpect.Equal(typeof(int), ambassador.ResponseType);
        ScenarioExpect.Equal("BuildInventoryAmbassador", ambassador.FactoryMethodName);
        ScenarioExpect.Equal("inventory-ambassador", ambassador.AmbassadorName);
        ScenarioExpect.Equal("trace", telemetry.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAmbassadorAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAmbassadorAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new AmbassadorTelemetryAttribute(""));
        ScenarioExpect.IsType<AmbassadorTransformAttribute>(new AmbassadorTransformAttribute());
        ScenarioExpect.IsType<AmbassadorConnectionPolicyAttribute>(new AmbassadorConnectionPolicyAttribute());
        ScenarioExpect.IsType<AmbassadorCallAttribute>(new AmbassadorCallAttribute());
        ScenarioExpect.IsType<AmbassadorFallbackAttribute>(new AmbassadorFallbackAttribute());
    }

    [Scenario("Leader Election Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Leader_Election_Attributes_Expose_Defaults_And_Configuration()
    {
        var leader = new GenerateLeaderElectionAttribute(typeof(string))
        {
            FactoryMethodName = "BuildLeader",
            ElectionName = "orders-leader",
            LeaseDurationMilliseconds = 5000
        };

        ScenarioExpect.Equal(typeof(string), leader.ContextType);
        ScenarioExpect.Equal("BuildLeader", leader.FactoryMethodName);
        ScenarioExpect.Equal("orders-leader", leader.ElectionName);
        ScenarioExpect.Equal(5000, leader.LeaseDurationMilliseconds);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateLeaderElectionAttribute(null!));
        ScenarioExpect.IsType<LeaderCandidateIdAttribute>(new LeaderCandidateIdAttribute());
        ScenarioExpect.IsType<LeaderAcquiredAttribute>(new LeaderAcquiredAttribute());
        ScenarioExpect.IsType<LeaderRenewedAttribute>(new LeaderRenewedAttribute());
        ScenarioExpect.IsType<LeaderReleasedAttribute>(new LeaderReleasedAttribute());
    }

    [Scenario("Scheduler Agent Supervisor Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Scheduler_Agent_Supervisor_Attributes_Expose_Defaults_And_Configuration()
    {
        var scheduler = new GenerateSchedulerAgentSupervisorAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildScheduler",
            SupervisorName = "orders-scheduler",
            MaxAttempts = 5,
            RetryDelayMilliseconds = 250
        };
        var agent = new SchedulerAgentAttribute("release-agent");

        ScenarioExpect.Equal(typeof(string), scheduler.WorkType);
        ScenarioExpect.Equal(typeof(int), scheduler.ResultType);
        ScenarioExpect.Equal("BuildScheduler", scheduler.FactoryMethodName);
        ScenarioExpect.Equal("orders-scheduler", scheduler.SupervisorName);
        ScenarioExpect.Equal(5, scheduler.MaxAttempts);
        ScenarioExpect.Equal(250, scheduler.RetryDelayMilliseconds);
        ScenarioExpect.Equal("release-agent", agent.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSchedulerAgentSupervisorAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSchedulerAgentSupervisorAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new SchedulerAgentAttribute(""));
        ScenarioExpect.IsType<SchedulerRetryWhenAttribute>(new SchedulerRetryWhenAttribute());
    }

    [Scenario("Strangler Fig Attributes Expose Defaults And Configuration")]
    [Fact]
    public void StranglerFig_Attributes_Expose_Defaults_And_Configuration()
    {
        var migration = new GenerateStranglerFigAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildCheckoutMigration",
            MigrationName = "checkout-strangler"
        };
        var route = new StranglerFigRouteAttribute("enterprise-tenant");

        ScenarioExpect.Equal(typeof(string), migration.RequestType);
        ScenarioExpect.Equal(typeof(int), migration.ResponseType);
        ScenarioExpect.Equal("BuildCheckoutMigration", migration.FactoryMethodName);
        ScenarioExpect.Equal("checkout-strangler", migration.MigrationName);
        ScenarioExpect.Equal("enterprise-tenant", route.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateStranglerFigAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateStranglerFigAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new StranglerFigRouteAttribute(""));
        ScenarioExpect.IsType<StranglerFigLegacyAttribute>(new StranglerFigLegacyAttribute());
        ScenarioExpect.IsType<StranglerFigModernAttribute>(new StranglerFigModernAttribute());
    }

    [Scenario("Priority Queue Attributes Expose Defaults And Configuration")]
    [Fact]
    public void PriorityQueue_Attributes_Expose_Defaults_And_Configuration()
    {
        var queue = new GeneratePriorityQueueAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildFulfillmentPriority",
            QueueName = "fulfillment-priority",
            DequeueHighestPriorityFirst = false
        };

        ScenarioExpect.Equal(typeof(string), queue.ItemType);
        ScenarioExpect.Equal(typeof(int), queue.PriorityType);
        ScenarioExpect.Equal("BuildFulfillmentPriority", queue.FactoryMethodName);
        ScenarioExpect.Equal("fulfillment-priority", queue.QueueName);
        ScenarioExpect.False(queue.DequeueHighestPriorityFirst);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GeneratePriorityQueueAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GeneratePriorityQueueAttribute(typeof(string), null!));
        ScenarioExpect.IsType<PriorityQueuePrioritySelectorAttribute>(new PriorityQueuePrioritySelectorAttribute());
    }

    [Scenario("Health Endpoint Attributes Expose Defaults And Configuration")]
    [Fact]
    public void HealthEndpoint_Attributes_Expose_Defaults_And_Configuration()
    {
        var endpoint = new GenerateHealthEndpointAttribute(typeof(string))
        {
            FactoryMethodName = "BuildFulfillmentHealth",
            EndpointName = "fulfillment-health"
        };
        var check = new HealthEndpointCheckAttribute("database") { Order = 2 };

        ScenarioExpect.Equal(typeof(string), endpoint.ContextType);
        ScenarioExpect.Equal("BuildFulfillmentHealth", endpoint.FactoryMethodName);
        ScenarioExpect.Equal("fulfillment-health", endpoint.EndpointName);
        ScenarioExpect.Equal("database", check.Name);
        ScenarioExpect.Equal(2, check.Order);
        ScenarioExpect.Null(new HealthEndpointCheckAttribute().Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateHealthEndpointAttribute(null!));
    }

    [Scenario("Queue Load Leveling Attributes Expose Defaults And Configuration")]
    [Fact]
    public void QueueLoadLeveling_Attributes_Expose_Defaults_And_Configuration()
    {
        var policy = new GenerateQueueLoadLevelingPolicyAttribute(typeof(string))
        {
            FactoryMethodName = "BuildFulfillmentQueue",
            PolicyName = "fulfillment-queue",
            MaxConcurrentWorkers = 2,
            MaxQueueLength = 32,
            QueueTimeoutMilliseconds = 500
        };

        ScenarioExpect.Equal(typeof(string), policy.ResultType);
        ScenarioExpect.Equal("BuildFulfillmentQueue", policy.FactoryMethodName);
        ScenarioExpect.Equal("fulfillment-queue", policy.PolicyName);
        ScenarioExpect.Equal(2, policy.MaxConcurrentWorkers);
        ScenarioExpect.Equal(32, policy.MaxQueueLength);
        ScenarioExpect.Equal(500, policy.QueueTimeoutMilliseconds);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateQueueLoadLevelingPolicyAttribute(null!));
    }

    [Scenario("Competing Consumers Attributes Expose Defaults And Configuration")]
    [Fact]
    public void CompetingConsumers_Attributes_Expose_Defaults_And_Configuration()
    {
        var group = new GenerateCompetingConsumerGroupAttribute(typeof(string), typeof(int))
        {
            FactoryMethodName = "BuildFulfillmentConsumers",
            GroupName = "fulfillment-consumers",
            MaxConcurrentDeliveries = 4
        };

        ScenarioExpect.Equal(typeof(string), group.MessageType);
        ScenarioExpect.Equal(typeof(int), group.ResultType);
        ScenarioExpect.Equal("BuildFulfillmentConsumers", group.FactoryMethodName);
        ScenarioExpect.Equal("fulfillment-consumers", group.GroupName);
        ScenarioExpect.Equal(4, group.MaxConcurrentDeliveries);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateCompetingConsumerGroupAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateCompetingConsumerGroupAttribute(typeof(string), null!));
    }

    [Scenario("Pipes And Filters Attributes Expose Defaults And Configuration")]
    [Fact]
    public void PipesAndFilters_Attributes_Expose_Defaults_And_Configuration()
    {
        var pipeline = new GeneratePipesAndFiltersPipelineAttribute(typeof(string))
        {
            FactoryMethodName = "BuildPipeline",
            PipelineName = "fulfillment-pipeline"
        };

        ScenarioExpect.Equal(typeof(string), pipeline.ContextType);
        ScenarioExpect.Equal("BuildPipeline", pipeline.FactoryMethodName);
        ScenarioExpect.Equal("fulfillment-pipeline", pipeline.PipelineName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GeneratePipesAndFiltersPipelineAttribute(null!));
    }

    [Scenario("Circuit Breaker Attributes Expose Defaults And Configuration")]
    [Fact]
    public void CircuitBreaker_Attributes_Expose_Defaults_And_Configuration()
    {
        var breaker = new GenerateCircuitBreakerPolicyAttribute(typeof(string))
        {
            FactoryMethodName = "BuildFulfillmentPolicy",
            PolicyName = "fulfillment",
            FailureThreshold = 2,
            BreakDurationMilliseconds = 500
        };

        ScenarioExpect.Equal(typeof(string), breaker.ResultType);
        ScenarioExpect.Equal("BuildFulfillmentPolicy", breaker.FactoryMethodName);
        ScenarioExpect.Equal("fulfillment", breaker.PolicyName);
        ScenarioExpect.Equal(2, breaker.FailureThreshold);
        ScenarioExpect.Equal(500, breaker.BreakDurationMilliseconds);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateCircuitBreakerPolicyAttribute(null!));
        ScenarioExpect.IsType<CircuitBreakerResultPredicateAttribute>(new CircuitBreakerResultPredicateAttribute());
        ScenarioExpect.IsType<CircuitBreakerExceptionPredicateAttribute>(new CircuitBreakerExceptionPredicateAttribute());
    }

    [Scenario("Specification Attributes Expose Defaults And Validation")]
    [Fact]
    public void Specification_Attributes_Expose_Defaults_And_Validation()
    {
        var generator = new GenerateSpecificationRegistryAttribute(typeof(string))
        {
            FactoryMethodName = "BuildRegistry"
        };
        var rule = new SpecificationRuleAttribute("approved");

        ScenarioExpect.Equal(typeof(string), generator.CandidateType);
        ScenarioExpect.Equal("BuildRegistry", generator.FactoryMethodName);
        ScenarioExpect.Equal("approved", rule.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSpecificationRegistryAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new SpecificationRuleAttribute(""));
    }

    [Scenario("Retry Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Retry_Attributes_Expose_Defaults_And_Configuration()
    {
        var retry = new GenerateRetryPolicyAttribute(typeof(string))
        {
            FactoryMethodName = "BuildInventoryPolicy",
            PolicyName = "inventory",
            MaxAttempts = 5,
            InitialDelayMilliseconds = 25,
            BackoffFactor = 2
        };

        ScenarioExpect.Equal(typeof(string), retry.ResultType);
        ScenarioExpect.Equal("BuildInventoryPolicy", retry.FactoryMethodName);
        ScenarioExpect.Equal("inventory", retry.PolicyName);
        ScenarioExpect.Equal(5, retry.MaxAttempts);
        ScenarioExpect.Equal(25, retry.InitialDelayMilliseconds);
        ScenarioExpect.Equal(2, retry.BackoffFactor);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateRetryPolicyAttribute(null!));
        ScenarioExpect.IsType<RetryResultPredicateAttribute>(new RetryResultPredicateAttribute());
        ScenarioExpect.IsType<RetryExceptionPredicateAttribute>(new RetryExceptionPredicateAttribute());
    }

    [Scenario("Interpreter Attributes Expose Defaults And Validation")]
    [Fact]
    public void Interpreter_Attributes_Expose_Defaults_And_Validation()
    {
        var generator = new GenerateInterpreterAttribute(typeof(string), typeof(decimal))
        {
            FactoryMethodName = "BuildRules"
        };
        var terminal = new InterpreterTerminalAttribute("number");
        var nonTerminal = new InterpreterNonTerminalAttribute("add");

        ScenarioExpect.Equal(typeof(string), generator.ContextType);
        ScenarioExpect.Equal(typeof(decimal), generator.ResultType);
        ScenarioExpect.Equal("BuildRules", generator.FactoryMethodName);
        ScenarioExpect.Equal("number", terminal.Name);
        ScenarioExpect.Equal("add", nonTerminal.Name);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateInterpreterAttribute(null!, typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateInterpreterAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new InterpreterTerminalAttribute(""));
        ScenarioExpect.Throws<ArgumentException>(() => new InterpreterNonTerminalAttribute(" "));
    }

    [Scenario("Adapter Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Adapter_Attributes_Expose_Defaults_And_Configuration()
    {
        var generator = new GenerateAdapterAttribute
        {
            Target = typeof(IDisposable),
            Adaptee = typeof(string),
            AdapterTypeName = "StringDisposableAdapter",
            MissingMap = AdapterMissingMapPolicy.ThrowingStub,
            Sealed = false,
            Namespace = "Demo.Adapters"
        };
        var map = new AdapterMapAttribute { TargetMember = nameof(IDisposable.Dispose) };

        ScenarioExpect.Equal(typeof(IDisposable), generator.Target);
        ScenarioExpect.Equal(typeof(string), generator.Adaptee);
        ScenarioExpect.Equal("StringDisposableAdapter", generator.AdapterTypeName);
        ScenarioExpect.Equal(AdapterMissingMapPolicy.ThrowingStub, generator.MissingMap);
        ScenarioExpect.False(generator.Sealed);
        ScenarioExpect.Equal("Demo.Adapters", generator.Namespace);
        ScenarioExpect.Equal(nameof(IDisposable.Dispose), map.TargetMember);
        AssertEnumValues(AdapterMissingMapPolicy.Error, AdapterMissingMapPolicy.ThrowingStub, AdapterMissingMapPolicy.Ignore);
    }

    [Scenario("Bridge Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Bridge_Attributes_Expose_Defaults_And_Configuration()
    {
        var implementor = new BridgeImplementorAttribute { ImplementorTypeName = "StorageBackend" };
        var abstraction = new BridgeAbstractionAttribute(typeof(IDisposable))
        {
            ImplementorPropertyName = "Backend",
            GenerateDefault = true,
            DefaultTypeName = "FileBackend"
        };

        ScenarioExpect.Equal("StorageBackend", implementor.ImplementorTypeName);
        ScenarioExpect.Equal(typeof(IDisposable), abstraction.ImplementorType);
        ScenarioExpect.Equal("Backend", abstraction.ImplementorPropertyName);
        ScenarioExpect.True(abstraction.GenerateDefault);
        ScenarioExpect.Equal("FileBackend", abstraction.DefaultTypeName);
        ScenarioExpect.IsType<BridgeIgnoreAttribute>(new BridgeIgnoreAttribute());
    }

    [Scenario("Chain And Command Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Chain_And_Command_Attributes_Expose_Defaults_And_Configuration()
    {
        var chain = new ChainAttribute
        {
            Model = ChainModel.Pipeline,
            HandleMethodName = "Run",
            TryHandleMethodName = "TryRun"
        };
        var handler = new ChainHandlerAttribute { Order = 7, Name = "Audit" };
        var command = new CommandAttribute
        {
            CommandTypeName = "SubmitOrder",
            GenerateAsync = false,
            ForceAsync = true,
            GenerateUndo = true
        };
        var commandHandler = new CommandHandlerAttribute { CommandType = typeof(string) };

        ScenarioExpect.Equal(ChainModel.Pipeline, chain.Model);
        ScenarioExpect.Equal("Run", chain.HandleMethodName);
        ScenarioExpect.Equal("TryRun", chain.TryHandleMethodName);
        ScenarioExpect.Equal(7, handler.Order);
        ScenarioExpect.Equal("Audit", handler.Name);
        ScenarioExpect.Equal("SubmitOrder", command.CommandTypeName);
        ScenarioExpect.False(command.GenerateAsync);
        ScenarioExpect.True(command.ForceAsync);
        ScenarioExpect.True(command.GenerateUndo);
        ScenarioExpect.Equal(typeof(string), commandHandler.CommandType);
        ScenarioExpect.IsType<ChainDefaultAttribute>(new ChainDefaultAttribute());
        ScenarioExpect.IsType<ChainTerminalAttribute>(new ChainTerminalAttribute());
        ScenarioExpect.IsType<CommandHostAttribute>(new CommandHostAttribute());
        ScenarioExpect.IsType<CommandCaseAttribute>(new CommandCaseAttribute());
        ScenarioExpect.IsType<CommandUndoAttribute>(new CommandUndoAttribute());
        AssertEnumValues(ChainModel.Responsibility, ChainModel.Pipeline);
    }

    [Scenario("Composite Composer And Decorator Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Composite_Composer_And_Decorator_Attributes_Expose_Defaults_And_Configuration()
    {
        var composite = new CompositeComponentAttribute
        {
            ComponentBaseName = "NodeBase",
            CompositeBaseName = "BranchBase",
            ChildrenPropertyName = "Items",
            Storage = CompositeChildrenStorage.ImmutableArray,
            GenerateTraversalHelpers = true
        };
        var composer = new ComposerAttribute
        {
            InvokeMethodName = "Run",
            InvokeAsyncMethodName = "RunAsync",
            GenerateAsync = true,
            ForceAsync = true,
            WrapOrder = ComposerWrapOrder.InnerFirst
        };
        var step = new ComposeStepAttribute(12) { Name = "Validate" };
        var decorator = new GenerateDecoratorAttribute
        {
            BaseTypeName = "StorageDecorator",
            HelpersTypeName = "StorageDecorators",
            Composition = DecoratorCompositionMode.PipelineNextStyle,
            GenerateAsync = true,
            ForceAsync = true
        };

        ScenarioExpect.Equal("NodeBase", composite.ComponentBaseName);
        ScenarioExpect.Equal("BranchBase", composite.CompositeBaseName);
        ScenarioExpect.Equal("Items", composite.ChildrenPropertyName);
        ScenarioExpect.Equal(CompositeChildrenStorage.ImmutableArray, composite.Storage);
        ScenarioExpect.True(composite.GenerateTraversalHelpers);
        ScenarioExpect.Equal("Run", composer.InvokeMethodName);
        ScenarioExpect.Equal("RunAsync", composer.InvokeAsyncMethodName);
        ScenarioExpect.True(composer.GenerateAsync);
        ScenarioExpect.True(composer.ForceAsync);
        ScenarioExpect.Equal(ComposerWrapOrder.InnerFirst, composer.WrapOrder);
        ScenarioExpect.Equal(12, step.Order);
        ScenarioExpect.Equal("Validate", step.Name);
        ScenarioExpect.Equal("StorageDecorator", decorator.BaseTypeName);
        ScenarioExpect.Equal("StorageDecorators", decorator.HelpersTypeName);
        ScenarioExpect.Equal(DecoratorCompositionMode.PipelineNextStyle, decorator.Composition);
        ScenarioExpect.True(decorator.GenerateAsync);
        ScenarioExpect.True(decorator.ForceAsync);
        ScenarioExpect.IsType<CompositeIgnoreAttribute>(new CompositeIgnoreAttribute());
        ScenarioExpect.IsType<ComposeTerminalAttribute>(new ComposeTerminalAttribute());
        ScenarioExpect.IsType<ComposeIgnoreAttribute>(new ComposeIgnoreAttribute());
        ScenarioExpect.IsType<DecoratorIgnoreAttribute>(new DecoratorIgnoreAttribute());
        AssertEnumValues(CompositeChildrenStorage.List, CompositeChildrenStorage.ImmutableArray);
        AssertEnumValues(ComposerWrapOrder.OuterFirst, ComposerWrapOrder.InnerFirst);
        AssertEnumValues(DecoratorCompositionMode.None, DecoratorCompositionMode.HelpersOnly, DecoratorCompositionMode.PipelineNextStyle);
    }

    [Scenario("Facade Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Facade_Attributes_Expose_Defaults_And_Configuration()
    {
        var facade = new GenerateFacadeAttribute
        {
            FacadeTypeName = "BillingFacade",
            GenerateAsync = false,
            ForceAsync = true,
            MissingMap = FacadeMissingMapPolicy.Stub,
            TargetTypeName = "Billing.Client",
            Include = ["CreateInvoice"],
            Exclude = ["DeleteInvoice"],
            MemberPrefix = "Billing",
            FieldName = "_billing"
        };
        var expose = new FacadeExposeAttribute { MethodName = "Checkout" };
        var map = new FacadeMapAttribute { MemberName = "GetInvoice" };

        ScenarioExpect.Equal("BillingFacade", facade.FacadeTypeName);
        ScenarioExpect.False(facade.GenerateAsync);
        ScenarioExpect.True(facade.ForceAsync);
        ScenarioExpect.Equal(FacadeMissingMapPolicy.Stub, facade.MissingMap);
        ScenarioExpect.Equal("Billing.Client", facade.TargetTypeName);
        ScenarioExpect.Equal(["CreateInvoice"], facade.Include);
        ScenarioExpect.Equal(["DeleteInvoice"], facade.Exclude);
        ScenarioExpect.Equal("Billing", facade.MemberPrefix);
        ScenarioExpect.Equal("_billing", facade.FieldName);
        ScenarioExpect.Equal("Checkout", expose.MethodName);
        ScenarioExpect.Equal("GetInvoice", map.MemberName);
        ScenarioExpect.IsType<FacadeIgnoreAttribute>(new FacadeIgnoreAttribute());
        AssertEnumValues(FacadeMissingMapPolicy.Error, FacadeMissingMapPolicy.Stub, FacadeMissingMapPolicy.Ignore);
    }

    [Scenario("Flyweight Iterator And Messaging Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Flyweight_Iterator_And_Messaging_Attributes_Expose_Defaults_And_Configuration()
    {
        var flyweight = new FlyweightAttribute(typeof(string))
        {
            CacheTypeName = "SymbolCache",
            Capacity = 128,
            Eviction = FlyweightEviction.Lru,
            Threading = FlyweightThreadingPolicy.Concurrent,
            GenerateTryGet = false
        };
        var abstractFactory = new GenerateAbstractFactoryAttribute(typeof(DayOfWeek))
        {
            FactoryMethodName = "BuildWidgets",
            ServiceProviderFactoryMethodName = "BuildWidgetsFromServices"
        };
        var abstractFactoryProduct = new AbstractFactoryProductAttribute(DayOfWeek.Monday, typeof(string), typeof(string))
        {
            IsDefaultFamily = true
        };
        var factoryMethod = new FactoryMethodAttribute(typeof(string))
        {
            CreateMethodName = "Make",
            CaseInsensitiveStrings = false
        };
        var factoryCase = new FactoryCaseAttribute("email");
        var factoryClass = new FactoryClassAttribute(typeof(int))
        {
            FactoryTypeName = "WidgetFactory",
            GenerateTryCreate = false,
            GenerateEnumKeys = true
        };
        var factoryClassKey = new FactoryClassKeyAttribute(7);
        var iterator = new IteratorAttribute
        {
            GenerateEnumerator = false,
            GenerateTryMoveNext = false
        };
        var dispatcher = new GenerateDispatcherAttribute
        {
            Namespace = "Demo.Dispatching",
            Name = "DemoDispatcher",
            IncludeObjectOverloads = true,
            IncludeStreaming = false,
            Visibility = GeneratedVisibility.Internal
        };
        var messageChannel = new GenerateMessageChannelAttribute(typeof(string))
        {
            FactoryName = "BuildChannel",
            ChannelName = "inventory",
            Capacity = 12,
            BackpressurePolicy = "DropOldest"
        };
        var messageBus = new GenerateMessageBusAttribute(typeof(string))
        {
            FactoryName = "BuildBus",
            BusName = "orders"
        };
        var messageBusRoute = new MessageBusRouteAttribute("accepted");
        var messagingBridge = new GenerateMessagingBridgeAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildBridge",
            BridgeName = "partner-commerce"
        };
        var correlationIdentifier = new GenerateCorrelationIdentifierAttribute(typeof(string))
        {
            FactoryName = "BuildCorrelation",
            HeaderName = "X-Correlation",
            PreserveExisting = false
        };
        var messageHistory = new GenerateMessageHistoryAttribute(typeof(string), "checkout-api")
        {
            FactoryName = "BuildHistory",
            Action = "received",
            HeaderName = "X-History"
        };
        var channelPurger = new GenerateChannelPurgerAttribute(typeof(string))
        {
            FactoryName = "BuildPurger",
            PurgerName = "inventory-maintenance"
        };
        var invalidMessageChannel = new GenerateInvalidMessageChannelAttribute(typeof(string))
        {
            FactoryName = "BuildInvalids",
            ChannelName = "invalid-orders"
        };
        var pollingConsumer = new GeneratePollingConsumerAttribute(typeof(string))
        {
            FactoryName = "BuildPoller",
            ConsumerName = "inventory-poller"
        };
        var eventDrivenConsumer = new GenerateEventDrivenConsumerAttribute(typeof(string))
        {
            FactoryName = "BuildConsumer",
            ConsumerName = "order-events"
        };
        var eventDrivenHandler = new EventDrivenConsumerHandlerAttribute("audit");
        var channelAdapter = new GenerateChannelAdapterAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildAdapter",
            AdapterName = "erp-orders"
        };
        var messagingGateway = new GenerateMessagingGatewayAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildGateway",
            GatewayName = "payments"
        };
        var serviceActivator = new GenerateServiceActivatorAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildActivator",
            ActivatorName = "inventory"
        };
        var routingSlip = new GenerateRoutingSlipAttribute(typeof(string))
        {
            FactoryName = "Build",
            AsyncFactoryName = "BuildAsync"
        };
        var routingStep = new RoutingSlipStepAttribute("validate", 10);
        var saga = new GenerateSagaAttribute(typeof(int))
        {
            FactoryName = "BuildSaga",
            AsyncFactoryName = "BuildSagaAsync"
        };
        var sagaStep = new SagaStepAttribute(typeof(decimal), 11);
        var router = new GenerateContentRouterAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildRouter"
        };
        var route = new ContentRouteAttribute("priority", 4, "IsPriority");
        var messageFilter = new GenerateMessageFilterAttribute(typeof(string))
        {
            FactoryName = "BuildFilter",
            FilterName = "orders",
            RejectionReason = "manual review"
        };
        var messageFilterRule = new MessageFilterRuleAttribute("trusted", 9);
        var messageExpiration = new GenerateMessageExpirationAttribute(typeof(string))
        {
            FactoryName = "BuildExpiration",
            PolicyName = "orders",
            HeaderName = "x-expires-at",
            DefaultTtlMilliseconds = 1000,
            PreserveExisting = false,
            ExpiredReason = "too old"
        };
        var messageStore = new GenerateMessageStoreAttribute(typeof(string))
        {
            FactoryName = "BuildStore",
            StoreName = "order-audit"
        };
        var wireTap = new GenerateWireTapAttribute(typeof(string))
        {
            FactoryName = "BuildTap",
            TapName = "orders-observability"
        };
        var wireTapHandler = new WireTapHandlerAttribute("audit", 12);
        var controlBus = new GenerateControlBusAttribute(typeof(string))
        {
            FactoryName = "BuildControlBus",
            BusName = "ops-control"
        };
        var controlBusCommand = new ControlBusCommandAttribute("pause", "pause-handler", 13);
        var scatterGather = new GenerateScatterGatherAttribute(typeof(string), typeof(int), typeof(decimal))
        {
            FactoryName = "BuildScatterGather",
            Name = "supplier-quotes"
        };
        var scatterRecipient = new ScatterGatherRecipientAttribute("regional", 14, "CanQuote");
        var resequencer = new GenerateResequencerAttribute(typeof(string))
        {
            FactoryName = "BuildResequencer",
            Name = "shipment-events",
            StartsAt = 10
        };
        var claimCheck = new GenerateClaimCheckAttribute(typeof(string))
        {
            FactoryName = "BuildClaimCheck",
            ClaimCheckName = "documents",
            StoreName = "blob-store",
            ClaimIdPrefix = "doc"
        };
        var deadLetter = new GenerateDeadLetterChannelAttribute(typeof(string))
        {
            FactoryName = "BuildDeadLetters",
            ChannelName = "checkout-dead",
            Source = "checkout.fulfillment",
            IdPrefix = "checkout",
            IncludeExceptionDetails = false
        };
        var recipientList = new GenerateRecipientListAttribute(typeof(string))
        {
            FactoryName = "BuildRecipients",
            AsyncFactoryName = "BuildRecipientsAsync"
        };
        var recipient = new RecipientListRecipientAttribute("priority-audit", 5, "IsPriority");
        var splitter = new GenerateSplitterAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildSplitter"
        };
        var aggregator = new GenerateAggregatorAttribute(typeof(string), typeof(int), typeof(decimal))
        {
            FactoryName = "BuildAggregator",
            DuplicatePolicy = "Replace"
        };
        var mailbox = new GenerateMailboxAttribute(typeof(string))
        {
            FactoryName = "BuildMailbox",
            Capacity = 4,
            BackpressurePolicy = "Reject",
            ErrorPolicy = "Continue"
        };
        var reliability = new GenerateReliabilityPipelineAttribute(typeof(string), typeof(int), typeof(decimal))
        {
            ReceiverFactoryName = "BuildReceiver",
            InboxFactoryName = "BuildInbox",
            OutboxFactoryName = "BuildOutbox",
            DuplicatePolicy = "ReplayCompleted",
            MissingKeyPolicy = "Process"
        };
        var backplane = new GenerateBackplaneTopologyAttribute(typeof(object))
        {
            HostBuilderType = typeof(List<>),
            ConfigureMethodName = "ApplyTopology"
        };
        var requestReply = new BackplaneRequestReplyAttribute(typeof(string), typeof(int), "orders", "Handle")
        {
            PredicateMethodName = "IsPriority"
        };
        var subscription = new BackplaneSubscriptionAttribute(typeof(decimal), "orders.submitted", "audit", "Audit");
        var envelope = new GenerateMessageEnvelopeAttribute(typeof(string))
        {
            FactoryName = "BuildEnvelope",
            ContextFactoryName = "BuildContext"
        };
        var envelopeHeader = new MessageEnvelopeHeaderAttribute("tenant-id", typeof(string))
        {
            ParameterName = "tenantId"
        };
        var translator = new GenerateMessageTranslatorAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildTranslator",
            TranslatorName = "orders",
            PreserveHeaders = false
        };
        var translatorDrop = new MessageTranslatorDropHeaderAttribute("raw-signature");
        var translatorHeader = new MessageTranslatorHeaderAttribute("content-type", "application/vnd.demo+json");

        ScenarioExpect.Equal(typeof(string), flyweight.KeyType);
        ScenarioExpect.Equal("SymbolCache", flyweight.CacheTypeName);
        ScenarioExpect.Equal(128, flyweight.Capacity);
        ScenarioExpect.Equal(FlyweightEviction.Lru, flyweight.Eviction);
        ScenarioExpect.Equal(FlyweightThreadingPolicy.Concurrent, flyweight.Threading);
        ScenarioExpect.False(flyweight.GenerateTryGet);
        ScenarioExpect.Equal(typeof(DayOfWeek), abstractFactory.KeyType);
        ScenarioExpect.Equal("BuildWidgets", abstractFactory.FactoryMethodName);
        ScenarioExpect.Equal("BuildWidgetsFromServices", abstractFactory.ServiceProviderFactoryMethodName);
        ScenarioExpect.Equal(DayOfWeek.Monday, abstractFactoryProduct.FamilyKey);
        ScenarioExpect.Equal(typeof(string), abstractFactoryProduct.ContractType);
        ScenarioExpect.Equal(typeof(string), abstractFactoryProduct.ImplementationType);
        ScenarioExpect.True(abstractFactoryProduct.IsDefaultFamily);
        ScenarioExpect.Equal(typeof(string), factoryMethod.KeyType);
        ScenarioExpect.Equal("Make", factoryMethod.CreateMethodName);
        ScenarioExpect.False(factoryMethod.CaseInsensitiveStrings);
        ScenarioExpect.Equal("email", factoryCase.Key);
        ScenarioExpect.IsType<FactoryDefaultAttribute>(new FactoryDefaultAttribute());
        ScenarioExpect.Equal(typeof(int), factoryClass.KeyType);
        ScenarioExpect.Equal("WidgetFactory", factoryClass.FactoryTypeName);
        ScenarioExpect.False(factoryClass.GenerateTryCreate);
        ScenarioExpect.True(factoryClass.GenerateEnumKeys);
        ScenarioExpect.Equal(7, factoryClassKey.Key);
        ScenarioExpect.False(iterator.GenerateEnumerator);
        ScenarioExpect.False(iterator.GenerateTryMoveNext);
        ScenarioExpect.Equal("Demo.Dispatching", dispatcher.Namespace);
        ScenarioExpect.Equal("DemoDispatcher", dispatcher.Name);
        ScenarioExpect.True(dispatcher.IncludeObjectOverloads);
        ScenarioExpect.False(dispatcher.IncludeStreaming);
        ScenarioExpect.Equal(GeneratedVisibility.Internal, dispatcher.Visibility);
        ScenarioExpect.Equal(typeof(string), messageChannel.PayloadType);
        ScenarioExpect.Equal("BuildChannel", messageChannel.FactoryName);
        ScenarioExpect.Equal("inventory", messageChannel.ChannelName);
        ScenarioExpect.Equal(12, messageChannel.Capacity);
        ScenarioExpect.Equal("DropOldest", messageChannel.BackpressurePolicy);
        ScenarioExpect.Equal(typeof(string), channelPurger.PayloadType);
        ScenarioExpect.Equal("BuildPurger", channelPurger.FactoryName);
        ScenarioExpect.Equal("inventory-maintenance", channelPurger.PurgerName);
        ScenarioExpect.Equal(typeof(string), invalidMessageChannel.PayloadType);
        ScenarioExpect.Equal("BuildInvalids", invalidMessageChannel.FactoryName);
        ScenarioExpect.Equal("invalid-orders", invalidMessageChannel.ChannelName);
        ScenarioExpect.Equal(typeof(string), pollingConsumer.PayloadType);
        ScenarioExpect.Equal("BuildPoller", pollingConsumer.FactoryName);
        ScenarioExpect.Equal("inventory-poller", pollingConsumer.ConsumerName);
        ScenarioExpect.Equal(typeof(string), eventDrivenConsumer.PayloadType);
        ScenarioExpect.Equal("BuildConsumer", eventDrivenConsumer.FactoryName);
        ScenarioExpect.Equal("order-events", eventDrivenConsumer.ConsumerName);
        ScenarioExpect.Equal("audit", eventDrivenHandler.Name);
        ScenarioExpect.Equal(typeof(string), channelAdapter.ExternalType);
        ScenarioExpect.Equal(typeof(int), channelAdapter.PayloadType);
        ScenarioExpect.Equal("BuildAdapter", channelAdapter.FactoryName);
        ScenarioExpect.Equal("erp-orders", channelAdapter.AdapterName);
        ScenarioExpect.Equal(typeof(string), messagingGateway.RequestType);
        ScenarioExpect.Equal(typeof(int), messagingGateway.ResponseType);
        ScenarioExpect.Equal("BuildGateway", messagingGateway.FactoryName);
        ScenarioExpect.Equal("payments", messagingGateway.GatewayName);
        ScenarioExpect.Equal(typeof(string), serviceActivator.RequestType);
        ScenarioExpect.Equal(typeof(int), serviceActivator.ResponseType);
        ScenarioExpect.Equal("BuildActivator", serviceActivator.FactoryName);
        ScenarioExpect.Equal("inventory", serviceActivator.ActivatorName);
        ScenarioExpect.Equal(typeof(string), routingSlip.PayloadType);
        ScenarioExpect.Equal("Build", routingSlip.FactoryName);
        ScenarioExpect.Equal("BuildAsync", routingSlip.AsyncFactoryName);
        ScenarioExpect.Equal("validate", routingStep.Name);
        ScenarioExpect.Equal(10, routingStep.Order);
        ScenarioExpect.Equal(typeof(int), saga.StateType);
        ScenarioExpect.Equal("BuildSaga", saga.FactoryName);
        ScenarioExpect.Equal("BuildSagaAsync", saga.AsyncFactoryName);
        ScenarioExpect.Equal(typeof(decimal), sagaStep.MessageType);
        ScenarioExpect.Equal(11, sagaStep.Order);
        ScenarioExpect.Equal(typeof(string), router.PayloadType);
        ScenarioExpect.Equal(typeof(int), router.ResultType);
        ScenarioExpect.Equal("BuildRouter", router.FactoryName);
        ScenarioExpect.Equal("priority", route.Name);
        ScenarioExpect.Equal(4, route.Order);
        ScenarioExpect.Equal("IsPriority", route.PredicateMethodName);
        ScenarioExpect.Equal(typeof(string), messageFilter.PayloadType);
        ScenarioExpect.Equal("BuildFilter", messageFilter.FactoryName);
        ScenarioExpect.Equal("orders", messageFilter.FilterName);
        ScenarioExpect.Equal("manual review", messageFilter.RejectionReason);
        ScenarioExpect.Equal("trusted", messageFilterRule.Name);
        ScenarioExpect.Equal(9, messageFilterRule.Order);
        ScenarioExpect.Equal(typeof(string), messageExpiration.PayloadType);
        ScenarioExpect.Equal("BuildExpiration", messageExpiration.FactoryName);
        ScenarioExpect.Equal("orders", messageExpiration.PolicyName);
        ScenarioExpect.Equal("x-expires-at", messageExpiration.HeaderName);
        ScenarioExpect.Equal(1000, messageExpiration.DefaultTtlMilliseconds);
        ScenarioExpect.False(messageExpiration.PreserveExisting);
        ScenarioExpect.Equal("too old", messageExpiration.ExpiredReason);
        ScenarioExpect.Equal(typeof(string), messageStore.PayloadType);
        ScenarioExpect.Equal("BuildStore", messageStore.FactoryName);
        ScenarioExpect.Equal("order-audit", messageStore.StoreName);
        ScenarioExpect.Equal(typeof(string), wireTap.PayloadType);
        ScenarioExpect.Equal("BuildTap", wireTap.FactoryName);
        ScenarioExpect.Equal("orders-observability", wireTap.TapName);
        ScenarioExpect.Equal("audit", wireTapHandler.Name);
        ScenarioExpect.Equal(12, wireTapHandler.Order);
        ScenarioExpect.Equal(typeof(string), controlBus.CommandType);
        ScenarioExpect.Equal("BuildControlBus", controlBus.FactoryName);
        ScenarioExpect.Equal("ops-control", controlBus.BusName);
        ScenarioExpect.Equal("pause", controlBusCommand.CommandName);
        ScenarioExpect.Equal("pause-handler", controlBusCommand.HandlerName);
        ScenarioExpect.Equal(13, controlBusCommand.Order);
        ScenarioExpect.Equal(typeof(string), scatterGather.RequestType);
        ScenarioExpect.Equal(typeof(int), scatterGather.ResponseType);
        ScenarioExpect.Equal(typeof(decimal), scatterGather.ResultType);
        ScenarioExpect.Equal("BuildScatterGather", scatterGather.FactoryName);
        ScenarioExpect.Equal("supplier-quotes", scatterGather.Name);
        ScenarioExpect.Equal("regional", scatterRecipient.Name);
        ScenarioExpect.Equal(14, scatterRecipient.Order);
        ScenarioExpect.Equal("CanQuote", scatterRecipient.PredicateMethodName);
        ScenarioExpect.Equal(typeof(string), resequencer.PayloadType);
        ScenarioExpect.Equal("BuildResequencer", resequencer.FactoryName);
        ScenarioExpect.Equal("shipment-events", resequencer.Name);
        ScenarioExpect.Equal(10, resequencer.StartsAt);
        ScenarioExpect.Equal(typeof(string), claimCheck.PayloadType);
        ScenarioExpect.Equal("BuildClaimCheck", claimCheck.FactoryName);
        ScenarioExpect.Equal("documents", claimCheck.ClaimCheckName);
        ScenarioExpect.Equal("blob-store", claimCheck.StoreName);
        ScenarioExpect.Equal("doc", claimCheck.ClaimIdPrefix);
        ScenarioExpect.Equal(typeof(string), deadLetter.PayloadType);
        ScenarioExpect.Equal("BuildDeadLetters", deadLetter.FactoryName);
        ScenarioExpect.Equal("checkout-dead", deadLetter.ChannelName);
        ScenarioExpect.Equal("checkout.fulfillment", deadLetter.Source);
        ScenarioExpect.Equal("checkout", deadLetter.IdPrefix);
        ScenarioExpect.False(deadLetter.IncludeExceptionDetails);
        ScenarioExpect.Equal(typeof(string), recipientList.PayloadType);
        ScenarioExpect.Equal("BuildRecipients", recipientList.FactoryName);
        ScenarioExpect.Equal("BuildRecipientsAsync", recipientList.AsyncFactoryName);
        ScenarioExpect.Equal("priority-audit", recipient.Name);
        ScenarioExpect.Equal(5, recipient.Order);
        ScenarioExpect.Equal("IsPriority", recipient.PredicateMethodName);
        ScenarioExpect.Equal(typeof(string), splitter.PayloadType);
        ScenarioExpect.Equal(typeof(int), splitter.ItemType);
        ScenarioExpect.Equal("BuildSplitter", splitter.FactoryName);
        ScenarioExpect.Equal(typeof(string), aggregator.KeyType);
        ScenarioExpect.Equal(typeof(int), aggregator.ItemType);
        ScenarioExpect.Equal(typeof(decimal), aggregator.ResultType);
        ScenarioExpect.Equal("BuildAggregator", aggregator.FactoryName);
        ScenarioExpect.Equal("Replace", aggregator.DuplicatePolicy);
        ScenarioExpect.Equal(typeof(string), mailbox.PayloadType);
        ScenarioExpect.Equal("BuildMailbox", mailbox.FactoryName);
        ScenarioExpect.Equal(4, mailbox.Capacity);
        ScenarioExpect.Equal("Reject", mailbox.BackpressurePolicy);
        ScenarioExpect.Equal("Continue", mailbox.ErrorPolicy);
        ScenarioExpect.Equal(typeof(string), reliability.PayloadType);
        ScenarioExpect.Equal(typeof(int), reliability.ResultType);
        ScenarioExpect.Equal(typeof(decimal), reliability.OutboxPayloadType);
        ScenarioExpect.Equal("BuildReceiver", reliability.ReceiverFactoryName);
        ScenarioExpect.Equal("BuildInbox", reliability.InboxFactoryName);
        ScenarioExpect.Equal("BuildOutbox", reliability.OutboxFactoryName);
        ScenarioExpect.Equal("ReplayCompleted", reliability.DuplicatePolicy);
        ScenarioExpect.Equal("Process", reliability.MissingKeyPolicy);
        ScenarioExpect.Equal(typeof(object), backplane.ServicesType);
        ScenarioExpect.Equal(typeof(List<>), backplane.HostBuilderType);
        ScenarioExpect.Equal("ApplyTopology", backplane.ConfigureMethodName);
        ScenarioExpect.Equal(typeof(string), requestReply.RequestType);
        ScenarioExpect.Equal(typeof(int), requestReply.ResponseType);
        ScenarioExpect.Equal("orders", requestReply.EndpointName);
        ScenarioExpect.Equal("Handle", requestReply.HandlerMethodName);
        ScenarioExpect.Equal("IsPriority", requestReply.PredicateMethodName);
        ScenarioExpect.Equal(typeof(decimal), subscription.EventType);
        ScenarioExpect.Equal("orders.submitted", subscription.Topic);
        ScenarioExpect.Equal("audit", subscription.EndpointName);
        ScenarioExpect.Equal("Audit", subscription.HandlerMethodName);
        ScenarioExpect.Equal(typeof(string), envelope.PayloadType);
        ScenarioExpect.Equal("BuildEnvelope", envelope.FactoryName);
        ScenarioExpect.Equal("BuildContext", envelope.ContextFactoryName);
        ScenarioExpect.Equal("tenant-id", envelopeHeader.Name);
        ScenarioExpect.Equal(typeof(string), envelopeHeader.ValueType);
        ScenarioExpect.Equal("tenantId", envelopeHeader.ParameterName);
        ScenarioExpect.Equal(typeof(string), translator.InputType);
        ScenarioExpect.Equal(typeof(int), translator.OutputType);
        ScenarioExpect.Equal("BuildTranslator", translator.FactoryName);
        ScenarioExpect.Equal("orders", translator.TranslatorName);
        ScenarioExpect.False(translator.PreserveHeaders);
        ScenarioExpect.Equal("raw-signature", translatorDrop.Name);
        ScenarioExpect.Equal("content-type", translatorHeader.Name);
        ScenarioExpect.Equal("application/vnd.demo+json", translatorHeader.Value);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageChannelAttribute(null!));
        ScenarioExpect.Equal(typeof(string), messageBus.PayloadType);
        ScenarioExpect.Equal("BuildBus", messageBus.FactoryName);
        ScenarioExpect.Equal("orders", messageBus.BusName);
        ScenarioExpect.Equal("accepted", messageBusRoute.Topic);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageBusAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new MessageBusRouteAttribute(""));
        ScenarioExpect.Equal(typeof(string), messagingBridge.InboundType);
        ScenarioExpect.Equal(typeof(int), messagingBridge.OutboundType);
        ScenarioExpect.Equal("BuildBridge", messagingBridge.FactoryName);
        ScenarioExpect.Equal("partner-commerce", messagingBridge.BridgeName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessagingBridgeAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessagingBridgeAttribute(typeof(string), null!));
        ScenarioExpect.Equal(typeof(string), correlationIdentifier.PayloadType);
        ScenarioExpect.Equal("BuildCorrelation", correlationIdentifier.FactoryName);
        ScenarioExpect.Equal("X-Correlation", correlationIdentifier.HeaderName);
        ScenarioExpect.False(correlationIdentifier.PreserveExisting);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateCorrelationIdentifierAttribute(null!));
        ScenarioExpect.Equal(typeof(string), messageHistory.PayloadType);
        ScenarioExpect.Equal("checkout-api", messageHistory.Component);
        ScenarioExpect.Equal("BuildHistory", messageHistory.FactoryName);
        ScenarioExpect.Equal("received", messageHistory.Action);
        ScenarioExpect.Equal("X-History", messageHistory.HeaderName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageHistoryAttribute(null!, "api"));
        ScenarioExpect.Throws<ArgumentException>(() => new GenerateMessageHistoryAttribute(typeof(string), ""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateChannelPurgerAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateInvalidMessageChannelAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GeneratePollingConsumerAttribute(null!));
        ScenarioExpect.IsType<PollingConsumerSourceAttribute>(new PollingConsumerSourceAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateEventDrivenConsumerAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new EventDrivenConsumerHandlerAttribute(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateChannelAdapterAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateChannelAdapterAttribute(typeof(string), null!));
        ScenarioExpect.IsType<ChannelAdapterInboundAttribute>(new ChannelAdapterInboundAttribute());
        ScenarioExpect.IsType<ChannelAdapterOutboundAttribute>(new ChannelAdapterOutboundAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessagingGatewayAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessagingGatewayAttribute(typeof(string), null!));
        ScenarioExpect.IsType<MessagingGatewayHandlerAttribute>(new MessagingGatewayHandlerAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateServiceActivatorAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateServiceActivatorAttribute(typeof(string), null!));
        ScenarioExpect.IsType<ServiceActivatorHandlerAttribute>(new ServiceActivatorHandlerAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateRoutingSlipAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new RoutingSlipStepAttribute("", 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSagaAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new SagaStepAttribute(null!, 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateContentRouterAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateContentRouterAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new ContentRouteAttribute("", 1, "Predicate"));
        ScenarioExpect.Throws<ArgumentException>(() => new ContentRouteAttribute("name", 1, ""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageFilterAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new MessageFilterRuleAttribute("", 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageExpirationAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageStoreAttribute(null!));
        ScenarioExpect.IsType<MessageStoreIdentityAttribute>(new MessageStoreIdentityAttribute());
        ScenarioExpect.IsType<MessageStoreRetentionAttribute>(new MessageStoreRetentionAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateWireTapAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new WireTapHandlerAttribute("", 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateControlBusAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new ControlBusCommandAttribute("", "handler"));
        ScenarioExpect.Throws<ArgumentException>(() => new ControlBusCommandAttribute("pause", ""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateScatterGatherAttribute(null!, typeof(int), typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateScatterGatherAttribute(typeof(string), null!, typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateScatterGatherAttribute(typeof(string), typeof(int), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new ScatterGatherRecipientAttribute(""));
        ScenarioExpect.IsType<ScatterGatherAggregatorAttribute>(new ScatterGatherAggregatorAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateResequencerAttribute(null!));
        ScenarioExpect.IsType<ResequencerSequenceAttribute>(new ResequencerSequenceAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateClaimCheckAttribute(null!));
        ScenarioExpect.IsType<ClaimCheckStoreFactoryAttribute>(new ClaimCheckStoreFactoryAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateDeadLetterChannelAttribute(null!));
        ScenarioExpect.IsType<DeadLetterStoreFactoryAttribute>(new DeadLetterStoreFactoryAttribute());
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateRecipientListAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new RecipientListRecipientAttribute("", 1, "Predicate"));
        ScenarioExpect.Throws<ArgumentException>(() => new RecipientListRecipientAttribute("name", 1, ""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSplitterAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSplitterAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAggregatorAttribute(null!, typeof(int), typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAggregatorAttribute(typeof(string), null!, typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAggregatorAttribute(typeof(string), typeof(int), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMailboxAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateReliabilityPipelineAttribute(null!, typeof(int), typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateReliabilityPipelineAttribute(typeof(string), null!, typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateReliabilityPipelineAttribute(typeof(string), typeof(int), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateBackplaneTopologyAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new BackplaneRequestReplyAttribute(null!, typeof(int), "orders", "Handle"));
        ScenarioExpect.Throws<ArgumentNullException>(() => new BackplaneRequestReplyAttribute(typeof(string), null!, "orders", "Handle"));
        ScenarioExpect.Throws<ArgumentException>(() => new BackplaneRequestReplyAttribute(typeof(string), typeof(int), "", "Handle"));
        ScenarioExpect.Throws<ArgumentException>(() => new BackplaneRequestReplyAttribute(typeof(string), typeof(int), "orders", ""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new BackplaneSubscriptionAttribute(null!, "orders", "audit", "Handle"));
        ScenarioExpect.Throws<ArgumentException>(() => new BackplaneSubscriptionAttribute(typeof(string), "", "audit", "Handle"));
        ScenarioExpect.Throws<ArgumentException>(() => new BackplaneSubscriptionAttribute(typeof(string), "orders", "", "Handle"));
        ScenarioExpect.Throws<ArgumentException>(() => new BackplaneSubscriptionAttribute(typeof(string), "orders", "audit", ""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageEnvelopeAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new MessageEnvelopeHeaderAttribute("", typeof(string)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new MessageEnvelopeHeaderAttribute("tenant-id", null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageTranslatorAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageTranslatorAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new MessageTranslatorDropHeaderAttribute(""));
        ScenarioExpect.Throws<ArgumentException>(() => new MessageTranslatorHeaderAttribute("", "value"));
        ScenarioExpect.Throws<ArgumentNullException>(() => new MessageTranslatorHeaderAttribute("content-type", null!));
        ScenarioExpect.IsType<MessageTranslatorHandlerAttribute>(new MessageTranslatorHandlerAttribute());
        ScenarioExpect.IsType<SagaCompleteWhenAttribute>(new SagaCompleteWhenAttribute());
        ScenarioExpect.IsType<ContentRouteDefaultAttribute>(new ContentRouteDefaultAttribute());
        ScenarioExpect.IsType<SplitterProjectionAttribute>(new SplitterProjectionAttribute());
        ScenarioExpect.IsType<AggregatorCorrelationAttribute>(new AggregatorCorrelationAttribute());
        ScenarioExpect.IsType<AggregatorCompletionAttribute>(new AggregatorCompletionAttribute());
        ScenarioExpect.IsType<AggregatorProjectionAttribute>(new AggregatorProjectionAttribute());
        ScenarioExpect.IsType<MailboxHandlerAttribute>(new MailboxHandlerAttribute());
        ScenarioExpect.IsType<MailboxErrorHandlerAttribute>(new MailboxErrorHandlerAttribute());
        ScenarioExpect.IsType<MailboxEventSinkAttribute>(new MailboxEventSinkAttribute());
        ScenarioExpect.IsType<ReliabilityHandlerAttribute>(new ReliabilityHandlerAttribute());
        ScenarioExpect.IsType<ReliabilityKeySelectorAttribute>(new ReliabilityKeySelectorAttribute());
        ScenarioExpect.IsType<FlyweightFactoryAttribute>(new FlyweightFactoryAttribute());
        ScenarioExpect.IsType<IteratorStepAttribute>(new IteratorStepAttribute());
        ScenarioExpect.IsType<TraversalIteratorAttribute>(new TraversalIteratorAttribute());
        ScenarioExpect.IsType<DepthFirstAttribute>(new DepthFirstAttribute());
        ScenarioExpect.IsType<BreadthFirstAttribute>(new BreadthFirstAttribute());
        ScenarioExpect.IsType<TraversalChildrenAttribute>(new TraversalChildrenAttribute());
        AssertEnumValues(FlyweightEviction.None, FlyweightEviction.Lru);
        AssertEnumValues(FlyweightThreadingPolicy.SingleThreadedFast, FlyweightThreadingPolicy.Locking, FlyweightThreadingPolicy.Concurrent);
        AssertEnumValues(GeneratedVisibility.Public, GeneratedVisibility.Internal);
    }

    [Scenario("Memento Prototype Observer Proxy And Singleton Attributes Expose Defaults And Configuration")]
    [Fact]
    public void Memento_Prototype_Observer_Proxy_And_Singleton_Attributes_Expose_Defaults_And_Configuration()
    {
        var memento = new MementoAttribute
        {
            GenerateCaretaker = true,
            Capacity = 10,
            InclusionMode = MementoInclusionMode.ExplicitOnly,
            SkipDuplicates = false
        };
        var mementoStrategy = new MementoStrategyAttribute(MementoCaptureStrategy.DeepCopy);
        var prototype = new PrototypeAttribute
        {
            Mode = PrototypeMode.DeepWhenPossible,
            CloneMethodName = "Copy",
            IncludeExplicit = true
        };
        var prototypeStrategy = new PrototypeStrategyAttribute(PrototypeCloneStrategy.Clone);
        var observer = new ObserverAttribute(typeof(string))
        {
            Threading = ObserverThreadingPolicy.Concurrent,
            Exceptions = ObserverExceptionPolicy.Aggregate,
            Order = ObserverOrderPolicy.Undefined,
            GenerateAsync = false,
            ForceAsync = true
        };
        var proxy = new GenerateProxyAttribute
        {
            ProxyTypeName = "BillingProxy",
            InterceptorMode = ProxyInterceptorMode.Pipeline,
            GenerateAsync = true,
            ForceAsync = true,
            Exceptions = ProxyExceptionPolicy.Swallow
        };
        var singleton = new SingletonAttribute
        {
            Mode = SingletonMode.Lazy,
            Threading = SingletonThreading.SingleThreadedFast,
            InstancePropertyName = "Current"
        };

        ScenarioExpect.True(memento.GenerateCaretaker);
        ScenarioExpect.Equal(10, memento.Capacity);
        ScenarioExpect.Equal(MementoInclusionMode.ExplicitOnly, memento.InclusionMode);
        ScenarioExpect.False(memento.SkipDuplicates);
        ScenarioExpect.Equal(MementoCaptureStrategy.DeepCopy, mementoStrategy.Strategy);
        ScenarioExpect.Equal(PrototypeMode.DeepWhenPossible, prototype.Mode);
        ScenarioExpect.Equal("Copy", prototype.CloneMethodName);
        ScenarioExpect.True(prototype.IncludeExplicit);
        ScenarioExpect.Equal(PrototypeCloneStrategy.Clone, prototypeStrategy.Strategy);
        ScenarioExpect.Equal(typeof(string), observer.PayloadType);
        ScenarioExpect.Equal(ObserverThreadingPolicy.Concurrent, observer.Threading);
        ScenarioExpect.Equal(ObserverExceptionPolicy.Aggregate, observer.Exceptions);
        ScenarioExpect.Equal(ObserverOrderPolicy.Undefined, observer.Order);
        ScenarioExpect.False(observer.GenerateAsync);
        ScenarioExpect.True(observer.ForceAsync);
        ScenarioExpect.Equal("BillingProxy", proxy.ProxyTypeName);
        ScenarioExpect.Equal(ProxyInterceptorMode.Pipeline, proxy.InterceptorMode);
        ScenarioExpect.True(proxy.GenerateAsync);
        ScenarioExpect.True(proxy.ForceAsync);
        ScenarioExpect.Equal(ProxyExceptionPolicy.Swallow, proxy.Exceptions);
        ScenarioExpect.Equal(SingletonMode.Lazy, singleton.Mode);
        ScenarioExpect.Equal(SingletonThreading.SingleThreadedFast, singleton.Threading);
        ScenarioExpect.Equal("Current", singleton.InstancePropertyName);
        ScenarioExpect.IsType<MementoIgnoreAttribute>(new MementoIgnoreAttribute());
        ScenarioExpect.IsType<MementoIncludeAttribute>(new MementoIncludeAttribute());
        ScenarioExpect.IsType<ObserverHubAttribute>(new ObserverHubAttribute());
        ScenarioExpect.IsType<ObservedEventAttribute>(new ObservedEventAttribute());
        ScenarioExpect.IsType<PrototypeIgnoreAttribute>(new PrototypeIgnoreAttribute());
        ScenarioExpect.IsType<PrototypeIncludeAttribute>(new PrototypeIncludeAttribute());
        ScenarioExpect.IsType<ProxyIgnoreAttribute>(new ProxyIgnoreAttribute());
        ScenarioExpect.IsType<SingletonFactoryAttribute>(new SingletonFactoryAttribute());
        AssertEnumValues(MementoInclusionMode.IncludeAll, MementoInclusionMode.ExplicitOnly);
        AssertEnumValues(MementoCaptureStrategy.ByReference, MementoCaptureStrategy.Clone, MementoCaptureStrategy.DeepCopy, MementoCaptureStrategy.Custom);
        AssertEnumValues(PrototypeMode.ShallowWithWarnings, PrototypeMode.Shallow, PrototypeMode.DeepWhenPossible);
        AssertEnumValues(PrototypeCloneStrategy.ByReference, PrototypeCloneStrategy.ShallowCopy, PrototypeCloneStrategy.Clone, PrototypeCloneStrategy.DeepCopy, PrototypeCloneStrategy.Custom);
        AssertEnumValues(ObserverThreadingPolicy.SingleThreadedFast, ObserverThreadingPolicy.Locking, ObserverThreadingPolicy.Concurrent);
        AssertEnumValues(ObserverExceptionPolicy.Continue, ObserverExceptionPolicy.Stop, ObserverExceptionPolicy.Aggregate);
        AssertEnumValues(ObserverOrderPolicy.RegistrationOrder, ObserverOrderPolicy.Undefined);
        AssertEnumValues(ProxyInterceptorMode.None, ProxyInterceptorMode.Single, ProxyInterceptorMode.Pipeline);
        AssertEnumValues(ProxyExceptionPolicy.Rethrow, ProxyExceptionPolicy.Swallow);
        AssertEnumValues(SingletonMode.Eager, SingletonMode.Lazy);
        AssertEnumValues(SingletonThreading.ThreadSafe, SingletonThreading.SingleThreadedFast);
    }

    [Scenario("State And Template Attributes Expose Defaults And Configuration")]
    [Fact]
    public void State_And_Template_Attributes_Expose_Defaults_And_Configuration()
    {
        var stateMachine = new StateMachineAttribute(typeof(TestState), typeof(TestTrigger))
        {
            FireMethodName = "Apply",
            FireAsyncMethodName = "ApplyAsync",
            CanFireMethodName = "CanApply",
            GenerateAsync = true,
            ForceAsync = true,
            InvalidTrigger = StateMachineInvalidTriggerPolicy.ReturnFalse,
            GuardFailure = StateMachineGuardFailurePolicy.Ignore
        };
        var transition = new StateTransitionAttribute
        {
            From = TestState.Draft,
            Trigger = TestTrigger.Publish,
            To = TestState.Published
        };
        var guard = new StateGuardAttribute
        {
            From = TestState.Draft,
            Trigger = TestTrigger.Publish
        };
        var entry = new StateEntryAttribute(TestState.Published);
        var exit = new StateExitAttribute(TestState.Draft);
        var template = new TemplateAttribute
        {
            ExecuteMethodName = "Run",
            ExecuteAsyncMethodName = "RunAsync",
            GenerateAsync = true,
            ForceAsync = true,
            ErrorPolicy = TemplateErrorPolicy.HandleAndContinue
        };
        var step = new TemplateStepAttribute(3)
        {
            Name = "Persist",
            Optional = true
        };
        var hook = new TemplateHookAttribute(HookPoint.OnError)
        {
            StepOrder = 3
        };
        var unitOfWork = new GenerateUnitOfWorkAttribute
        {
            FactoryName = "BuildCheckout"
        };
        var unitOfWorkStep = new UnitOfWorkStepAttribute("persist", 20)
        {
            RollbackMethodName = "UndoPersist"
        };
        var transactionScript = new GenerateTransactionScriptAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildSubmitOrder",
            ScriptName = "submit-order"
        };
        var serviceLayer = new GenerateServiceLayerOperationAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildRegisterCustomer",
            OperationName = "register-customer"
        };
        var serviceLayerRule = new ServiceLayerRuleAttribute("email", "Email is required.", 10);
        var domainEvents = new GenerateDomainEventDispatcherAttribute(typeof(string))
        {
            FactoryName = "BuildOrderEvents",
            DispatcherName = "order-events"
        };
        var domainEventHandler = new DomainEventHandlerAttribute(typeof(string), 20);
        var eventStore = new GenerateEventStoreAttribute(typeof(string), typeof(Guid))
        {
            FactoryName = "BuildOrderEvents",
            StoreName = "order-events"
        };
        var featureToggles = new GenerateFeatureToggleSetAttribute(typeof(string))
        {
            FactoryName = "BuildCheckoutToggles",
            SetName = "checkout"
        };
        var featureToggleRule = new FeatureToggleRuleAttribute("new-checkout")
        {
            DefaultEnabled = true
        };
        var auditLog = new GenerateAuditLogAttribute(typeof(string), typeof(Guid))
        {
            FactoryName = "BuildOrderAudit",
            LogName = "order-audit"
        };
        var materializedView = new GenerateMaterializedViewAttribute(typeof(int), typeof(string))
        {
            FactoryName = "BuildOrderReadModel",
            ViewName = "order-read-model"
        };
        var materializedViewHandler = new MaterializedViewHandlerAttribute(typeof(string))
        {
            Order = 30
        };
        var tableGateway = new GenerateTableDataGatewayAttribute(typeof(string), typeof(int))
        {
            FactoryName = "BuildOrderTable",
            TableName = "orders"
        };

        ScenarioExpect.Equal(typeof(TestState), stateMachine.StateType);
        ScenarioExpect.Equal(typeof(TestTrigger), stateMachine.TriggerType);
        ScenarioExpect.Equal("Apply", stateMachine.FireMethodName);
        ScenarioExpect.Equal("ApplyAsync", stateMachine.FireAsyncMethodName);
        ScenarioExpect.Equal("CanApply", stateMachine.CanFireMethodName);
        ScenarioExpect.True(stateMachine.GenerateAsync);
        ScenarioExpect.True(stateMachine.ForceAsync);
        ScenarioExpect.Equal(StateMachineInvalidTriggerPolicy.ReturnFalse, stateMachine.InvalidTrigger);
        ScenarioExpect.Equal(StateMachineGuardFailurePolicy.Ignore, stateMachine.GuardFailure);
        ScenarioExpect.Equal(TestState.Draft, transition.From);
        ScenarioExpect.Equal(TestTrigger.Publish, transition.Trigger);
        ScenarioExpect.Equal(TestState.Published, transition.To);
        ScenarioExpect.Equal(TestState.Draft, guard.From);
        ScenarioExpect.Equal(TestTrigger.Publish, guard.Trigger);
        ScenarioExpect.Equal(TestState.Published, entry.State);
        ScenarioExpect.Equal(TestState.Draft, exit.State);
        ScenarioExpect.Equal("Run", template.ExecuteMethodName);
        ScenarioExpect.Equal("RunAsync", template.ExecuteAsyncMethodName);
        ScenarioExpect.True(template.GenerateAsync);
        ScenarioExpect.True(template.ForceAsync);
        ScenarioExpect.Equal(TemplateErrorPolicy.HandleAndContinue, template.ErrorPolicy);
        ScenarioExpect.Equal(3, step.Order);
        ScenarioExpect.Equal("Persist", step.Name);
        ScenarioExpect.True(step.Optional);
        ScenarioExpect.Equal(HookPoint.OnError, hook.HookPoint);
        ScenarioExpect.Equal(3, hook.StepOrder);
        ScenarioExpect.Equal("BuildCheckout", unitOfWork.FactoryName);
        ScenarioExpect.Equal("persist", unitOfWorkStep.Name);
        ScenarioExpect.Equal(20, unitOfWorkStep.Order);
        ScenarioExpect.Equal("UndoPersist", unitOfWorkStep.RollbackMethodName);
        ScenarioExpect.Equal(typeof(string), transactionScript.RequestType);
        ScenarioExpect.Equal(typeof(int), transactionScript.ResponseType);
        ScenarioExpect.Equal("BuildSubmitOrder", transactionScript.FactoryName);
        ScenarioExpect.Equal("submit-order", transactionScript.ScriptName);
        ScenarioExpect.Equal(typeof(string), serviceLayer.RequestType);
        ScenarioExpect.Equal(typeof(int), serviceLayer.ResponseType);
        ScenarioExpect.Equal("BuildRegisterCustomer", serviceLayer.FactoryName);
        ScenarioExpect.Equal("register-customer", serviceLayer.OperationName);
        ScenarioExpect.Equal("email", serviceLayerRule.Code);
        ScenarioExpect.Equal("Email is required.", serviceLayerRule.Message);
        ScenarioExpect.Equal(10, serviceLayerRule.Order);
        ScenarioExpect.Equal(typeof(string), domainEvents.EventBaseType);
        ScenarioExpect.Equal("BuildOrderEvents", domainEvents.FactoryName);
        ScenarioExpect.Equal("order-events", domainEvents.DispatcherName);
        ScenarioExpect.Equal(typeof(string), domainEventHandler.EventType);
        ScenarioExpect.Equal(20, domainEventHandler.Order);
        ScenarioExpect.Equal(typeof(string), eventStore.EventType);
        ScenarioExpect.Equal(typeof(Guid), eventStore.StreamIdType);
        ScenarioExpect.Equal("BuildOrderEvents", eventStore.FactoryName);
        ScenarioExpect.Equal("order-events", eventStore.StoreName);
        ScenarioExpect.Equal(typeof(string), featureToggles.ContextType);
        ScenarioExpect.Equal("BuildCheckoutToggles", featureToggles.FactoryName);
        ScenarioExpect.Equal("checkout", featureToggles.SetName);
        ScenarioExpect.Equal("new-checkout", featureToggleRule.Name);
        ScenarioExpect.True(featureToggleRule.DefaultEnabled);
        ScenarioExpect.Equal(typeof(string), auditLog.EntryType);
        ScenarioExpect.Equal(typeof(Guid), auditLog.KeyType);
        ScenarioExpect.Equal("BuildOrderAudit", auditLog.FactoryName);
        ScenarioExpect.Equal("order-audit", auditLog.LogName);
        ScenarioExpect.Equal(typeof(int), materializedView.StateType);
        ScenarioExpect.Equal(typeof(string), materializedView.EventType);
        ScenarioExpect.Equal("BuildOrderReadModel", materializedView.FactoryName);
        ScenarioExpect.Equal("order-read-model", materializedView.ViewName);
        ScenarioExpect.Equal(typeof(string), materializedViewHandler.EventType);
        ScenarioExpect.Equal(30, materializedViewHandler.Order);
        ScenarioExpect.Equal(typeof(string), tableGateway.RowType);
        ScenarioExpect.Equal(typeof(int), tableGateway.KeyType);
        ScenarioExpect.Equal("BuildOrderTable", tableGateway.FactoryName);
        ScenarioExpect.Equal("orders", tableGateway.TableName);
        ScenarioExpect.Throws<ArgumentException>(() => new UnitOfWorkStepAttribute("", 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateTransactionScriptAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateTransactionScriptAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateServiceLayerOperationAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateServiceLayerOperationAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new ServiceLayerRuleAttribute("", "message", 1));
        ScenarioExpect.Throws<ArgumentException>(() => new ServiceLayerRuleAttribute("code", "", 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateDomainEventDispatcherAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new DomainEventHandlerAttribute(null!, 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateEventStoreAttribute(null!, typeof(Guid)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateEventStoreAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateFeatureToggleSetAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new FeatureToggleRuleAttribute(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAuditLogAttribute(null!, typeof(Guid)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAuditLogAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMaterializedViewAttribute(null!, typeof(string)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMaterializedViewAttribute(typeof(int), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new MaterializedViewHandlerAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateTableDataGatewayAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateTableDataGatewayAttribute(typeof(string), null!));
        ScenarioExpect.IsType<TableGatewayKeySelectorAttribute>(new TableGatewayKeySelectorAttribute());
        ScenarioExpect.IsType<AuditLogKeySelectorAttribute>(new AuditLogKeySelectorAttribute());
        ScenarioExpect.IsType<TransactionScriptHandlerAttribute>(new TransactionScriptHandlerAttribute());
        ScenarioExpect.IsType<TransactionScriptValidatorAttribute>(new TransactionScriptValidatorAttribute());
        ScenarioExpect.IsType<ServiceLayerHandlerAttribute>(new ServiceLayerHandlerAttribute());
        AssertEnumValues(StateMachineInvalidTriggerPolicy.Throw, StateMachineInvalidTriggerPolicy.Ignore, StateMachineInvalidTriggerPolicy.ReturnFalse);
        AssertEnumValues(StateMachineGuardFailurePolicy.Throw, StateMachineGuardFailurePolicy.Ignore, StateMachineGuardFailurePolicy.ReturnFalse);
        AssertEnumValues(HookPoint.BeforeAll, HookPoint.AfterAll, HookPoint.OnError);
        AssertEnumValues(TemplateErrorPolicy.Rethrow, TemplateErrorPolicy.HandleAndContinue);
    }

    [Scenario("Visitor Attribute Exposes Defaults And Configuration")]
    [Fact]
    public void Visitor_Attribute_Exposes_Defaults_And_Configuration()
    {
        var defaults = new GenerateVisitorAttribute();
        var configured = new GenerateVisitorAttribute
        {
            VisitorInterfaceName = "IWorkflowNodeVisitor",
            GenerateAsync = false,
            GenerateActions = false,
            AutoDiscoverDerivedTypes = false
        };

        ScenarioExpect.Null(defaults.VisitorInterfaceName);
        ScenarioExpect.True(defaults.GenerateAsync);
        ScenarioExpect.True(defaults.GenerateActions);
        ScenarioExpect.True(defaults.AutoDiscoverDerivedTypes);
        ScenarioExpect.Equal("IWorkflowNodeVisitor", configured.VisitorInterfaceName);
        ScenarioExpect.False(configured.GenerateAsync);
        ScenarioExpect.False(configured.GenerateActions);
        ScenarioExpect.False(configured.AutoDiscoverDerivedTypes);
    }

    [Scenario("Content Enricher Attributes Expose Defaults And Configuration")]
    [Fact]
    public void ContentEnricher_Attributes_Expose_Defaults_And_Configuration()
    {
        var generator = new GenerateContentEnricherAttribute(typeof(string))
        {
            FactoryName = "BuildCustomerProfile",
            EnricherName = "customer-profile",
            DefaultPolicy = ContentEnrichmentErrorPolicy.Skip
        };
        var step = new ContentEnrichmentStepAttribute("normalize-email")
        {
            Order = 20,
            Policy = ContentEnrichmentErrorPolicy.UseDefault,
            DefaultFactoryName = "UseDefaultEmail"
        };

        ScenarioExpect.Equal(typeof(string), generator.PayloadType);
        ScenarioExpect.Equal("BuildCustomerProfile", generator.FactoryName);
        ScenarioExpect.Equal("customer-profile", generator.EnricherName);
        ScenarioExpect.Equal(ContentEnrichmentErrorPolicy.Skip, generator.DefaultPolicy);
        ScenarioExpect.Equal("normalize-email", step.Name);
        ScenarioExpect.Equal(20, step.Order);
        ScenarioExpect.Equal(ContentEnrichmentErrorPolicy.UseDefault, step.Policy);
        ScenarioExpect.Equal("UseDefaultEmail", step.DefaultFactoryName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateContentEnricherAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new ContentEnrichmentStepAttribute(" "));
        AssertEnumValues(ContentEnrichmentErrorPolicy.Throw, ContentEnrichmentErrorPolicy.Skip, ContentEnrichmentErrorPolicy.UseDefault);
    }

    [Scenario("Guaranteed Delivery Attribute Exposes Defaults And Configuration")]
    [Fact]
    public void GuaranteedDelivery_Attribute_Exposes_Defaults_And_Configuration()
    {
        var generator = new GenerateGuaranteedDeliveryAttribute(typeof(string))
        {
            FactoryName = "BuildShipmentDelivery",
            QueueName = "shipment-delivery",
            LeaseMilliseconds = 45000,
            MaxDeliveryAttempts = 7
        };

        ScenarioExpect.Equal(typeof(string), generator.PayloadType);
        ScenarioExpect.Equal("BuildShipmentDelivery", generator.FactoryName);
        ScenarioExpect.Equal("shipment-delivery", generator.QueueName);
        ScenarioExpect.Equal(45000, generator.LeaseMilliseconds);
        ScenarioExpect.Equal(7, generator.MaxDeliveryAttempts);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateGuaranteedDeliveryAttribute(null!));
    }

    private static void AssertEnumValues<TEnum>(params TEnum[] values)
        where TEnum : struct, Enum
    {
        foreach (var value in values)
        {
            ScenarioExpect.True(Enum.IsDefined(value), $"{typeof(TEnum).Name}.{value} should be defined.");
        }
    }
}
