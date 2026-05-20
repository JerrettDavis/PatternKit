using PatternKit.Generators.Adapter;
using PatternKit.Generators.Bridge;
using PatternKit.Generators.Chain;
using PatternKit.Generators.Command;
using PatternKit.Generators.Composite;
using PatternKit.Generators.Composer;
using PatternKit.Generators.Decorator;
using PatternKit.Generators.Facade;
using PatternKit.Generators.Flyweight;
using PatternKit.Generators.Iterator;
using PatternKit.Generators.Messaging;
using PatternKit.Generators.Observer;
using PatternKit.Generators.Prototype;
using PatternKit.Generators.Proxy;
using PatternKit.Generators.Singleton;
using PatternKit.Generators.State;
using PatternKit.Generators.Template;
using PatternKit.Generators.Visitors;
using PatternKit.Generators;
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
        { typeof(GenerateAdapterAttribute), AttributeTargets.Class, true, false },
        { typeof(AdapterMapAttribute), AttributeTargets.Method, false, false },
        { typeof(BridgeImplementorAttribute), AttributeTargets.Interface | AttributeTargets.Class, false, false },
        { typeof(BridgeAbstractionAttribute), AttributeTargets.Class, false, false },
        { typeof(BridgeIgnoreAttribute), AttributeTargets.Method | AttributeTargets.Property, false, false },
        { typeof(ChainAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ChainHandlerAttribute), AttributeTargets.Method, false, false },
        { typeof(ChainDefaultAttribute), AttributeTargets.Method, false, false },
        { typeof(ChainTerminalAttribute), AttributeTargets.Method, false, false },
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
        { typeof(GenerateFacadeAttribute), AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, true, false },
        { typeof(FacadeExposeAttribute), AttributeTargets.Method, false, false },
        { typeof(FacadeMapAttribute), AttributeTargets.Method, false, false },
        { typeof(FacadeIgnoreAttribute), AttributeTargets.Method | AttributeTargets.Property, false, false },
        { typeof(FlyweightAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(FlyweightFactoryAttribute), AttributeTargets.Method, false, false },
        { typeof(IteratorAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(IteratorStepAttribute), AttributeTargets.Method, false, false },
        { typeof(TraversalIteratorAttribute), AttributeTargets.Class, false, false },
        { typeof(DepthFirstAttribute), AttributeTargets.Method, false, false },
        { typeof(BreadthFirstAttribute), AttributeTargets.Method, false, false },
        { typeof(TraversalChildrenAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateDispatcherAttribute), AttributeTargets.Assembly, false, true },
        { typeof(GenerateRoutingSlipAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(RoutingSlipStepAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateSagaAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(SagaStepAttribute), AttributeTargets.Method, false, false },
        { typeof(SagaCompleteWhenAttribute), AttributeTargets.Method, false, false },
        { typeof(GenerateContentRouterAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ContentRouteAttribute), AttributeTargets.Method, false, false },
        { typeof(ContentRouteDefaultAttribute), AttributeTargets.Method, false, false },
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
        { typeof(GenerateMessageEnvelopeAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(MessageEnvelopeHeaderAttribute), AttributeTargets.Class | AttributeTargets.Struct, true, false },
        { typeof(ObserverAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(ObserverHubAttribute), AttributeTargets.Class, false, false },
        { typeof(ObservedEventAttribute), AttributeTargets.Property, false, false },
        { typeof(PrototypeAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(PrototypeIgnoreAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false },
        { typeof(PrototypeIncludeAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false },
        { typeof(PrototypeStrategyAttribute), AttributeTargets.Property | AttributeTargets.Field, false, false },
        { typeof(GenerateProxyAttribute), AttributeTargets.Interface | AttributeTargets.Class, false, false },
        { typeof(ProxyIgnoreAttribute), AttributeTargets.Method | AttributeTargets.Property, false, false },
        { typeof(SingletonAttribute), AttributeTargets.Class, false, false },
        { typeof(SingletonFactoryAttribute), AttributeTargets.Method, false, false },
        { typeof(StateMachineAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(StateTransitionAttribute), AttributeTargets.Method, true, false },
        { typeof(StateGuardAttribute), AttributeTargets.Method, true, false },
        { typeof(StateEntryAttribute), AttributeTargets.Method, true, false },
        { typeof(StateExitAttribute), AttributeTargets.Method, true, false },
        { typeof(TemplateAttribute), AttributeTargets.Class | AttributeTargets.Struct, false, false },
        { typeof(TemplateStepAttribute), AttributeTargets.Method, false, false },
        { typeof(TemplateHookAttribute), AttributeTargets.Method, false, false },
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
        var envelope = new GenerateMessageEnvelopeAttribute(typeof(string))
        {
            FactoryName = "BuildEnvelope",
            ContextFactoryName = "BuildContext"
        };
        var envelopeHeader = new MessageEnvelopeHeaderAttribute("tenant-id", typeof(string))
        {
            ParameterName = "tenantId"
        };

        ScenarioExpect.Equal(typeof(string), flyweight.KeyType);
        ScenarioExpect.Equal("SymbolCache", flyweight.CacheTypeName);
        ScenarioExpect.Equal(128, flyweight.Capacity);
        ScenarioExpect.Equal(FlyweightEviction.Lru, flyweight.Eviction);
        ScenarioExpect.Equal(FlyweightThreadingPolicy.Concurrent, flyweight.Threading);
        ScenarioExpect.False(flyweight.GenerateTryGet);
        ScenarioExpect.False(iterator.GenerateEnumerator);
        ScenarioExpect.False(iterator.GenerateTryMoveNext);
        ScenarioExpect.Equal("Demo.Dispatching", dispatcher.Namespace);
        ScenarioExpect.Equal("DemoDispatcher", dispatcher.Name);
        ScenarioExpect.True(dispatcher.IncludeObjectOverloads);
        ScenarioExpect.False(dispatcher.IncludeStreaming);
        ScenarioExpect.Equal(GeneratedVisibility.Internal, dispatcher.Visibility);
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
        ScenarioExpect.Equal(typeof(string), envelope.PayloadType);
        ScenarioExpect.Equal("BuildEnvelope", envelope.FactoryName);
        ScenarioExpect.Equal("BuildContext", envelope.ContextFactoryName);
        ScenarioExpect.Equal("tenant-id", envelopeHeader.Name);
        ScenarioExpect.Equal(typeof(string), envelopeHeader.ValueType);
        ScenarioExpect.Equal("tenantId", envelopeHeader.ParameterName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateRoutingSlipAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new RoutingSlipStepAttribute("", 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSagaAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new SagaStepAttribute(null!, 1));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateContentRouterAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateContentRouterAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentException>(() => new ContentRouteAttribute("", 1, "Predicate"));
        ScenarioExpect.Throws<ArgumentException>(() => new ContentRouteAttribute("name", 1, ""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateRecipientListAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new RecipientListRecipientAttribute("", 1, "Predicate"));
        ScenarioExpect.Throws<ArgumentException>(() => new RecipientListRecipientAttribute("name", 1, ""));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSplitterAttribute(null!, typeof(int)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateSplitterAttribute(typeof(string), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAggregatorAttribute(null!, typeof(int), typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAggregatorAttribute(typeof(string), null!, typeof(decimal)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateAggregatorAttribute(typeof(string), typeof(int), null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMailboxAttribute(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => new GenerateMessageEnvelopeAttribute(null!));
        ScenarioExpect.Throws<ArgumentException>(() => new MessageEnvelopeHeaderAttribute("", typeof(string)));
        ScenarioExpect.Throws<ArgumentNullException>(() => new MessageEnvelopeHeaderAttribute("tenant-id", null!));
        ScenarioExpect.IsType<SagaCompleteWhenAttribute>(new SagaCompleteWhenAttribute());
        ScenarioExpect.IsType<ContentRouteDefaultAttribute>(new ContentRouteDefaultAttribute());
        ScenarioExpect.IsType<SplitterProjectionAttribute>(new SplitterProjectionAttribute());
        ScenarioExpect.IsType<AggregatorCorrelationAttribute>(new AggregatorCorrelationAttribute());
        ScenarioExpect.IsType<AggregatorCompletionAttribute>(new AggregatorCompletionAttribute());
        ScenarioExpect.IsType<AggregatorProjectionAttribute>(new AggregatorProjectionAttribute());
        ScenarioExpect.IsType<MailboxHandlerAttribute>(new MailboxHandlerAttribute());
        ScenarioExpect.IsType<MailboxErrorHandlerAttribute>(new MailboxErrorHandlerAttribute());
        ScenarioExpect.IsType<MailboxEventSinkAttribute>(new MailboxEventSinkAttribute());
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

    private static void AssertEnumValues<TEnum>(params TEnum[] values)
        where TEnum : struct, Enum
    {
        foreach (var value in values)
        {
            ScenarioExpect.True(Enum.IsDefined(value), $"{typeof(TEnum).Name}.{value} should be defined.");
        }
    }
}
