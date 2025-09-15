using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PatternKit.Examples.Chain.ConfigDriven;

// ---- Strategy contracts ----

/// <summary>
/// Represents a discount rule that can mutate a <see cref="TransactionContext"/> by applying a discount.
/// </summary>
/// <remarks>
/// Implementations should be deterministic and idempotent within a single pipeline run.
/// </remarks>
public interface IDiscountRule
{
    /// <summary>
    /// Unique key used to reference this rule from configuration (e.g., "discount:cash-2pc").
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Applies the discount to the provided <paramref name="ctx"/> (if applicable).
    /// </summary>
    /// <param name="ctx">The transaction context.</param>
    void Apply(TransactionContext ctx);
}

/// <summary>
/// Represents a rounding strategy that can adjust the <see cref="TransactionContext.GrandTotal"/> via rounding.
/// </summary>
public interface IRoundingStrategy
{
    /// <summary>
    /// Unique key used to reference this strategy from configuration (e.g., "round:charity").
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Applies rounding to the provided <paramref name="ctx"/> (if applicable).
    /// </summary>
    /// <param name="ctx">The transaction context.</param>
    void Apply(TransactionContext ctx);
}

/// <summary>
/// Handler that determines if it can process a given tender and performs the handling.
/// </summary>
public interface ITenderHandler
{
    /// <summary>
    /// Unique key for display/configuration (e.g., "tender:cash", "tender:card").
    /// </summary>
    string Key { get; } // e.g. "cash", "card:visa", etc.

    /// <summary>
    /// Returns <see langword="true"/> if this handler can process <paramref name="t"/> in <paramref name="ctx"/>.
    /// </summary>
    /// <param name="ctx">The transaction context.</param>
    /// <param name="t">The tender candidate.</param>
    bool CanHandle(TransactionContext ctx, Tender t);

    /// <summary>
    /// Performs tender handling and returns the outcome.
    /// </summary>
    /// <param name="ctx">The transaction context.</param>
    /// <param name="t">The tender to process.</param>
    /// <returns>A <see cref="TxResult"/> describing the outcome.</returns>
    TxResult Handle(TransactionContext ctx, Tender t);
}

// ---- Config model (what to run & in what order) ----

/// <summary>
/// Options that describe which pipeline components to run and in what order.
/// </summary>
public sealed class PipelineOptions
{
    /// <summary>
    /// Discount rule keys in execution order.
    /// </summary>
    public List<string> DiscountRules { get; init; } = []; // keys in order

    /// <summary>
    /// Rounding strategy keys in execution order.
    /// </summary>
    public List<string> Rounding { get; init; } = [];      // keys in order

    /// <summary>
    /// Optional tender display order (purely informational).
    /// </summary>
    public List<string> TenderOrder { get; init; } = [];   // optional, for display
}

// --- Discount rules ---

/// <summary>
/// Applies a 2% discount when the first tender is cash.
/// </summary>
public sealed class Cash2Pct : IDiscountRule
{
    /// <inheritdoc />
    public string Key => "discount:cash-2pc";

    /// <inheritdoc />
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

/// <summary>
/// Applies a 5% discount when a loyalty ID is present.
/// </summary>
public sealed class Loyalty5Pct : IDiscountRule
{
    /// <inheritdoc />
    public string Key => "discount:loyalty-5pc";

    /// <inheritdoc />
    public void Apply(TransactionContext ctx)
    {
        if (!string.IsNullOrWhiteSpace(ctx.Customer.LoyaltyId))
        {
            var off = Math.Round(ctx.Subtotal * 0.05m, 2);
            ctx.AddDiscount(off, $"loyalty {ctx.Customer.LoyaltyId}");
        }
    }
}

/// <summary>
/// Applies a $1 off per bundled item when the same bundle key reaches a quantity of two or more.
/// </summary>
public sealed class Bundle1OffEach : IDiscountRule
{
    /// <inheritdoc />
    public string Key => "discount:bundle-1off";

    /// <inheritdoc />
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

/// <summary>
/// Rounds up to the next dollar when a "CHARITY:*" SKU is present.
/// </summary>
public sealed class CharityRoundUp : IRoundingStrategy
{
    /// <inheritdoc />
    public string Key => "round:charity";

    /// <inheritdoc />
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

/// <summary>
/// Rounds to the nearest nickel when the transaction is cash-only and includes the "ROUND:NICKEL" SKU.
/// </summary>
public sealed class NickelCashOnly : IRoundingStrategy
{
    /// <inheritdoc />
    public string Key => "round:nickel-cash-only";

    /// <inheritdoc />
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

/// <summary>
/// Handles cash tenders by opening the drawer, applying payment, and computing change.
/// </summary>
public sealed class CashTender : ITenderHandler
{
    private readonly IDeviceBus _devices;
    /// <summary>Creates a new cash tender handler.</summary>
    public CashTender(IDeviceBus devices) => _devices = devices;

    /// <inheritdoc />
    public string Key => "tender:cash";

    /// <inheritdoc />
    public bool CanHandle(TransactionContext ctx, Tender t) => t.Kind == PaymentKind.Cash;

    /// <inheritdoc />
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

/// <summary>
/// Handles card tenders by authorizing and capturing the remainder due.
/// </summary>
public sealed class CardTender : ITenderHandler
{
    private readonly CardProcessors _processors;
    /// <summary>Creates a new card tender handler.</summary>
    public CardTender(CardProcessors processors) => _processors = processors;

    /// <inheritdoc />
    public string Key => "tender:card";

    /// <inheritdoc />
    public bool CanHandle(TransactionContext ctx, Tender t) => t.Kind == PaymentKind.Card;

    /// <inheritdoc />
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

/// <summary>
/// DI-friendly configuration and registration helpers for the config-driven transaction pipeline.
/// </summary>
public static class ConfigDrivenPipelineDemo
{
    /// <summary>
    /// Registers a config-driven transaction pipeline into the service collection and returns it.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="config">Application configuration containing "Payment:Pipeline" section.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <remarks>
    /// This method wires up device bus, card processors, strategies (discounts, rounding, tenders),
    /// and builds a shared <see cref="TransactionPipeline"/> using <see cref="PipelineOptions"/>.
    /// </remarks>
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
        /// <summary>
        /// Executes the registered pipeline against the provided context.
        /// </summary>
        /// <param name="ctx">The transaction context to process.</param>
        /// <returns>The terminal result and the (mutated) context.</returns>
        public (TxResult Result, TransactionContext Ctx) Run(TransactionContext ctx)
            => pipeline.Run(ctx);
    }
}