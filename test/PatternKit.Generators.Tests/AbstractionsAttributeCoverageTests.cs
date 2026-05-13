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
using PatternKit.Generators;

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
        { typeof(TemplateHookAttribute), AttributeTargets.Method, false, false }
    };

    [Theory]
    [MemberData(nameof(AttributeUsageCases))]
    public void AttributeUsage_Is_Declared_As_Expected(
        Type attributeType,
        AttributeTargets validOn,
        bool allowMultiple,
        bool inherited)
    {
        var usage = Assert.Single(attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>());

        Assert.Equal(validOn, usage.ValidOn);
        Assert.Equal(allowMultiple, usage.AllowMultiple);
        Assert.Equal(inherited, usage.Inherited);
    }

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

        Assert.Equal(typeof(IDisposable), generator.Target);
        Assert.Equal(typeof(string), generator.Adaptee);
        Assert.Equal("StringDisposableAdapter", generator.AdapterTypeName);
        Assert.Equal(AdapterMissingMapPolicy.ThrowingStub, generator.MissingMap);
        Assert.False(generator.Sealed);
        Assert.Equal("Demo.Adapters", generator.Namespace);
        Assert.Equal(nameof(IDisposable.Dispose), map.TargetMember);
        AssertEnumValues(AdapterMissingMapPolicy.Error, AdapterMissingMapPolicy.ThrowingStub, AdapterMissingMapPolicy.Ignore);
    }

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

        Assert.Equal("StorageBackend", implementor.ImplementorTypeName);
        Assert.Equal(typeof(IDisposable), abstraction.ImplementorType);
        Assert.Equal("Backend", abstraction.ImplementorPropertyName);
        Assert.True(abstraction.GenerateDefault);
        Assert.Equal("FileBackend", abstraction.DefaultTypeName);
        Assert.IsType<BridgeIgnoreAttribute>(new BridgeIgnoreAttribute());
    }

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

        Assert.Equal(ChainModel.Pipeline, chain.Model);
        Assert.Equal("Run", chain.HandleMethodName);
        Assert.Equal("TryRun", chain.TryHandleMethodName);
        Assert.Equal(7, handler.Order);
        Assert.Equal("Audit", handler.Name);
        Assert.Equal("SubmitOrder", command.CommandTypeName);
        Assert.False(command.GenerateAsync);
        Assert.True(command.ForceAsync);
        Assert.True(command.GenerateUndo);
        Assert.Equal(typeof(string), commandHandler.CommandType);
        Assert.IsType<ChainDefaultAttribute>(new ChainDefaultAttribute());
        Assert.IsType<ChainTerminalAttribute>(new ChainTerminalAttribute());
        Assert.IsType<CommandHostAttribute>(new CommandHostAttribute());
        Assert.IsType<CommandCaseAttribute>(new CommandCaseAttribute());
        Assert.IsType<CommandUndoAttribute>(new CommandUndoAttribute());
        AssertEnumValues(ChainModel.Responsibility, ChainModel.Pipeline);
    }

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

        Assert.Equal("NodeBase", composite.ComponentBaseName);
        Assert.Equal("BranchBase", composite.CompositeBaseName);
        Assert.Equal("Items", composite.ChildrenPropertyName);
        Assert.Equal(CompositeChildrenStorage.ImmutableArray, composite.Storage);
        Assert.True(composite.GenerateTraversalHelpers);
        Assert.Equal("Run", composer.InvokeMethodName);
        Assert.Equal("RunAsync", composer.InvokeAsyncMethodName);
        Assert.True(composer.GenerateAsync);
        Assert.True(composer.ForceAsync);
        Assert.Equal(ComposerWrapOrder.InnerFirst, composer.WrapOrder);
        Assert.Equal(12, step.Order);
        Assert.Equal("Validate", step.Name);
        Assert.Equal("StorageDecorator", decorator.BaseTypeName);
        Assert.Equal("StorageDecorators", decorator.HelpersTypeName);
        Assert.Equal(DecoratorCompositionMode.PipelineNextStyle, decorator.Composition);
        Assert.True(decorator.GenerateAsync);
        Assert.True(decorator.ForceAsync);
        Assert.IsType<CompositeIgnoreAttribute>(new CompositeIgnoreAttribute());
        Assert.IsType<ComposeTerminalAttribute>(new ComposeTerminalAttribute());
        Assert.IsType<ComposeIgnoreAttribute>(new ComposeIgnoreAttribute());
        Assert.IsType<DecoratorIgnoreAttribute>(new DecoratorIgnoreAttribute());
        AssertEnumValues(CompositeChildrenStorage.List, CompositeChildrenStorage.ImmutableArray);
        AssertEnumValues(ComposerWrapOrder.OuterFirst, ComposerWrapOrder.InnerFirst);
        AssertEnumValues(DecoratorCompositionMode.None, DecoratorCompositionMode.HelpersOnly, DecoratorCompositionMode.PipelineNextStyle);
    }

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

        Assert.Equal("BillingFacade", facade.FacadeTypeName);
        Assert.False(facade.GenerateAsync);
        Assert.True(facade.ForceAsync);
        Assert.Equal(FacadeMissingMapPolicy.Stub, facade.MissingMap);
        Assert.Equal("Billing.Client", facade.TargetTypeName);
        Assert.Equal(["CreateInvoice"], facade.Include);
        Assert.Equal(["DeleteInvoice"], facade.Exclude);
        Assert.Equal("Billing", facade.MemberPrefix);
        Assert.Equal("_billing", facade.FieldName);
        Assert.Equal("Checkout", expose.MethodName);
        Assert.Equal("GetInvoice", map.MemberName);
        Assert.IsType<FacadeIgnoreAttribute>(new FacadeIgnoreAttribute());
        AssertEnumValues(FacadeMissingMapPolicy.Error, FacadeMissingMapPolicy.Stub, FacadeMissingMapPolicy.Ignore);
    }

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

        Assert.Equal(typeof(string), flyweight.KeyType);
        Assert.Equal("SymbolCache", flyweight.CacheTypeName);
        Assert.Equal(128, flyweight.Capacity);
        Assert.Equal(FlyweightEviction.Lru, flyweight.Eviction);
        Assert.Equal(FlyweightThreadingPolicy.Concurrent, flyweight.Threading);
        Assert.False(flyweight.GenerateTryGet);
        Assert.False(iterator.GenerateEnumerator);
        Assert.False(iterator.GenerateTryMoveNext);
        Assert.Equal("Demo.Dispatching", dispatcher.Namespace);
        Assert.Equal("DemoDispatcher", dispatcher.Name);
        Assert.True(dispatcher.IncludeObjectOverloads);
        Assert.False(dispatcher.IncludeStreaming);
        Assert.Equal(GeneratedVisibility.Internal, dispatcher.Visibility);
        Assert.Equal(typeof(string), routingSlip.PayloadType);
        Assert.Equal("Build", routingSlip.FactoryName);
        Assert.Equal("BuildAsync", routingSlip.AsyncFactoryName);
        Assert.Equal("validate", routingStep.Name);
        Assert.Equal(10, routingStep.Order);
        Assert.Equal(typeof(int), saga.StateType);
        Assert.Equal("BuildSaga", saga.FactoryName);
        Assert.Equal("BuildSagaAsync", saga.AsyncFactoryName);
        Assert.Equal(typeof(decimal), sagaStep.MessageType);
        Assert.Equal(11, sagaStep.Order);
        Assert.Throws<ArgumentNullException>(() => new GenerateRoutingSlipAttribute(null!));
        Assert.Throws<ArgumentException>(() => new RoutingSlipStepAttribute("", 1));
        Assert.Throws<ArgumentNullException>(() => new GenerateSagaAttribute(null!));
        Assert.Throws<ArgumentNullException>(() => new SagaStepAttribute(null!, 1));
        Assert.IsType<SagaCompleteWhenAttribute>(new SagaCompleteWhenAttribute());
        Assert.IsType<FlyweightFactoryAttribute>(new FlyweightFactoryAttribute());
        Assert.IsType<IteratorStepAttribute>(new IteratorStepAttribute());
        Assert.IsType<TraversalIteratorAttribute>(new TraversalIteratorAttribute());
        Assert.IsType<DepthFirstAttribute>(new DepthFirstAttribute());
        Assert.IsType<BreadthFirstAttribute>(new BreadthFirstAttribute());
        Assert.IsType<TraversalChildrenAttribute>(new TraversalChildrenAttribute());
        AssertEnumValues(FlyweightEviction.None, FlyweightEviction.Lru);
        AssertEnumValues(FlyweightThreadingPolicy.SingleThreadedFast, FlyweightThreadingPolicy.Locking, FlyweightThreadingPolicy.Concurrent);
        AssertEnumValues(GeneratedVisibility.Public, GeneratedVisibility.Internal);
    }

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

        Assert.True(memento.GenerateCaretaker);
        Assert.Equal(10, memento.Capacity);
        Assert.Equal(MementoInclusionMode.ExplicitOnly, memento.InclusionMode);
        Assert.False(memento.SkipDuplicates);
        Assert.Equal(MementoCaptureStrategy.DeepCopy, mementoStrategy.Strategy);
        Assert.Equal(PrototypeMode.DeepWhenPossible, prototype.Mode);
        Assert.Equal("Copy", prototype.CloneMethodName);
        Assert.True(prototype.IncludeExplicit);
        Assert.Equal(PrototypeCloneStrategy.Clone, prototypeStrategy.Strategy);
        Assert.Equal(typeof(string), observer.PayloadType);
        Assert.Equal(ObserverThreadingPolicy.Concurrent, observer.Threading);
        Assert.Equal(ObserverExceptionPolicy.Aggregate, observer.Exceptions);
        Assert.Equal(ObserverOrderPolicy.Undefined, observer.Order);
        Assert.False(observer.GenerateAsync);
        Assert.True(observer.ForceAsync);
        Assert.Equal("BillingProxy", proxy.ProxyTypeName);
        Assert.Equal(ProxyInterceptorMode.Pipeline, proxy.InterceptorMode);
        Assert.True(proxy.GenerateAsync);
        Assert.True(proxy.ForceAsync);
        Assert.Equal(ProxyExceptionPolicy.Swallow, proxy.Exceptions);
        Assert.Equal(SingletonMode.Lazy, singleton.Mode);
        Assert.Equal(SingletonThreading.SingleThreadedFast, singleton.Threading);
        Assert.Equal("Current", singleton.InstancePropertyName);
        Assert.IsType<MementoIgnoreAttribute>(new MementoIgnoreAttribute());
        Assert.IsType<MementoIncludeAttribute>(new MementoIncludeAttribute());
        Assert.IsType<ObserverHubAttribute>(new ObserverHubAttribute());
        Assert.IsType<ObservedEventAttribute>(new ObservedEventAttribute());
        Assert.IsType<PrototypeIgnoreAttribute>(new PrototypeIgnoreAttribute());
        Assert.IsType<PrototypeIncludeAttribute>(new PrototypeIncludeAttribute());
        Assert.IsType<ProxyIgnoreAttribute>(new ProxyIgnoreAttribute());
        Assert.IsType<SingletonFactoryAttribute>(new SingletonFactoryAttribute());
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

        Assert.Equal(typeof(TestState), stateMachine.StateType);
        Assert.Equal(typeof(TestTrigger), stateMachine.TriggerType);
        Assert.Equal("Apply", stateMachine.FireMethodName);
        Assert.Equal("ApplyAsync", stateMachine.FireAsyncMethodName);
        Assert.Equal("CanApply", stateMachine.CanFireMethodName);
        Assert.True(stateMachine.GenerateAsync);
        Assert.True(stateMachine.ForceAsync);
        Assert.Equal(StateMachineInvalidTriggerPolicy.ReturnFalse, stateMachine.InvalidTrigger);
        Assert.Equal(StateMachineGuardFailurePolicy.Ignore, stateMachine.GuardFailure);
        Assert.Equal(TestState.Draft, transition.From);
        Assert.Equal(TestTrigger.Publish, transition.Trigger);
        Assert.Equal(TestState.Published, transition.To);
        Assert.Equal(TestState.Draft, guard.From);
        Assert.Equal(TestTrigger.Publish, guard.Trigger);
        Assert.Equal(TestState.Published, entry.State);
        Assert.Equal(TestState.Draft, exit.State);
        Assert.Equal("Run", template.ExecuteMethodName);
        Assert.Equal("RunAsync", template.ExecuteAsyncMethodName);
        Assert.True(template.GenerateAsync);
        Assert.True(template.ForceAsync);
        Assert.Equal(TemplateErrorPolicy.HandleAndContinue, template.ErrorPolicy);
        Assert.Equal(3, step.Order);
        Assert.Equal("Persist", step.Name);
        Assert.True(step.Optional);
        Assert.Equal(HookPoint.OnError, hook.HookPoint);
        Assert.Equal(3, hook.StepOrder);
        AssertEnumValues(StateMachineInvalidTriggerPolicy.Throw, StateMachineInvalidTriggerPolicy.Ignore, StateMachineInvalidTriggerPolicy.ReturnFalse);
        AssertEnumValues(StateMachineGuardFailurePolicy.Throw, StateMachineGuardFailurePolicy.Ignore, StateMachineGuardFailurePolicy.ReturnFalse);
        AssertEnumValues(HookPoint.BeforeAll, HookPoint.AfterAll, HookPoint.OnError);
        AssertEnumValues(TemplateErrorPolicy.Rethrow, TemplateErrorPolicy.HandleAndContinue);
    }

    private static void AssertEnumValues<TEnum>(params TEnum[] values)
        where TEnum : struct, Enum
    {
        foreach (var value in values)
        {
            Assert.True(Enum.IsDefined(value), $"{typeof(TEnum).Name}.{value} should be defined.");
        }
    }
}
