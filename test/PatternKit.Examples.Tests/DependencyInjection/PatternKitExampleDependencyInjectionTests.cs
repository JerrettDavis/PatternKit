using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.AntiCorruptionDemo;
using PatternKit.Examples.ApiGateway;
using PatternKit.Examples.BulkheadDemo;
using PatternKit.Examples.CacheAsideDemo;
using PatternKit.Examples.CircuitBreakerDemo;
using PatternKit.Examples.DataMapperDemo;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.DomainEventDemo;
using PatternKit.Examples.IdentityMapDemo;
using PatternKit.Examples.MaterializedViewDemo;
using PatternKit.Examples.Messaging;
using PatternKit.Examples.ObserverDemo;
using PatternKit.Examples.PointOfSale;
using PatternKit.Examples.ProductionReadiness;
using PatternKit.Examples.QueueLoadLevelingDemo;
using PatternKit.Examples.RateLimitingDemo;
using PatternKit.Examples.RepositoryDemo;
using PatternKit.Examples.ServiceLayerDemo;
using PatternKit.Examples.Strategies.Composed;
using PatternKit.Examples.TableDataGatewayDemo;
using PatternKit.Examples.TransactionScriptDemo;
using PatternKit.Examples.UnitOfWorkDemo;
using Showcase = PatternKit.Examples.PatternShowcase.PatternShowcase;
using WidgetDemo = PatternKit.Examples.AbstractFactoryDemo.AbstractFactoryDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.DependencyInjection;

[Feature("Example-level dependency injection integrations")]
public sealed class PatternKitExampleDependencyInjectionTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Every catalog example has a fluent IServiceCollection integration")]
    [Fact]
    public Task Every_Catalog_Example_Has_A_Fluent_IServiceCollection_Integration()
        => Given("a service collection configured with all PatternKit examples", () =>
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("resolving the catalog and registered example service descriptors", provider => new
            {
                Provider = provider,
                Catalog = provider.GetRequiredService<IPatternKitExampleCatalog>(),
                Descriptors = provider.GetServices<PatternKitExampleServiceDescriptor>().ToArray()
            })
            .Then("each catalog entry has a matching IoC descriptor", result =>
                result.Catalog.Entries.All(entry =>
                    result.Descriptors.Any(descriptor =>
                        string.Equals(descriptor.ExampleName, entry.Name, StringComparison.Ordinal))))
            .And("each descriptor resolves its concrete integration service", result =>
                result.Descriptors.All(descriptor =>
                    result.Provider.GetRequiredService(descriptor.ServiceType) is not null))
            .And("each integration advertises dependency injection as an available surface", result =>
                result.Descriptors.All(descriptor =>
                    descriptor.Integration.HasFlag(ExampleIntegrationSurface.DependencyInjection)))
            .AssertPassed();

    [Scenario("IoC-registered examples can be used by importing applications")]
    [Fact]
    public Task IoC_Registered_Examples_Can_Be_Used_By_Importing_Applications()
        => Given("a provider configured with all PatternKit examples", () =>
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddPatternKitExamples();
                return services.BuildServiceProvider(validateScopes: true);
            })
            .When("using representative registered examples", UseRegisteredExamples)
            .Then("the registered examples produce expected outputs", checks =>
            {
                foreach (var check in checks)
                    ScenarioExpect.True(check.Passed, check.Name);
            })
            .AssertPassed();

    private static IReadOnlyList<(string Name, bool Passed)> UseRegisteredExamples(ServiceProvider provider)
    {
        var coercion = provider.GetRequiredService<CoercionExample>();
        var abstractFactory = provider.GetRequiredService<AbstractFactoryWidgetExample>();
        var notifications = provider.GetRequiredService<ComposedNotificationStrategyExample>();
        var auth = provider.GetRequiredService<AuthLoggingChainExample>();
        var router = provider.GetRequiredService<MinimalWebRequestRouterExample>();
        var payment = provider.GetRequiredService<PaymentProcessorDecoratorExample>();
        var singleton = provider.GetRequiredService<PosAppStateSingletonExample>();
        var showcase = provider.GetRequiredService<PatternsShowcaseExample>();
        var proxy = provider.GetRequiredService<ProxyPatternDemonstrationsExample>();
        var flyweight = provider.GetRequiredService<FlyweightGlyphCacheExample>();
        var editor = provider.GetRequiredService<TextEditorMementoExample>();
        var eventHub = provider.GetRequiredService<ObserverEventHubExample>();
        var viewModel = provider.GetRequiredService<ReactiveViewModelExample>();
        var transaction = provider.GetRequiredService<ReactiveTransactionExample>();
        var asyncState = provider.GetRequiredService<AsyncConnectionStateMachineExample>();
        var template = provider.GetRequiredService<TemplateMethodSubclassingExample>();
        var asyncTemplate = provider.GetRequiredService<TemplateMethodAsyncExample>();
        var antiCorruption = provider.GetRequiredService<LegacyOrderAntiCorruptionExample>();
        var routing = provider.GetRequiredService<MessageRouterVisitorExample>();
        var generatedRecipients = provider.GetRequiredService<GeneratedRecipientListExample>();
        var competingConsumers = provider.GetRequiredService<FulfillmentCompetingConsumersExampleService>();
        var pipesAndFilters = provider.GetRequiredService<FulfillmentPipesAndFiltersExampleService>();
        var messageFilter = provider.GetRequiredService<OrderMessageFilterExampleService>();
        var generatedTranslator = provider.GetRequiredService<GeneratedMessageTranslatorExample>();
        var generatedClaimCheck = provider.GetRequiredService<GeneratedClaimCheckExample>();
        var generatedDeadLetters = provider.GetRequiredService<GeneratedDeadLetterChannelExample>();
        var envelope = provider.GetRequiredService<EnterpriseMessagingWorkflowSuiteExample>();
        var cqrs = provider.GetRequiredService<CqrsDispatcherExample>();
        var checkout = provider.GetRequiredService<ResilientCheckoutMailboxesExample>();
        var interpreter = provider.GetRequiredService<GeneratedInterpreterRulesExample>();
        var specifications = provider.GetRequiredService<LoanApprovalSpecificationsExample>();
        var orderRepository = provider.GetRequiredService<OrderRepositoryPatternExample>();
        var unitOfWork = provider.GetRequiredService<CheckoutUnitOfWorkPatternExample>();
        var dataMapper = provider.GetRequiredService<OrderDataMapperPatternExample>();
        var identityMap = provider.GetRequiredService<OrderIdentityMapPatternExample>();
        var transactionScript = provider.GetRequiredService<OrderTransactionScriptPatternExample>();
        var serviceLayer = provider.GetRequiredService<CustomerServiceLayerPatternExample>();
        var domainEvents = provider.GetRequiredService<OrderDomainEventPatternExample>();
        var tableGateway = provider.GetRequiredService<OrderTableDataGatewayPatternExample>();
        var eventSourcing = provider.GetRequiredService<OrderEventSourcingPatternExample>();
        var featureToggles = provider.GetRequiredService<CheckoutFeatureTogglePatternExample>();
        var auditLog = provider.GetRequiredService<OrderAuditLogPatternExample>();
        var materializedView = provider.GetRequiredService<OrderMaterializedViewPatternExample>();
        var inventoryRetry = provider.GetRequiredService<InventoryRetryExample>();
        var fulfillmentBreaker = provider.GetRequiredService<FulfillmentCircuitBreakerExample>();
        var shippingBulkhead = provider.GetRequiredService<ShippingBulkheadExample>();
        var queueLoadLeveling = provider.GetRequiredService<FulfillmentQueueLoadLevelingExample>();
        var productCacheAside = provider.GetRequiredService<ProductCatalogCacheAsideExample>();
        var productRateLimit = provider.GetRequiredService<ProductSearchRateLimitingExample>();

        auth.Chain.Execute(new PatternKit.Examples.Chain.HttpRequest("GET", "/admin/metrics", new Dictionary<string, string>()));

        var json = router.Router.Handle(new Request("GET", "/orders", new Dictionary<string, string>()));
        var receipt = payment.Processor.Execute(CreateOrder());
        var order = showcase.Facade.Place(new Showcase.OrderDto(
            "ORD-DI",
            "VIP-100",
            "cash",
            [new Showcase.OrderItemDto("SKU-1", "Widget", 20m, 1)]));

        var received = false;
        using var subscription = eventHub.Hub.On((in UserEvent _) => received = true);
        eventHub.Hub.Publish(new UserEvent(1, "login"));

        viewModel.ViewModel.FirstName.Value = "Ada";
        viewModel.ViewModel.LastName.Value = "Lovelace";
        transaction.Transaction.AddItem(new LineItem("SKU-1", 1, 10m));
        transaction.Transaction.SetPayment(PaymentKind.CreditCard);

        var send = notifications.Strategy.ExecuteAsync(new SendContext(Guid.NewGuid(), "hello", false), CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var state = asyncState.RunAsync(["connect", "ok"]).GetAwaiter().GetResult();
        var asyncResult = asyncTemplate.Pipeline.ExecuteAsync(7, CancellationToken.None).GetAwaiter().GetResult();
        var generatedRecipientList = generatedRecipients.Runner.RunGenerated();
        var cqrsFluent = cqrs.RunFluentAsync(CancellationToken.None).GetAwaiter().GetResult();
        var cqrsGenerated = cqrs.RunSourceGeneratedAsync(provider, CancellationToken.None).GetAwaiter().GetResult();
        editor.Editor.Insert("hello");

        return
        [
            ("integer coercer converts text", coercion.Integers.From("42") == 42),
            ("boolean coercer converts text", coercion.Booleans.From("true") == true),
            ("string coercer accepts strings", coercion.Strings.From("patternkit") == "patternkit"),
            ("abstract factory creates DI-backed widget families", abstractFactory.Factory.GetFamily(WidgetDemo.Platform.Windows).Create<WidgetDemo.IButton>().Render().Contains("Windows", StringComparison.Ordinal)),
            ("notification strategy sends", send.Success),
            ("auth chain logs denied admin requests", auth.Log.Contains("deny: missing auth", StringComparer.Ordinal)),
            ("minimal router returns a successful response", json.StatusCode == 200),
            ("decorated payment processor totals the order", receipt.FinalTotal > 0),
            ("singleton POS app state is initialized", singleton.State.Instance.Devices.PrinterReady),
            ("pattern showcase facade places an order", order.ok),
            ("remote proxy returns remote data", proxy.RemoteProxy.Execute(42).Contains("42", StringComparison.Ordinal)),
            ("email proxy accepts example addresses", proxy.EmailProxy.Execute(("user@example.com", "Hello", "Body"))),
            ("flyweight renderer returns one glyph per character", flyweight.RenderSentence("hello").Count == 5),
            ("memento editor tracks inserted text", editor.Editor.State.Text == "hello"),
            ("observer event hub publishes events", received),
            ("reactive view model enables save", viewModel.ViewModel.CanSave.Value),
            ("reactive transaction enables checkout", transaction.Transaction.CanCheckout.Value),
            ("async state machine connects", state.Final == PatternKit.Examples.AsyncStateDemo.ConnectionStateDemo.Mode.Connected),
            ("template method counts words", template.Processor.Execute("one two") == 2),
            ("async template method formats payloads", asyncResult == "PAYLOAD:7"),
            ("generated anti-corruption layer imports legacy orders", antiCorruption.Service.Import(new LegacyOrderDto("ORD-100", 125m, "USD", "cust-42")).Accepted),
            ("message router visitor aggregates totals", routing.Run().AggregatedTotal == 100m),
            ("generated message translator normalizes partner events", generatedTranslator.Service.Import(PartnerEventTranslatorExample.CreatePartnerMessage("partner-a", "EXT-100", 125m)).Accepted),
            ("generated claim check restores large document payloads", generatedClaimCheck.Workflow.Process(LargeDocumentClaimCheckExample.CreateDocumentMessage("doc-100")).Restored),
            ("generated dead-letter channel prepares replay handoff", generatedDeadLetters.Workflow.Capture(FulfillmentDeadLetterChannelExample.CreateCommand("order-100"), "adapter failed").ReadyForReplay),
            ("generated recipient list delivers billing and audit recipients", generatedRecipientList.DeliveredRecipients.Count == 2),
            ("generated competing consumers dispatch fulfillment work", competingConsumers.Service.DispatchAsync(new FulfillmentConsumerWork("ORDER-CC", "central")).GetAwaiter().GetResult().Accepted),
            ("generated pipes and filters publish fulfillment work", pipesAndFilters.Service.ProcessAsync("ORDER-PF").GetAwaiter().GetResult().Value.Published),
            ("generated message filter screens trusted orders", messageFilter.Service.Screen(new("ORDER-MF", "trusted", 250m, true)).Accepted),
            ("message envelope example tracks first attempt", envelope.Run().Attempt == 1),
            ("CQRS fluent path matches command writes to query reads", cqrsFluent.QueryMatchedCommand),
            ("CQRS generated path matches command writes to query reads", cqrsGenerated.QueryMatchedCommand),
            ("resilient checkout succeeds", checkout.Run(CreateCheckoutRequest(), new PatternKit.Examples.Messaging.CheckoutServices()).Succeeded),
            ("generated interpreter computes tier discounts", interpreter.Pricing.Interpret(PatternKit.Examples.InterpreterDemo.InterpreterDemo.TierDiscountRule, new PatternKit.Examples.InterpreterDemo.InterpreterDemo.PricingContext { CartTotal = 100m, CustomerTier = "Gold" }) == 10m),
            ("generated interpreter evaluates VIP eligibility", interpreter.Eligibility.Interpret(PatternKit.Examples.InterpreterDemo.InterpreterDemo.VipEligibilityRule, new PatternKit.Examples.InterpreterDemo.InterpreterDemo.PricingContext { CartTotal = 150m, CustomerTier = "Gold" })),
            ("generated specification registry approves prime loans", specifications.Service.Evaluate(PatternKit.Examples.SpecificationDemo.LoanApprovalSpecificationDemo.CreatePrimeApplication()).Approved),
            ("repository example rejects duplicate order keys", orderRepository.Workflow.RunAsync().AsTask().GetAwaiter().GetResult().DuplicateRejected),
            ("unit of work example commits checkout steps", unitOfWork.Workflow.RunAsync().AsTask().GetAwaiter().GetResult().Committed),
            ("data mapper example rehydrates stored orders", dataMapper.Workflow.RunAsync().AsTask().GetAwaiter().GetResult().LoadedCustomerId == "customer-1"),
            ("identity map example reuses loaded orders", identityMap.Runner.RunFluent().ReusedInstance),
            ("transaction script example submits orders", transactionScript.Runner.RunFluentAsync().AsTask().GetAwaiter().GetResult().Submitted),
            ("service layer example registers customers", serviceLayer.Runner.RunFluentAsync().AsTask().GetAwaiter().GetResult().Registered),
            ("domain event example dispatches order events", domainEvents.Runner.RunFluentAsync().AsTask().GetAwaiter().GetResult().Dispatched),
            ("table data gateway example queries order rows", tableGateway.Runner.RunFluentAsync().AsTask().GetAwaiter().GetResult().ClosedOrderCount == 1),
            ("event sourcing example replays paid order streams", eventSourcing.Runner.RunFluentAsync().AsTask().GetAwaiter().GetResult().Paid),
            ("feature toggle example evaluates checkout features", featureToggles.Runner.RunFluent().NewCheckoutEnabled),
            ("audit log example records order actions", auditLog.Runner.RunFluentAsync().AsTask().GetAwaiter().GetResult().EntryCount == 2),
            ("materialized view example builds shipped order read models", materializedView.Runner.RunFluentAsync().AsTask().GetAwaiter().GetResult().Status == "Shipped"),
            ("generated retry policy recovers inventory lookups", inventoryRetry.Service.CheckAsync("SKU-42").GetAwaiter().GetResult().Available),
            ("generated circuit breaker isolates fulfillment outages", CircuitBreakerOpens(fulfillmentBreaker.Service)),
            ("generated bulkhead reserves shipping allocations", shippingBulkhead.Service.ReserveAsync("ORDER-100").GetAwaiter().GetResult().Succeeded),
            ("generated queue load leveling accepts fulfillment work", queueLoadLeveling.Service.EnqueueAsync(new FulfillmentWorkItem("ORDER-QL", "central")).GetAwaiter().GetResult().Accepted),
            ("generated cache-aside reuses product catalog reads", CacheAsideHits(productCacheAside.Service)),
            ("generated rate limit rejects product search overflow", RateLimitRejects(productRateLimit.Service))
        ];
    }

    private static bool RateLimitRejects(ProductSearchRateLimitService service)
    {
        _ = service.SearchAsync("tenant-a", "boots").GetAwaiter().GetResult();
        _ = service.SearchAsync("tenant-a", "jackets").GetAwaiter().GetResult();
        return service.SearchAsync("tenant-a", "hats").GetAwaiter().GetResult().Rejected;
    }

    private static bool CacheAsideHits(ProductCatalogCacheAsideService service)
    {
        _ = service.FindAsync("SKU-42").GetAwaiter().GetResult();
        return service.FindAsync("SKU-42").GetAwaiter().GetResult().CacheHit;
    }

    private static bool CircuitBreakerOpens(FulfillmentCircuitBreakerService service)
    {
        _ = service.SubmitAsync("ORDER-42").GetAwaiter().GetResult();
        var opened = service.SubmitAsync("ORDER-42").GetAwaiter().GetResult();
        var rejected = service.SubmitAsync("ORDER-42").GetAwaiter().GetResult();
        return opened.State == PatternKit.Cloud.CircuitBreaker.CircuitBreakerState.Open && rejected.Rejected;
    }

    private static PurchaseOrder CreateOrder()
        => new()
        {
            OrderId = "DI-ORDER",
            Customer = new CustomerInfo { CustomerId = "CUST-1", LoyaltyTier = "Gold" },
            Store = new StoreLocation
            {
                StoreId = "STORE-1",
                State = "CA",
                Country = "USA",
                StateTaxRate = 0.0725m,
                LocalTaxRate = 0.0125m
            },
            Items =
            [
                new OrderLineItem
                {
                    Sku = "SKU-1",
                    ProductName = "Widget",
                    UnitPrice = 25m,
                    Quantity = 1,
                    Category = "General"
                }
            ]
        };

    private static PatternKit.Examples.Messaging.CheckoutRequest CreateCheckoutRequest()
        => new(
            "checkout-di",
            "customer-1",
            [new PatternKit.Examples.Messaging.CheckoutLine("SKU-1", 1, 25m)]);
}
