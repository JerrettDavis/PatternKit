using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.ApiGateway;
using PatternKit.Examples.DependencyInjection;
using PatternKit.Examples.ObserverDemo;
using PatternKit.Examples.PointOfSale;
using PatternKit.Examples.ProductionReadiness;
using PatternKit.Examples.Strategies.Composed;
using Showcase = PatternKit.Examples.PatternShowcase.PatternShowcase;
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
            .Then("the registered examples produce expected outputs", passed => passed)
            .AssertPassed();

    private static bool UseRegisteredExamples(ServiceProvider provider)
    {
        var coercion = provider.GetRequiredService<CoercionExample>();
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
        var routing = provider.GetRequiredService<MessageRouterVisitorExample>();
        var envelope = provider.GetRequiredService<EnterpriseMessagingWorkflowSuiteExample>();
        var checkout = provider.GetRequiredService<ResilientCheckoutMailboxesExample>();

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
        editor.Editor.Insert("hello");

        return coercion.Integers.From("42") == 42
               && coercion.Booleans.From("true") == true
               && !string.IsNullOrWhiteSpace(coercion.Strings.From(12))
               && send.Success
               && auth.Log.Contains("deny: missing auth", StringComparer.Ordinal)
               && json.StatusCode == 200
               && receipt.FinalTotal > 0
               && singleton.State.Instance.Devices.PrinterReady
               && order.ok
               && proxy.RemoteProxy.Execute(42).Contains("42", StringComparison.Ordinal)
               && proxy.EmailProxy.Execute(("user@example.com", "Hello", "Body"))
               && flyweight.RenderSentence("hello").Count == 5
               && editor.Editor.State.Text == "hello"
               && received
               && viewModel.ViewModel.CanSave.Value
               && transaction.Transaction.CanCheckout.Value
               && state.Final == PatternKit.Examples.AsyncStateDemo.ConnectionStateDemo.Mode.Connected
               && template.Processor.Execute("one two") == 2
               && asyncResult == "PAYLOAD:7"
               && routing.Run().AggregatedTotal == 100m
               && envelope.Run().Attempt == 1
               && checkout.Run(CreateCheckoutRequest(), new PatternKit.Examples.Messaging.CheckoutServices()).Succeeded;
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
