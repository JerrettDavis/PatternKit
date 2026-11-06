using Microsoft.Extensions.DependencyInjection;
using PatternKit.Behavioral.Chain;
using PatternKit.Behavioral.Iterator;
using PatternKit.Behavioral.Memento;
using PatternKit.Behavioral.Observer;
using PatternKit.Behavioral.State;
using PatternKit.Behavioral.Strategy;
using PatternKit.Behavioral.TypeDispatcher;
using PatternKit.Creational.AbstractFactory;
using PatternKit.Creational.Factory;
using PatternKit.Creational.Prototype;
using PatternKit.Structural.Decorator;
using PatternKit.Structural.Facade;
using PatternKit.Structural.Flyweight;
using PatternKit.Structural.Proxy;

namespace PatternKit.Examples.EnterpriseFeatureSlices;

/// <summary>
/// Enterprise feature-slice demo that wires PatternKit artifacts through the standard
/// Microsoft.Extensions.DependencyInjection container.
/// </summary>
public static class EnterpriseFeatureSlicesDemo
{
    public enum ProductKind { Physical, Digital, Subscription }
    public enum CustomerTier { Standard, Gold, Enterprise }
    public enum Region { NorthAmerica, Europe, AsiaPacific }
    public enum FulfillmentMode { Standard, Express, Electronic }
    public enum CheckoutStage { Received, Validating, Priced, Paid, FulfillmentQueued, Rejected }
    public enum CheckoutEventKind { Validate, Price, Pay, QueueFulfillment, Reject }

    public sealed record CatalogItem(string Sku, ProductKind Kind, string Name, decimal ListPrice, double WeightKg);
    public sealed record CheckoutLine(string Sku, ProductKind Kind, int Quantity);
    public sealed record CheckoutRequest(
        string OrderId,
        string CustomerId,
        CustomerTier Tier,
        Region Region,
        FulfillmentMode Mode,
        IReadOnlyList<CheckoutLine> Lines,
        bool ManagerOverride = false);

    public sealed record FulfillmentDraft(string OrderId, CheckoutLine Line, CatalogItem CatalogItem);

    public abstract record FulfillmentInstruction(string OrderId, string Sku, int Quantity);
    public sealed record ShipPhysical(string OrderId, string Sku, int Quantity, double WeightKg)
        : FulfillmentInstruction(OrderId, Sku, Quantity);
    public sealed record SendDownload(string OrderId, string Sku, int Quantity)
        : FulfillmentInstruction(OrderId, Sku, Quantity);
    public sealed record ActivateSubscription(string OrderId, string Sku, int Quantity, int Months)
        : FulfillmentInstruction(OrderId, Sku, Quantity);

    public sealed record CheckoutEvent(string OrderId, CheckoutEventKind Kind, string Detail);
    public sealed record CheckoutSnapshot(string OrderId, CheckoutStage Stage, decimal Total, string Note);
    public sealed record ValidationOutcome(bool Accepted, string Message);
    public sealed record PaymentCharge(string OrderId, Region Region, decimal Amount, bool ManagerOverride);
    public sealed record PaymentReceipt(bool Approved, string Provider, string AuthorizationCode);
    public sealed record CheckoutResult(
        bool Accepted,
        string Message,
        CheckoutStage Stage,
        decimal Total,
        IReadOnlyList<string> WorkItems,
        IReadOnlyList<string> Audit,
        int SnapshotVersion);

    public interface IEnterpriseCheckout
    {
        CheckoutResult Place(CheckoutRequest request);
        CheckoutResult Estimate(CheckoutRequest request);
    }

    public interface IRegionalTaxPolicy
    {
        decimal Apply(decimal taxableAmount);
    }

    public interface IRegionalPaymentGateway
    {
        string Name { get; }
        PaymentReceipt Charge(PaymentCharge charge);
    }

    public interface IRegionalRiskPolicy
    {
        bool Allows(PaymentCharge charge);
    }

    public sealed class AuditLog
    {
        private readonly List<string> _entries = [];

        public IReadOnlyList<string> Entries => _entries.ToArray();

        public void Add(string entry) => _entries.Add(entry);

        public void Clear() => _entries.Clear();
    }

    public sealed class EnterpriseCheckoutContext
    {
        public required CheckoutRequest Request { get; set; }
        public required IReadOnlyList<CatalogItem> CatalogItems { get; set; }
        public required IReadOnlyList<FulfillmentInstruction> Instructions { get; set; }
        public decimal Subtotal { get; set; }
        public decimal Shipping { get; set; }
        public decimal Discount { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }
        public CheckoutStage Stage { get; set; } = CheckoutStage.Received;
        public List<string> WorkItems { get; } = [];
        public List<string> Audit { get; } = [];
    }

    public static IServiceCollection AddEnterpriseFeatureSlices(this IServiceCollection services)
    {
        services.AddSingleton<AuditLog>();
        services.AddSingleton(CreateCatalog());
        services.AddSingleton(CreateFulfillmentFactory());
        services.AddSingleton(CreateCheckoutPrototype());
        services.AddSingleton(CreateDiscountStrategy());
        services.AddSingleton(CreateValidationChain());
        services.AddSingleton(CreateFulfillmentRenderer());
        services.AddSingleton(CreateCheckoutStateMachine());
        services.AddSingleton(CreateCheckoutHistory());
        services.AddSingleton<Observer<CheckoutEvent>>(sp => CreateCheckoutObserver(sp.GetRequiredService<AuditLog>()));
        services.AddSingleton<AbstractFactory<Region>>(sp => CreateRegionalFactory(sp.GetRequiredService<AuditLog>()));
        services.AddSingleton<Decorator<EnterpriseCheckoutContext, decimal>>(sp =>
            CreatePriceCalculator(sp.GetRequiredService<AbstractFactory<Region>>()));
        services.AddSingleton<Proxy<PaymentCharge, PaymentReceipt>>(sp =>
            CreatePaymentProxy(sp.GetRequiredService<AbstractFactory<Region>>(), sp.GetRequiredService<AuditLog>()));
        services.AddSingleton<IEnterpriseCheckout>(sp =>
        {
            var service = new EnterpriseCheckoutService(
                sp.GetRequiredService<Flyweight<string, CatalogItem>>(),
                sp.GetRequiredService<Factory<ProductKind, FulfillmentDraft, FulfillmentInstruction>>(),
                sp.GetRequiredService<Prototype<string, EnterpriseCheckoutContext>>(),
                sp.GetRequiredService<ResultChain<EnterpriseCheckoutContext, ValidationOutcome>>(),
                sp.GetRequiredService<Strategy<EnterpriseCheckoutContext, decimal>>(),
                sp.GetRequiredService<Decorator<EnterpriseCheckoutContext, decimal>>(),
                sp.GetRequiredService<Proxy<PaymentCharge, PaymentReceipt>>(),
                sp.GetRequiredService<TypeDispatcher<FulfillmentInstruction, string>>(),
                sp.GetRequiredService<StateMachine<CheckoutStage, CheckoutEvent>>(),
                sp.GetRequiredService<Memento<CheckoutSnapshot>>(),
                sp.GetRequiredService<Observer<CheckoutEvent>>(),
                sp.GetRequiredService<AuditLog>());

            return TypedFacade<IEnterpriseCheckout>.Create()
                .Map<CheckoutRequest, CheckoutResult>(x => x.Place, request => service.Place(request))
                .Map<CheckoutRequest, CheckoutResult>(x => x.Estimate, request => service.Estimate(request))
                .Build();
        });

        return services;
    }

    public static ServiceProvider BuildServiceProvider()
        => new ServiceCollection()
            .AddEnterpriseFeatureSlices()
            .BuildServiceProvider(validateScopes: true);

    public static CheckoutRequest CreateRetailRequest(string orderId = "SO-1001")
        => new(
            orderId,
            "CUST-GOLD",
            CustomerTier.Gold,
            Region.NorthAmerica,
            FulfillmentMode.Express,
            [
                new CheckoutLine("KB-001", ProductKind.Physical, 1),
                new CheckoutLine("APP-365", ProductKind.Subscription, 1),
                new CheckoutLine("EBOOK-42", ProductKind.Digital, 1)
            ]);

    private static Flyweight<string, CatalogItem> CreateCatalog()
        => Flyweight<string, CatalogItem>.Create()
            .WithComparer(StringComparer.OrdinalIgnoreCase)
            .Preload("KB-001", new CatalogItem("KB-001", ProductKind.Physical, "Mechanical Keyboard", 129.99m, 1.2))
            .Preload("APP-365", new CatalogItem("APP-365", ProductKind.Subscription, "Productivity Suite", 199.00m, 0))
            .Preload("EBOOK-42", new CatalogItem("EBOOK-42", ProductKind.Digital, "Architecture Playbook", 39.00m, 0))
            .WithFactory(sku => new CatalogItem(sku, ProductKind.Physical, $"Special order {sku}", 59.00m, 0.5))
            .Build();

    private static Factory<ProductKind, FulfillmentDraft, FulfillmentInstruction> CreateFulfillmentFactory()
        => Factory<ProductKind, FulfillmentDraft, FulfillmentInstruction>.Create()
            .Map(ProductKind.Physical, static (in FulfillmentDraft draft) =>
                new ShipPhysical(draft.OrderId, draft.Line.Sku, draft.Line.Quantity, draft.CatalogItem.WeightKg))
            .Map(ProductKind.Digital, static (in FulfillmentDraft draft) =>
                new SendDownload(draft.OrderId, draft.Line.Sku, draft.Line.Quantity))
            .Map(ProductKind.Subscription, static (in FulfillmentDraft draft) =>
                new ActivateSubscription(draft.OrderId, draft.Line.Sku, draft.Line.Quantity, 12))
            .Build();

    private static Prototype<string, EnterpriseCheckoutContext> CreateCheckoutPrototype()
    {
        var seed = new EnterpriseCheckoutContext
        {
            Request = new CheckoutRequest("", "", CustomerTier.Standard, Region.NorthAmerica, FulfillmentMode.Standard, []),
            CatalogItems = [],
            Instructions = []
        };

        return Prototype<string, EnterpriseCheckoutContext>.Create()
            .Map("checkout", seed, static (in EnterpriseCheckoutContext context) => new EnterpriseCheckoutContext
            {
                Request = context.Request,
                CatalogItems = context.CatalogItems,
                Instructions = context.Instructions,
                Subtotal = context.Subtotal,
                Shipping = context.Shipping,
                Discount = context.Discount,
                Tax = context.Tax,
                Total = context.Total,
                Stage = context.Stage
            })
            .Default(seed, static (in EnterpriseCheckoutContext context) => new EnterpriseCheckoutContext
            {
                Request = context.Request,
                CatalogItems = context.CatalogItems,
                Instructions = context.Instructions,
                Subtotal = context.Subtotal,
                Shipping = context.Shipping,
                Discount = context.Discount,
                Tax = context.Tax,
                Total = context.Total,
                Stage = context.Stage
            })
            .Build();
    }

    private static Strategy<EnterpriseCheckoutContext, decimal> CreateDiscountStrategy()
        => Strategy<EnterpriseCheckoutContext, decimal>.Create()
            .When(static (in EnterpriseCheckoutContext ctx) => ctx.Request.Tier == CustomerTier.Enterprise)
                .Then(static (in EnterpriseCheckoutContext ctx) => Math.Round(ctx.Subtotal * 0.12m, 2))
            .When(static (in EnterpriseCheckoutContext ctx) => ctx.Request.Tier == CustomerTier.Gold)
                .Then(static (in EnterpriseCheckoutContext ctx) => Math.Round(ctx.Subtotal * 0.08m, 2))
            .When(static (in EnterpriseCheckoutContext ctx) => ctx.Subtotal >= 500m)
                .Then(static (in EnterpriseCheckoutContext _) => 25m)
            .Default(static (in EnterpriseCheckoutContext _) => 0m)
            .Build();

    private static ResultChain<EnterpriseCheckoutContext, ValidationOutcome> CreateValidationChain()
        => ResultChain<EnterpriseCheckoutContext, ValidationOutcome>.Create()
            .When(static (in EnterpriseCheckoutContext ctx) => string.IsNullOrWhiteSpace(ctx.Request.OrderId))
                .Then(static _ => new ValidationOutcome(false, "Order id is required"))
            .When(static (in EnterpriseCheckoutContext ctx) => ctx.Request.Lines.Count == 0)
                .Then(static _ => new ValidationOutcome(false, "At least one checkout line is required"))
            .When(static (in EnterpriseCheckoutContext ctx) => ctx.Request.Lines.Any(line => line.Quantity <= 0))
                .Then(static _ => new ValidationOutcome(false, "Line quantities must be positive"))
            .When(static (in EnterpriseCheckoutContext ctx) =>
                ctx.Request.Mode == FulfillmentMode.Electronic
                && ctx.Instructions.OfType<ShipPhysical>().Any())
                .Then(static _ => new ValidationOutcome(false, "Electronic fulfillment cannot contain physical goods"))
            .Finally(static (in EnterpriseCheckoutContext _, out ValidationOutcome? result, ResultChain<EnterpriseCheckoutContext, ValidationOutcome>.Next _) =>
            {
                result = new ValidationOutcome(true, "Accepted");
                return true;
            })
            .Build();

    private static AbstractFactory<Region> CreateRegionalFactory(AuditLog audit)
        => AbstractFactory<Region>.Create()
            .Family(Region.NorthAmerica)
                .Product<IRegionalTaxPolicy>(() => new FlatTaxPolicy(0.0825m))
                .Product<IRegionalPaymentGateway>(() => new RegionalPaymentGateway("Stripe", audit))
                .Product<IRegionalRiskPolicy>(() => new ThresholdRiskPolicy(5000m))
            .Family(Region.Europe)
                .Product<IRegionalTaxPolicy>(() => new FlatTaxPolicy(0.20m))
                .Product<IRegionalPaymentGateway>(() => new RegionalPaymentGateway("Adyen", audit))
                .Product<IRegionalRiskPolicy>(() => new ThresholdRiskPolicy(4000m))
            .Family(Region.AsiaPacific)
                .Product<IRegionalTaxPolicy>(() => new FlatTaxPolicy(0.10m))
                .Product<IRegionalPaymentGateway>(() => new RegionalPaymentGateway("Alipay", audit))
                .Product<IRegionalRiskPolicy>(() => new ThresholdRiskPolicy(4500m))
            .Build();

    private static Decorator<EnterpriseCheckoutContext, decimal> CreatePriceCalculator(AbstractFactory<Region> regionalFactory)
        => Decorator<EnterpriseCheckoutContext, decimal>.Create(static ctx => ctx.Subtotal)
            .After(static (ctx, price) => price + ctx.Shipping)
            .After((ctx, price) =>
            {
                var policy = regionalFactory.GetFamily(ctx.Request.Region).Create<IRegionalTaxPolicy>();
                ctx.Tax = policy.Apply(price) - price;
                return policy.Apply(price);
            })
            .After(static (ctx, price) => price - ctx.Discount)
            .Build();

    private static Proxy<PaymentCharge, PaymentReceipt> CreatePaymentProxy(
        AbstractFactory<Region> regionalFactory,
        AuditLog audit)
        => Proxy<PaymentCharge, PaymentReceipt>.Create(charge =>
            {
                var family = regionalFactory.GetFamily(charge.Region);
                var risk = family.Create<IRegionalRiskPolicy>();
                if (!risk.Allows(charge) && !charge.ManagerOverride)
                    return new PaymentReceipt(false, "risk", "manual-review");

                return family.Create<IRegionalPaymentGateway>().Charge(charge);
            })
            .Intercept((charge, next) =>
            {
                audit.Add($"payment:request:{charge.OrderId}:{charge.Amount:F2}");
                var receipt = next(charge);
                audit.Add($"payment:{receipt.Provider}:{receipt.AuthorizationCode}");
                return receipt;
            })
            .Build();

    private static TypeDispatcher<FulfillmentInstruction, string> CreateFulfillmentRenderer()
        => TypeDispatcher<FulfillmentInstruction, string>.Create()
            .On<ShipPhysical>(static item => $"ship:{item.Sku}:{item.Quantity}:{item.WeightKg:F1}kg")
            .On<SendDownload>(static item => $"download:{item.Sku}:{item.Quantity}")
            .On<ActivateSubscription>(static item => $"subscription:{item.Sku}:{item.Quantity}:{item.Months}mo")
            .Default(static item => $"manual:{item.Sku}:{item.Quantity}")
            .Build();

    private static StateMachine<CheckoutStage, CheckoutEvent> CreateCheckoutStateMachine()
        => StateMachine<CheckoutStage, CheckoutEvent>.Create()
            .InState(CheckoutStage.Received, state => state
                .When(static (in CheckoutEvent e) => e.Kind == CheckoutEventKind.Validate).Permit(CheckoutStage.Validating).End()
                .When(static (in CheckoutEvent e) => e.Kind == CheckoutEventKind.Reject).Permit(CheckoutStage.Rejected).End())
            .InState(CheckoutStage.Validating, state => state
                .When(static (in CheckoutEvent e) => e.Kind == CheckoutEventKind.Price).Permit(CheckoutStage.Priced).End()
                .When(static (in CheckoutEvent e) => e.Kind == CheckoutEventKind.Reject).Permit(CheckoutStage.Rejected).End())
            .InState(CheckoutStage.Priced, state => state
                .When(static (in CheckoutEvent e) => e.Kind == CheckoutEventKind.Pay).Permit(CheckoutStage.Paid).End()
                .When(static (in CheckoutEvent e) => e.Kind == CheckoutEventKind.Reject).Permit(CheckoutStage.Rejected).End())
            .InState(CheckoutStage.Paid, state => state
                .When(static (in CheckoutEvent e) => e.Kind == CheckoutEventKind.QueueFulfillment).Permit(CheckoutStage.FulfillmentQueued).End()
                .When(static (in CheckoutEvent e) => e.Kind == CheckoutEventKind.Reject).Permit(CheckoutStage.Rejected).End())
            .InState(CheckoutStage.FulfillmentQueued, state => state
                .Otherwise().Stay().AsDefault())
            .InState(CheckoutStage.Rejected, state => state
                .Otherwise().Stay().AsDefault())
            .Build();

    private static Memento<CheckoutSnapshot> CreateCheckoutHistory()
        => Memento<CheckoutSnapshot>.Create()
            .Capacity(32)
            .Build();

    private static Observer<CheckoutEvent> CreateCheckoutObserver(AuditLog audit)
    {
        var observer = Observer<CheckoutEvent>.Create().Build();
        observer.Subscribe((in CheckoutEvent e) => audit.Add($"event:{e.OrderId}:{e.Kind}:{e.Detail}"));
        observer.Subscribe(
            (in CheckoutEvent e) => e.Kind == CheckoutEventKind.QueueFulfillment,
            (in CheckoutEvent e) => audit.Add($"notify:fulfillment:{e.OrderId}"));
        return observer;
    }

    private sealed class EnterpriseCheckoutService(
        Flyweight<string, CatalogItem> catalog,
        Factory<ProductKind, FulfillmentDraft, FulfillmentInstruction> fulfillmentFactory,
        Prototype<string, EnterpriseCheckoutContext> contextPrototype,
        ResultChain<EnterpriseCheckoutContext, ValidationOutcome> validation,
        Strategy<EnterpriseCheckoutContext, decimal> discounts,
        Decorator<EnterpriseCheckoutContext, decimal> prices,
        Proxy<PaymentCharge, PaymentReceipt> payments,
        TypeDispatcher<FulfillmentInstruction, string> renderer,
        StateMachine<CheckoutStage, CheckoutEvent> stages,
        Memento<CheckoutSnapshot> history,
        Observer<CheckoutEvent> events,
        AuditLog audit)
    {
        public CheckoutResult Estimate(CheckoutRequest request)
            => Execute(request, chargePayment: false);

        public CheckoutResult Place(CheckoutRequest request)
            => Execute(request, chargePayment: true);

        private CheckoutResult Execute(CheckoutRequest request, bool chargePayment)
        {
            var ctx = BuildContext(request);
            Save(ctx, "received");

            Move(ctx, CheckoutEventKind.Validate, "start validation");
            validation.Execute(ctx, out var outcome);
            if (outcome is not { Accepted: true })
                return Reject(ctx, outcome?.Message ?? "Rejected");

            ctx.Subtotal = ctx.CatalogItems.Zip(request.Lines, static (item, line) => item.ListPrice * line.Quantity).Sum();
            ctx.Shipping = CalculateShipping(ctx);
            ctx.Discount = discounts.Execute(ctx);

            Move(ctx, CheckoutEventKind.Price, "priced");
            ctx.Total = prices.Execute(ctx);
            Save(ctx, "priced");

            if (!chargePayment)
                return Complete(ctx, "Estimated", accepted: true);

            Move(ctx, CheckoutEventKind.Pay, "payment requested");
            var receipt = payments.Execute(new PaymentCharge(request.OrderId, request.Region, ctx.Total, request.ManagerOverride));
            if (!receipt.Approved)
                return Reject(ctx, "Payment requires manual review");

            Move(ctx, CheckoutEventKind.QueueFulfillment, "fulfillment queued");
            var instructions = Flow<FulfillmentInstruction>.From(ctx.Instructions)
                .Filter(static item => item.Quantity > 0)
                .Map<string>(item => renderer.Dispatch(item))
                .ToList();
            ctx.WorkItems.AddRange(instructions);
            Save(ctx, "queued");

            return Complete(ctx, "Order accepted", accepted: true);
        }

        private EnterpriseCheckoutContext BuildContext(CheckoutRequest request)
        {
            var catalogItems = request.Lines
                .Select(line => catalog.Get(line.Sku))
                .ToList();

            var instructions = request.Lines.Zip(catalogItems, (line, item) =>
                fulfillmentFactory.Create(line.Kind, new FulfillmentDraft(request.OrderId, line, item)))
                .ToList();

            return contextPrototype.Create("checkout", ctx =>
            {
                ctx.Request = request;
                ctx.CatalogItems = catalogItems;
                ctx.Instructions = instructions;
                ctx.Stage = CheckoutStage.Received;
                ctx.Subtotal = 0m;
                ctx.Shipping = 0m;
                ctx.Discount = 0m;
                ctx.Tax = 0m;
                ctx.Total = 0m;
            });
        }

        private static decimal CalculateShipping(EnterpriseCheckoutContext ctx)
        {
            if (ctx.Request.Mode == FulfillmentMode.Electronic)
                return 0m;

            var weight = ctx.Instructions.OfType<ShipPhysical>().Sum(item => item.WeightKg * item.Quantity);
            var baseRate = ctx.Request.Mode == FulfillmentMode.Express ? 18m : 7m;
            var perKg = ctx.Request.Mode == FulfillmentMode.Express ? 6m : 3m;
            return Math.Round(baseRate + (decimal)weight * perKg, 2);
        }

        private CheckoutResult Reject(EnterpriseCheckoutContext ctx, string message)
        {
            Move(ctx, CheckoutEventKind.Reject, message);
            Save(ctx, "rejected");
            return Complete(ctx, message, accepted: false);
        }

        private CheckoutResult Complete(EnterpriseCheckoutContext ctx, string message, bool accepted)
            => new(
                accepted,
                message,
                ctx.Stage,
                ctx.Total,
                ctx.WorkItems.ToArray(),
                ctx.Audit.Concat(audit.Entries).ToArray(),
                history.CurrentVersion);

        private void Move(EnterpriseCheckoutContext ctx, CheckoutEventKind kind, string detail)
        {
            var evt = new CheckoutEvent(ctx.Request.OrderId, kind, detail);
            var stage = ctx.Stage;
            stages.Transition(ref stage, evt);
            ctx.Stage = stage;
            events.Publish(evt);
            ctx.Audit.Add($"stage:{ctx.Stage}");
        }

        private void Save(EnterpriseCheckoutContext ctx, string note)
            => history.Save(new CheckoutSnapshot(ctx.Request.OrderId, ctx.Stage, ctx.Total, note), note);
    }

    private sealed class FlatTaxPolicy(decimal rate) : IRegionalTaxPolicy
    {
        public decimal Apply(decimal taxableAmount) => Math.Round(taxableAmount * (1m + rate), 2);
    }

    private sealed class ThresholdRiskPolicy(decimal threshold) : IRegionalRiskPolicy
    {
        public bool Allows(PaymentCharge charge) => charge.Amount <= threshold;
    }

    private sealed class RegionalPaymentGateway(string name, AuditLog audit) : IRegionalPaymentGateway
    {
        public string Name => name;

        public PaymentReceipt Charge(PaymentCharge charge)
        {
            audit.Add($"gateway:{Name}:{charge.OrderId}");
            return new PaymentReceipt(true, Name, $"{Name[..Math.Min(3, Name.Length)].ToUpperInvariant()}-{charge.OrderId}");
        }
    }
}
