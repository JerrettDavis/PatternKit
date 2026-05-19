using Microsoft.Extensions.DependencyInjection;
using PatternKit.Behavioral.Chain;
using PatternKit.Behavioral.Memento;
using PatternKit.Behavioral.Observer;
using PatternKit.Behavioral.State;
using PatternKit.Behavioral.Strategy;
using PatternKit.Behavioral.TypeDispatcher;
using PatternKit.Creational.AbstractFactory;
using PatternKit.Creational.Factory;
using PatternKit.Creational.Prototype;
using PatternKit.Examples.EnterpriseFeatureSlices;
using PatternKit.Structural.Decorator;
using PatternKit.Structural.Flyweight;
using PatternKit.Structural.Proxy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using static PatternKit.Examples.EnterpriseFeatureSlices.EnterpriseFeatureSlicesDemo;

namespace PatternKit.Examples.Tests.EnterpriseFeatureSlices;

[Feature("Enterprise feature slices with standard .NET dependency injection")]
public sealed class EnterpriseFeatureSlicesDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("ServiceCollection resolves the feature facade and PatternKit artifacts")]
    [Fact]
    public Task ServiceCollection_Resolves_Feature_Facade_And_PatternKit_Artifacts()
        => Given("a provider built from the enterprise feature-slice registration", BuildServiceProvider)
            .When("resolving registered services", provider => new
            {
                Checkout = provider.GetService<IEnterpriseCheckout>(),
                Catalog = provider.GetService<Flyweight<string, CatalogItem>>(),
                FulfillmentFactory = provider.GetService<Factory<ProductKind, FulfillmentDraft, FulfillmentInstruction>>(),
                Prototype = provider.GetService<Prototype<string, EnterpriseCheckoutContext>>(),
                Validation = provider.GetService<ResultChain<EnterpriseCheckoutContext, ValidationOutcome>>(),
                Discounts = provider.GetService<Strategy<EnterpriseCheckoutContext, decimal>>(),
                Prices = provider.GetService<Decorator<EnterpriseCheckoutContext, decimal>>(),
                Payments = provider.GetService<Proxy<PaymentCharge, PaymentReceipt>>(),
                Stages = provider.GetService<StateMachine<CheckoutStage, CheckoutEvent>>(),
                History = provider.GetService<Memento<CheckoutSnapshot>>(),
                Events = provider.GetService<Observer<CheckoutEvent>>(),
                RegionalFactory = provider.GetService<AbstractFactory<Region>>()
            })
            .Then("the feature facade is available", services => services.Checkout is not null)
            .And("the pattern artifacts are container-owned singletons", services =>
                services.Catalog is not null
                && services.FulfillmentFactory is not null
                && services.Prototype is not null
                && services.Validation is not null
                && services.Discounts is not null
                && services.Prices is not null
                && services.Payments is not null
                && services.Stages is not null
                && services.History is not null
                && services.Events is not null
                && services.RegionalFactory is not null)
            .AssertPassed();

    [Scenario("Place accepts a real retail checkout and queues fulfillment work")]
    [Fact]
    public Task Place_Accepts_Real_Retail_Checkout_And_Queues_Fulfillment()
        => Given("a feature facade and a mixed physical digital subscription order", () =>
            {
                var provider = BuildServiceProvider();
                return new
                {
                    Provider = provider,
                    Checkout = provider.GetRequiredService<IEnterpriseCheckout>(),
                    Request = CreateRetailRequest()
                };
            })
            .When("placing the order", ctx => ctx.Checkout.Place(ctx.Request))
            .Then("the order is accepted", result =>
                result.Accepted && result.Stage == CheckoutStage.FulfillmentQueued)
            .And("fulfillment work items are typed by item kind", result =>
                result.WorkItems.Any(item => item.StartsWith("ship:KB-001", StringComparison.Ordinal))
                && result.WorkItems.Any(item => item.StartsWith("subscription:APP-365", StringComparison.Ordinal))
                && result.WorkItems.Any(item => item.StartsWith("download:EBOOK-42", StringComparison.Ordinal)))
            .And("the audit shows DI-wired observers and payment proxy activity", result =>
                result.Audit.Any(item => item.Contains("event:SO-1001:Validate", StringComparison.Ordinal))
                && result.Audit.Any(item => item.Contains("payment:request:SO-1001", StringComparison.Ordinal))
                && result.Audit.Any(item => item.Contains("gateway:Stripe:SO-1001", StringComparison.Ordinal))
                && result.SnapshotVersion >= 3)
            .AssertPassed();

    [Scenario("Estimate computes pricing without charging payment or queueing work")]
    [Fact]
    public Task Estimate_Computes_Pricing_Without_Charging_Or_Queueing()
        => Given("a feature facade and a retail order", () =>
            {
                var provider = BuildServiceProvider();
                return new
                {
                    Checkout = provider.GetRequiredService<IEnterpriseCheckout>(),
                    Request = CreateRetailRequest("SO-EST")
                };
            })
            .When("estimating the order", ctx => ctx.Checkout.Estimate(ctx.Request))
            .Then("the estimate is accepted at priced stage", result =>
                result.Accepted && result.Stage == CheckoutStage.Priced && result.Total > 0)
            .And("no payment or fulfillment side effects occurred", result =>
                result.WorkItems.Count == 0
                && result.Audit.All(item => !item.Contains("payment:request", StringComparison.Ordinal))
                && result.Audit.All(item => !item.Contains("notify:fulfillment", StringComparison.Ordinal)))
            .AssertPassed();

    [Scenario("Validation rejects incompatible electronic fulfillment for physical goods")]
    [Fact]
    public Task Validation_Rejects_Electronic_Mode_For_Physical_Goods()
        => Given("a feature facade and an electronic order containing physical goods", () =>
            {
                var provider = BuildServiceProvider();
                var request = CreateRetailRequest("SO-BAD") with { Mode = FulfillmentMode.Electronic };
                return new
                {
                    Checkout = provider.GetRequiredService<IEnterpriseCheckout>(),
                    Request = request
                };
            })
            .When("placing the order", ctx => ctx.Checkout.Place(ctx.Request))
            .Then("the order is rejected before payment", result =>
                !result.Accepted && result.Stage == CheckoutStage.Rejected)
            .And("the message identifies the fulfillment issue", result =>
                result.Message.Contains("Electronic fulfillment", StringComparison.Ordinal))
            .And("the payment proxy was not called", result =>
                result.Audit.All(item => !item.Contains("payment:request", StringComparison.Ordinal)))
            .AssertPassed();

    [Scenario("Risk proxy sends high value orders to manual review unless manager override is present")]
    [Fact]
    public async Task Risk_Proxy_Requires_Manager_Override_For_High_Value_Orders()
    {
        await Given("two high-value requests, one without override and one with override", () =>
            {
                var deniedProvider = BuildServiceProvider();
                var approvedProvider = BuildServiceProvider();
                var lines = new[] { new CheckoutLine("KB-001", ProductKind.Physical, 50) };

                return new
                {
                    Denied = deniedProvider.GetRequiredService<IEnterpriseCheckout>(),
                    Approved = approvedProvider.GetRequiredService<IEnterpriseCheckout>(),
                    DeniedRequest = new CheckoutRequest(
                        "SO-RISK-1",
                        "CUST-001",
                        CustomerTier.Standard,
                        Region.NorthAmerica,
                        FulfillmentMode.Standard,
                        lines),
                    ApprovedRequest = new CheckoutRequest(
                        "SO-RISK-2",
                        "CUST-001",
                        CustomerTier.Standard,
                        Region.NorthAmerica,
                        FulfillmentMode.Standard,
                        lines,
                        ManagerOverride: true)
                };
            })
            .When("placing both orders", ctx => new
            {
                Denied = ctx.Denied.Place(ctx.DeniedRequest),
                Approved = ctx.Approved.Place(ctx.ApprovedRequest)
            })
            .Then("the unapproved high-value order goes to manual review", result =>
                !result.Denied.Accepted
                && result.Denied.Message.Contains("manual review", StringComparison.Ordinal))
            .And("the manager override order is accepted", result =>
                result.Approved.Accepted
                && result.Approved.Stage == CheckoutStage.FulfillmentQueued)
            .And("the accepted path still goes through the regional payment gateway", result =>
                result.Approved.Audit.Any(item => item.Contains("gateway:Stripe:SO-RISK-2", StringComparison.Ordinal)))
            .AssertPassed();
    }

    [Scenario("Validation chain rejects malformed checkout requests before payment")]
    [Fact]
    public Task Validation_Rejects_Malformed_Checkout_Requests()
        => Given("a feature facade and malformed checkout requests", () =>
            {
                var provider = BuildServiceProvider();
                var checkout = provider.GetRequiredService<IEnterpriseCheckout>();

                return new
                {
                    Checkout = checkout,
                    MissingOrder = CreateRetailRequest("") with { CustomerId = "CUST-MISSING" },
                    EmptyLines = CreateRetailRequest("SO-EMPTY") with { Lines = [] },
                    BadQuantity = CreateRetailRequest("SO-QTY") with
                    {
                        Lines = [new CheckoutLine("EBOOK-42", ProductKind.Digital, 0)]
                    }
                };
            })
            .When("placing each malformed request", ctx => new
            {
                ctx.MissingOrder.CustomerId,
                MissingOrder = ctx.Checkout.Place(ctx.MissingOrder),
                EmptyLines = ctx.Checkout.Place(ctx.EmptyLines),
                BadQuantity = ctx.Checkout.Place(ctx.BadQuantity)
            })
            .Then("the order-id rule reports the missing identifier", result =>
                result.CustomerId == "CUST-MISSING"
                && !result.MissingOrder.Accepted
                && result.MissingOrder.Message.Contains("Order id", StringComparison.Ordinal))
            .And("the line-count rule reports empty orders", result =>
                !result.EmptyLines.Accepted
                && result.EmptyLines.Message.Contains("At least one checkout line", StringComparison.Ordinal))
            .And("the quantity rule reports invalid quantities", result =>
                !result.BadQuantity.Accepted
                && result.BadQuantity.Message.Contains("positive", StringComparison.Ordinal))
            .AssertPassed();

    [Scenario("Discount strategy covers enterprise, volume, and default pricing tiers")]
    [Fact]
    public Task Discount_Strategy_Covers_Enterprise_Volume_And_Default_Tiers()
        => Given("a feature facade and representative pricing requests", () =>
            {
                var provider = BuildServiceProvider();
                var checkout = provider.GetRequiredService<IEnterpriseCheckout>();

                return new
                {
                    Checkout = checkout,
                    Enterprise = CreateRetailRequest("SO-ENT") with { Tier = CustomerTier.Enterprise },
                    Volume = new CheckoutRequest(
                        "SO-VOL",
                        "CUST-STANDARD",
                        CustomerTier.Standard,
                        Region.NorthAmerica,
                        FulfillmentMode.Standard,
                        [new CheckoutLine("APP-365", ProductKind.Subscription, 3)]),
                    Default = new CheckoutRequest(
                        "SO-BASE",
                        "CUST-STANDARD",
                        CustomerTier.Standard,
                        Region.NorthAmerica,
                        FulfillmentMode.Electronic,
                        [new CheckoutLine("EBOOK-42", ProductKind.Digital, 1)])
                };
            })
            .When("estimating all pricing tiers", ctx => new
            {
                Enterprise = ctx.Checkout.Estimate(ctx.Enterprise),
                Volume = ctx.Checkout.Estimate(ctx.Volume),
                Default = ctx.Checkout.Estimate(ctx.Default)
            })
            .Then("the enterprise tier receives the largest configured discount", result =>
                result.Enterprise.Accepted
                && result.Enterprise.Total < result.Volume.Total)
            .And("the volume tier receives its fixed discount", result =>
                result.Volume.Accepted
                && result.Volume.Total > 0m)
            .And("the default tier remains a valid digital-only electronic estimate", result =>
                result.Default.Accepted
                && result.Default.Stage == CheckoutStage.Priced
                && result.Default.Total > 0m)
            .AssertPassed();

    [Scenario("Regional abstract factory routes payments through Europe and Asia Pacific providers")]
    [Fact]
    public Task Regional_Factory_Routes_Europe_And_Asia_Pacific_Payments()
        => Given("separate regional providers and digital checkout requests", () =>
            {
                var europeProvider = BuildServiceProvider();
                var asiaProvider = BuildServiceProvider();

                return new
                {
                    Europe = europeProvider.GetRequiredService<IEnterpriseCheckout>(),
                    Asia = asiaProvider.GetRequiredService<IEnterpriseCheckout>(),
                    EuropeRequest = new CheckoutRequest(
                        "SO-EU",
                        "CUST-EU",
                        CustomerTier.Standard,
                        Region.Europe,
                        FulfillmentMode.Electronic,
                        [new CheckoutLine("EBOOK-42", ProductKind.Digital, 1)]),
                    AsiaRequest = new CheckoutRequest(
                        "SO-APAC",
                        "CUST-APAC",
                        CustomerTier.Standard,
                        Region.AsiaPacific,
                        FulfillmentMode.Electronic,
                        [new CheckoutLine("EBOOK-42", ProductKind.Digital, 1)])
                };
            })
            .When("placing regional orders", ctx => new
            {
                Europe = ctx.Europe.Place(ctx.EuropeRequest),
                Asia = ctx.Asia.Place(ctx.AsiaRequest)
            })
            .Then("Europe uses the Adyen payment family", result =>
                result.Europe.Accepted
                && result.Europe.Audit.Any(item => item.Contains("gateway:Adyen:SO-EU", StringComparison.Ordinal)))
            .And("Asia Pacific uses the Alipay payment family", result =>
                result.Asia.Accepted
                && result.Asia.Audit.Any(item => item.Contains("gateway:Alipay:SO-APAC", StringComparison.Ordinal)))
            .AssertPassed();

    [Scenario("Fallback artifacts handle catalog misses, prototype defaults, and manual fulfillment")]
    [Fact]
    public Task Fallback_Artifacts_Handle_Catalog_Prototype_And_Dispatcher_Defaults()
        => Given("a provider with fallback-capable PatternKit artifacts", () =>
            {
                var provider = BuildServiceProvider();
                var audit = provider.GetRequiredService<AuditLog>();
                audit.Add("seed");

                return new
                {
                    Checkout = provider.GetRequiredService<IEnterpriseCheckout>(),
                    Prototype = provider.GetRequiredService<Prototype<string, EnterpriseCheckoutContext>>(),
                    Dispatcher = provider.GetRequiredService<TypeDispatcher<FulfillmentInstruction, string>>(),
                    Audit = audit,
                    SpecialOrder = new CheckoutRequest(
                        "SO-SPECIAL",
                        "CUST-SPECIAL",
                        CustomerTier.Standard,
                        Region.NorthAmerica,
                        FulfillmentMode.Standard,
                        [new CheckoutLine("SPECIAL-9", ProductKind.Physical, 1)])
                };
            })
            .When("using each fallback path", ctx =>
            {
                var defaultContext = ctx.Prototype.Create("quote");
                var manualWork = ctx.Dispatcher.Dispatch(new ManualInstruction("SO-MANUAL", "MAN-1", 2));
                ctx.Audit.Clear();
                var auditWasCleared = ctx.Audit.Entries.Count == 0;

                return new
                {
                    SpecialOrder = ctx.Checkout.Place(ctx.SpecialOrder),
                    DefaultContext = defaultContext,
                    ManualWork = manualWork,
                    AuditWasCleared = auditWasCleared
                };
            })
            .Then("the catalog flyweight creates a special-order product", result =>
                result.SpecialOrder.Accepted
                && result.SpecialOrder.WorkItems.Any(item => item.Contains("SPECIAL-9", StringComparison.Ordinal)))
            .And("the prototype default returns a fresh checkout context", result =>
                result.DefaultContext.Request.OrderId == ""
                && result.DefaultContext.Request.CustomerId == ""
                && result.DefaultContext.CatalogItems.Count == 0
                && result.DefaultContext.Stage == CheckoutStage.Received)
            .And("the dispatcher default renders manual fulfillment work", result =>
                result.ManualWork == "manual:MAN-1:2"
                && result.AuditWasCleared)
            .AssertPassed();

    private sealed record ManualInstruction(string ManualOrderId, string ManualSku, int ManualQuantity)
        : FulfillmentInstruction(ManualOrderId, ManualSku, ManualQuantity);
}
