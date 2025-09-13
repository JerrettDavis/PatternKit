using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PatternKit.Examples.Chain.ConfigDriven;

// ---- Strategy contracts ----
public interface IDiscountRule
{
    string Key { get; }
    void Apply(TransactionContext ctx);
}

public interface IRoundingStrategy
{
    string Key { get; }
    void Apply(TransactionContext ctx);
}

public interface ITenderHandler
{
    string Key { get; } // e.g. "cash", "card:visa", etc.
    bool CanHandle(TransactionContext ctx, Tender t);
    TxResult Handle(TransactionContext ctx, Tender t);
}

// ---- Config model (what to run & in what order) ----
public sealed class PipelineOptions
{
    public List<string> DiscountRules { get; init; } = []; // keys in order
    public List<string> Rounding { get; init; } = [];      // keys in order
    public List<string> TenderOrder { get; init; } = [];   // optional, for display
}

// --- Discount rules ---
public sealed class Cash2Pct : IDiscountRule
{
    public string Key => "discount:cash-2pc";

    public void Apply(TransactionContext ctx)
    {
        // FIRST tender being cash is a simple proxy for "cash-driven promo"
        var first = ctx.Tenders.Count > 0 ? ctx.Tenders[0] : ctx.Tender;
        if (first?.Kind == PaymentKind.Cash)
        {
            var off = Math.Round(ctx.Subtotal * 0.02m, 2);
            ctx.AddDiscount(off, "cash 2% off");
        }
    }
}

public sealed class Loyalty5Pct : IDiscountRule
{
    public string Key => "discount:loyalty-5pc";

    public void Apply(TransactionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.Customer.LoyaltyId))
        {
            var off = Math.Round(ctx.Subtotal * 0.05m, 2);
            ctx.AddDiscount(off, $"loyalty {ctx.Customer.LoyaltyId}");
        }
    }
}

public sealed class Bundle1OffEach : IDiscountRule
{
    public string Key => "discount:bundle-1off";

    public void Apply(TransactionContext ctx)
    {
        var off = ctx.Items
            .GroupBy(i => i.BundleKey)
            .Where(g => g.Key is not null && g.Sum(i => i.Qty) >= 2)
            .Sum(g => g.Sum(i => i.Qty) * 1.00m);

        if (off > 0) ctx.AddDiscount(off, "bundle deal");
    }
}

// --- Rounding strategies ---
public sealed class CharityRoundUp : IRoundingStrategy
{
    public string Key => "round:charity";

    public void Apply(TransactionContext ctx)
    {
        var charity = ctx.Items.FirstOrDefault(i =>
            i.Sku.StartsWith("CHARITY:", StringComparison.OrdinalIgnoreCase));
        if (charity is null) return;

        var up = Math.Ceiling(ctx.GrandTotal) - ctx.GrandTotal;
        up = Math.Round(up, 2);
        if (up > 0m)
            ctx.ApplyRounding(up, $"charity {charity.Sku["CHARITY:".Length..]}");
    }
}

public sealed class NickelCashOnly : IRoundingStrategy
{
    public string Key => "round:nickel-cash-only";

    public void Apply(TransactionContext ctx)
    {
        if (!ctx.IsCashOnlyTransaction)
        {
            ctx.Log.Add("round: skipped (not cash-only)");
            return;
        }

        var rounded = Math.Round(ctx.GrandTotal * 20m, MidpointRounding.AwayFromZero) / 20m;
        var delta = Math.Round(rounded - ctx.GrandTotal, 2);
        if (delta != 0m) ctx.ApplyRounding(delta, "nickel (cash-only)");
        else ctx.Log.Add("round: nickel (cash-only) +$0.00");
    }
}

// --- Tender handlers ---
public sealed class CashTender : ITenderHandler
{
    private readonly IDeviceBus _devices;
    public CashTender(IDeviceBus devices) => _devices = devices;

    public string Key => "tender:cash";

    public bool CanHandle(TransactionContext ctx, Tender t) => t.Kind == PaymentKind.Cash;

    public TxResult Handle(TransactionContext ctx, Tender t)
    {
        _devices.OpenCashDrawer();
        var applied = Math.Min(t.CashGiven, ctx.RemainderDue);
        ctx.ApplyPayment(applied, "cash");

        var leftover = Math.Round(t.CashGiven - applied, 2);
        if (leftover > 0m)
        {
            ctx.CashChange = (ctx.CashChange ?? 0m) + leftover;
            ctx.Log.Add($"cash: change {leftover:C2}");
        }

        return TxResult.Success("cash", "ok");
    }
}

public sealed class CardTender : ITenderHandler
{
    private readonly CardProcessors _processors;
    public CardTender(CardProcessors processors) => _processors = processors;

    public string Key => "tender:card";

    public bool CanHandle(TransactionContext ctx, Tender t) => t.Kind == PaymentKind.Card;

    public TxResult Handle(TransactionContext ctx, Tender t)
    {
        ctx.AuthorizationAmount = ctx.RemainderDue;
        if (ctx.AuthorizationAmount <= 0m) return TxResult.Success("card", "nothing to pay");

        var proc = _processors.Resolve(t.Vendor);
        var auth = proc.Authorize(ctx);
        if (!auth.Ok)
        {
            ctx.Log.Add($"auth: declined ({auth.Code})");
            return auth;
        }

        var cap = proc.Capture(ctx);
        if (!cap.Ok)
        {
            ctx.Log.Add("auth: capture failed");
            return cap;
        }

        ctx.ApplyPayment(ctx.AuthorizationAmount,
            $"card {t.Vendor} {t.AuthType?.ToString() ?? "Unknown"}");
        ctx.Log.Add($"auth: captured via {t.Vendor} {ctx.AuthorizationAmount:C2}");
        return TxResult.Success("card", "ok");
    }
}

public static class ConfigDrivenPipelineDemo
{
    public static IServiceCollection AddPaymentPipeline(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<PipelineOptions>()
            .Bind(config.GetSection("Payment:Pipeline"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Core infra
        services.AddSingleton<IDeviceBus, DeviceBus>();
        services.AddSingleton(new CardProcessors(new()
        {
            [CardVendor.Visa] = new GenericProcessor("VisaNet"),
            [CardVendor.Mastercard] = new GenericProcessor("MC"),
            [CardVendor.Amex] = new GenericProcessor("Amex"),
            [CardVendor.Chase] = new GenericProcessor("ChaseNet"),
            [CardVendor.InHouse] = new GenericProcessor("InHouse"),
            [CardVendor.Unknown] = new GenericProcessor("FallbackNet"),
        }));

        // Register strategies (keyed by Key)
        services.AddSingleton<IDiscountRule, Cash2Pct>();
        services.AddSingleton<IDiscountRule, Loyalty5Pct>();
        services.AddSingleton<IDiscountRule, Bundle1OffEach>();

        services.AddSingleton<IRoundingStrategy, CharityRoundUp>();
        services.AddSingleton<IRoundingStrategy, NickelCashOnly>();

        services.AddSingleton<ITenderHandler, CashTender>();
        services.AddSingleton<ITenderHandler, CardTender>();

        // Build and register a shared TransactionPipeline from config + strategies
        services.AddSingleton<TransactionPipeline>(sp =>
        {
            var devices = sp.GetRequiredService<IDeviceBus>();
            var opts = sp.GetRequiredService<IOptions<PipelineOptions>>();
            var discountRules = sp.GetServices<IDiscountRule>();
            var rounding = sp.GetServices<IRoundingStrategy>();
            var tenderHandlers = sp.GetServices<ITenderHandler>().ToArray();

            return TransactionPipelineBuilder.New()
                .WithDeviceBus(devices)
                .AddPreauth()
                .AddConfigDrivenDiscountsAndTax(opts, discountRules)
                .AddConfigDrivenRounding(opts, rounding)
                .WithTenderHandlers(tenderHandlers)
                .AddTenderHandling()
                .AddFinalize()
                .Build();
        });

        services.AddSingleton<PaymentPipeline>();

        return services;
    }

    /// <summary>
    /// Thin wrapper so existing callers can keep using PaymentPipeline.Run(ctx).
    /// </summary>
    public sealed class PaymentPipeline(TransactionPipeline pipeline)
    {
        public (TxResult Result, TransactionContext Ctx) Run(TransactionContext ctx)
            => pipeline.Run(ctx);
    }
}