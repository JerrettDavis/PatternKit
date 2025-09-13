using PatternKit.Behavioral.Chain;
using PatternKit.Creational.Builder;
using PatternKit.Examples.Chain.ConfigDriven;

namespace PatternKit.Examples.Chain;

// pipeline stage contract (true = continue; false = stop)
public delegate bool Stage(TransactionContext ctx);

// ---------- Tender router (BranchBuilder-powered strategy selection) ----------
public delegate bool TenderPred(in TransactionContext c, in Tender t);

public delegate TxResult TenderStep(TransactionContext c, in Tender t);

public delegate TxResult TenderRouter(TransactionContext c, in Tender t);

public static class TenderRouterFactory
{
    public static TenderRouter Build(IEnumerable<ITenderHandler> handlers)
    {
        var bb = BranchBuilder<TenderPred, TenderStep>.Create();

        foreach (var handler in handlers)
        {
            var h = handler; // avoid modified-closure issue
            bb.Add(
                // wrap CanHandle so it matches: (in TransactionContext, in Tender) -> bool
                (in c, in t) => h.CanHandle(c, t),
                // wrap Handle so it matches: (TransactionContext, in Tender) -> TxResult
                (c, in t) => h.Handle(c, t)
            );
        }

        return bb.Build<TenderRouter>(
            fallbackDefault: static (ctx, in t)
                => TxResult.Fail("route", $"no handler for {t.Kind}"),
            projector: static (predicates, steps, _, @default) =>
            {
                return (ctx, in t) =>
                {
                    for (var i = 0; i < predicates.Length; i++)
                        if (predicates[i](in ctx, in t))
                            return steps[i](ctx, in t);

                    return @default(ctx, in t);
                };
            });
    }
}

// ---------- Mini helper to wrap ActionChain into a Stage ----------
public static class ChainStage
{
    public static Stage From(ActionChain<TransactionContext> chain)
        => ctx =>
        {
            chain.Execute(ctx);
            return ctx.Result?.Ok != false;
        };
}

// ---------- Builder that composes the pipeline declaratively ----------
public sealed class TransactionPipeline(Stage[] stages)
{
    public (TxResult Result, TransactionContext Ctx) Run(TransactionContext ctx)
        => stages.Any(s => !s(ctx))
            ? (ctx.Result!.Value, ctx)
            : (ctx.Result ?? TxResult.Success("paid", "paid in full"), ctx);
}

public sealed class TransactionPipelineBuilder
{
    private readonly ChainBuilder<Stage> _chain = ChainBuilder<Stage>.Create();

    // deps
    private IDeviceBus _devices = new DeviceBus();
    private readonly List<ITenderHandler> _tenderHandlers = [];

    private IReadOnlyList<IRoundingRule> _roundingRules =
    [
        new CharityRoundUpRule(new NoopCharityTracker()),
        new NickelCashOnlyRule()
    ];

    public static TransactionPipelineBuilder New() => new();

    public TransactionPipelineBuilder WithDeviceBus(IDeviceBus devices)
    {
        _devices = devices;
        return this;
    }

    public TransactionPipelineBuilder WithTenderHandlers(params ITenderHandler[] handlers)
    {
        _tenderHandlers.AddRange(handlers);
        return this;
    }

    public TransactionPipelineBuilder WithRoundingRules(params IRoundingRule[] rules)
    {
        _roundingRules = rules;
        return this;
    }
    
    public TransactionPipelineBuilder AddStage(Stage stage)
    {
        _chain.Add(stage);
        return this;
    }

    public TransactionPipelineBuilder AddStage(ActionChain<TransactionContext> chain)
        => AddStage(ChainStage.From(chain));
    

    // --- PREAUTH via ActionChain (no if/else) --------------------------------
    public TransactionPipelineBuilder AddPreauth()
    {
        var preauth = ActionChain<TransactionContext>.Create()
            .When(static (in c) => c.Items.Any(i => i.AgeRestricted) && c.Customer.AgeYears < 21)
            .ThenStop(static c =>
            {
                c.Result = TxResult.Fail("age", "age verification failed");
                c.Log.Add("preauth: blocked by age restriction");
            })
            .When(static (in c) => c.Items.Count == 0)
            .ThenStop(static c =>
            {
                c.Result = TxResult.Fail("empty", "no items");
                c.Log.Add("preauth: empty basket");
            })
            .Finally(static (in c, next) =>
            {
                c.Log.Add("preauth: ok");
                next(in c);
            })
            .Build();

        _chain.Add(ChainStage.From(preauth));
        return this;
    }

    // --- DISCOUNTS & TAX via ActionChain (no if/else) ------------------------
    public TransactionPipelineBuilder AddDiscountsAndTax()
    {
        var totals = ActionChain<TransactionContext>.Create()
            .Use(static (in c, next) =>
            {
                c.RecomputeSubtotal();
                c.Log.Add($"subtotal: {c.Subtotal:C2}");
                next(in c);
            })
            .When(static (in c) => (c.Tenders.Count > 0 ? c.Tenders[0] : c.Tender)?.Kind == PaymentKind.Cash)
            .ThenContinue(static c =>
            {
                var off = Math.Round(c.Subtotal * 0.02m, 2);
                c.AddDiscount(off, "cash 2% off");
            })
            .When(static (in c) => !string.IsNullOrWhiteSpace(c.Customer.LoyaltyId))
            .ThenContinue(static c =>
            {
                var off = Math.Round(c.Subtotal * 0.05m, 2);
                c.AddDiscount(off, $"loyalty {c.Customer.LoyaltyId}");
            })
            .When(static (in c) => c.Items.Any(i => i.ManufacturerCoupon > 0m))
            .ThenContinue(static c =>
            {
                var off = c.Items.Sum(i => i.ManufacturerCoupon * i.Qty);
                c.AddDiscount(off, "manufacturer coupons");
            })
            .When(static (in c) => c.Items.Any(i => i.InHouseCoupon > 0m))
            .ThenContinue(static c =>
            {
                var off = c.Items.Sum(i => i.InHouseCoupon * i.Qty);
                c.AddDiscount(off, "in-house coupons");
            })
            .When(static (in c) =>
                c.Items.GroupBy(i => i.BundleKey).Any(g => g.Key is not null && g.Sum(i => i.Qty) >= 2))
            .ThenContinue(static c =>
            {
                var off = c.Items.GroupBy(i => i.BundleKey)
                    .Where(g => g.Key is not null && g.Sum(i => i.Qty) >= 2)
                    .Sum(g => g.Sum(i => i.Qty) * 1.00m);
                c.AddDiscount(off, "bundle deal");
            })
            .Finally(static (in c, next) =>
            {
                var taxable = Math.Max(0m, c.Subtotal - c.DiscountTotal);
                var tax = Math.Round(taxable * 0.0875m, 2);
                c.SetTax(tax);
                c.Log.Add($"pre-round total: {c.GrandTotal:C2}");
                next(in c);
            })
            .Build();

        _chain.Add(ChainStage.From(totals));
        return this;
    }

    // --- ROUNDING via strategy list -----------------------------------------
    public TransactionPipelineBuilder AddRounding()
    {
        _chain.Add(ctx =>
        {
            RoundingPipeline.Apply(ctx, _roundingRules);
            return true;
        });
        return this;
    }


    // --- TENDER handling via BranchBuilder-powered router --------------------
    public TransactionPipelineBuilder AddTenderHandling()
    {
        // if not supplied, default to your existing cash/card strategies
        if (_tenderHandlers.Count == 0)
        {
            var processors = new CardProcessors(new()
            {
                [CardVendor.Visa] = new GenericProcessor("VisaNet"),
                [CardVendor.Mastercard] = new GenericProcessor("MC"),
                [CardVendor.Amex] = new GenericProcessor("Amex"),
                [CardVendor.Chase] = new GenericProcessor("ChaseNet"),
                [CardVendor.InHouse] = new GenericProcessor("InHouse"),
                [CardVendor.Unknown] = new GenericProcessor("FallbackNet"),
            });

            _tenderHandlers.AddRange([
                new CashTender(_devices),
                new CardTender(processors)
            ]);
        }

        var route = TenderRouterFactory.Build(_tenderHandlers);

        _chain.Add(ctx =>
        {
            var tenders = ctx.Tenders.Count > 0
                ? ctx.Tenders
                : ctx.Tender is null
                    ? new List<Tender>()
                    : new List<Tender> { ctx.Tender };

            ctx.Result = tenders
                .TakeWhile(_ => ctx.RemainderDue > 0m)
                .Select(t => route(ctx, in t))
                .Cast<TxResult?>()
                .LastOrDefault(res => res is { Ok: true });

            return ctx.Result is null or { Ok: true };
        });

        return this;
    }

    // --- finalize ------------------------------------------------------------
    public TransactionPipelineBuilder AddFinalize()
    {
        var devices = _devices;
        _chain.Add(ctx =>
        {
            if (ctx.Result is { Ok: false }) return false;

            ctx.Result = ctx.RemainderDue > 0m
                ? TxResult.Fail("insufficient", $"still due {ctx.RemainderDue:C2}")
                : TxResult.Success("paid", "paid in full");

            if (ctx.Result.Value.Ok) devices.Beep("printer", 2);
            ctx.Log.Add("done.");
            return true;
        });
        return this;
    }

    public TransactionPipeline Build() => _chain.Build(stages => new TransactionPipeline(stages));
}

// ---------- Domain ----------
public enum PaymentKind
{
    Cash,
    Card /*, Crypto, Check ...*/
}

public enum CardAuthType
{
    Chip,
    Swipe,
    Contactless
}

public enum CardVendor
{
    Visa,
    Mastercard,
    Amex,
    Chase,
    InHouse,
    Unknown
}

public sealed record Customer(string? LoyaltyId, int AgeYears);

public sealed record LineItem(
    string Sku,
    decimal UnitPrice,
    int Qty = 1,
    bool AgeRestricted = false,
    string? BundleKey = null,
    decimal ManufacturerCoupon = 0m,
    decimal InHouseCoupon = 0m
);

public sealed record Tender(
    PaymentKind Kind,
    CardAuthType? AuthType = null,
    CardVendor? Vendor = null,
    decimal CashGiven = 0m
);

public sealed class TransactionContext
{
    public Guid Id { get; } = Guid.NewGuid();
    public required Customer Customer { get; init; }

    public Tender? Tender { get; set; }
    public List<Tender> Tenders { get; init; } = [];

    public required List<LineItem> Items { get; init; }

    // Running totals
    public decimal Subtotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal TaxTotal { get; private set; }
    public decimal RoundingDelta { get; private set; }
    public decimal GrandTotal => Subtotal - DiscountTotal + TaxTotal + RoundingDelta;

    // Tendering
    public decimal AmountPaid { get; private set; }
    public decimal RemainderDue => Math.Max(0m, Math.Round(GrandTotal - AmountPaid, 2));
    public decimal? CashChange { get; set; }

    // Card processors read this per-authorization
    public decimal AuthorizationAmount { get; set; }

    // Side-effects & logs
    public List<string> Log { get; } = [];

    // Terminal outcome
    public TxResult? Result { get; set; }

    public bool IsCashOnlyTransaction =>
        (Tenders.Count > 0
            ? Tenders
            : Tender is null
                ? []
                : new List<Tender> { Tender })
        .All(t => t.Kind == PaymentKind.Cash);

    public void RecomputeSubtotal() =>
        Subtotal = Items.Sum(i => i.UnitPrice * i.Qty);

    public void AddDiscount(decimal amount, string reason)
    {
        if (amount <= 0m) return;
        DiscountTotal += amount;
        Log.Add($"discount: {reason} {amount:C2}");
    }

    public void SetTax(decimal amount)
    {
        TaxTotal = amount;
        Log.Add($"tax: {amount:C2}");
    }

    public void ApplyRounding(decimal delta, string reason)
    {
        if (delta == 0m) return;
        RoundingDelta = Math.Round(delta, 2);
        Log.Add($"round: {reason} {(delta >= 0 ? "+" : "")}{RoundingDelta:C2}");
    }

    public void ApplyPayment(decimal amount, string how)
    {
        if (amount <= 0m) return;
        AmountPaid = Math.Round(AmountPaid + amount, 2);
        Log.Add($"paid: {how} {amount:C2} (remaining {RemainderDue:C2})");
    }
}

public readonly record struct TxResult(bool Ok, string Code, string Message)
{
    public static TxResult Success(string code = "ok", string msg = "approved") => new(true, code, msg);
    public static TxResult Fail(string code, string msg) => new(false, code, msg);
}

// ---------- External devices / services (stubs) ----------
public interface IDeviceBus
{
    void OpenCashDrawer(int drawer = 1);
    void Beep(string device, int units = 1);
}

public interface ICardProcessor
{
    TxResult Authorize(TransactionContext ctx);
    TxResult Capture(TransactionContext ctx);
}

public sealed class DeviceBus : IDeviceBus
{
    public void OpenCashDrawer(int drawer = 1)
    {
        /* talk to POS */
    }

    public void Beep(string device, int units = 1)
    {
        /* talk to peripherals */
    }
}

public sealed class CardProcessors
{
    private readonly Dictionary<CardVendor, ICardProcessor> _map;
    public CardProcessors(Dictionary<CardVendor, ICardProcessor> map) => _map = map;

    public ICardProcessor Resolve(CardVendor? v) =>
        v is not null && _map.TryGetValue(v.Value, out var p) ? p : _map[CardVendor.Unknown];
}

// ---------- Demo processors ----------
public sealed class GenericProcessor(string name) : ICardProcessor
{
    public TxResult Authorize(TransactionContext ctx) =>
        TxResult.Success("auth", $"{name}: authorized {ctx.AuthorizationAmount:C2}");

    public TxResult Capture(TransactionContext ctx) =>
        TxResult.Success("capture", $"{name}: captured {ctx.AuthorizationAmount:C2}");
}

// ---------- Tender strategies (flat, declarative) ----------
public interface ITenderStrategy
{
    PaymentKind Kind { get; }

    /// <summary>
    /// Attempts to apply this tender to the context. Returns a failure result if tendering should stop.
    /// Returns null to indicate success/continue.
    /// </summary>
    TxResult? TryApply(TransactionContext ctx, Tender tender);
}

public interface ICharityTracker
{
    void Track(string charity, Guid transactionId, decimal delta, decimal newTotal);
}

public sealed class NoopCharityTracker : ICharityTracker
{
    public void Track(string charity, Guid transactionId, decimal delta, decimal newTotal)
    {
        /* noop */
    }
}

public interface IRoundingRule
{
    /// Human readable reason (used for logging).
    string Reason { get; }

    /// Returns true if this rule should be considered for the current context.
    bool ShouldApply(in TransactionContext c);

    /// Computes the delta to apply. Return 0m for no change.
    decimal ComputeDelta(in TransactionContext c);

    /// Optional side-effects when the delta was actually applied.
    void OnApplied(TransactionContext c, decimal appliedDelta)
    {
    }
}

public sealed class CashTenderStrategy(IDeviceBus devices) : ITenderStrategy
{
    public PaymentKind Kind => PaymentKind.Cash;

    public TxResult? TryApply(TransactionContext ctx, Tender tender)
    {
        if (ctx.RemainderDue <= 0m) return null;

        devices.OpenCashDrawer();

        var applied = Math.Min(tender.CashGiven, ctx.RemainderDue);
        ctx.ApplyPayment(applied, "cash");

        // change only if this cash overpays AFTER covering all due
        var leftover = Math.Round(tender.CashGiven - applied, 2);
        if (leftover > 0m)
        {
            ctx.CashChange = (ctx.CashChange ?? 0m) + leftover;
            ctx.Log.Add($"cash: change {leftover:C2}");
        }

        return null; // success, continue
    }
}

public sealed class CardTenderStrategy(CardProcessors processors) : ITenderStrategy
{
    public PaymentKind Kind => PaymentKind.Card;

    public TxResult? TryApply(TransactionContext ctx, Tender tender)
    {
        if (ctx.RemainderDue <= 0m) return null;

        ctx.AuthorizationAmount = ctx.RemainderDue;
        if (ctx.AuthorizationAmount <= 0m) return null;

        var proc = processors.Resolve(tender.Vendor);
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
            $"card {tender.Vendor} {tender.AuthType?.ToString() ?? "Unknown"}");
        ctx.Log.Add($"auth: captured via {tender.Vendor} {ctx.AuthorizationAmount:C2}");
        return null; // success, continue
    }
}

public sealed class TenderStrategyRegistry
{
    private readonly Dictionary<PaymentKind, ITenderStrategy> _map;

    public TenderStrategyRegistry(IEnumerable<ITenderStrategy> strategies)
        => _map = strategies.ToDictionary(s => s.Kind);

    public ITenderStrategy Resolve(PaymentKind kind)
        => _map.TryGetValue(kind, out var s)
            ? s
            : throw new InvalidOperationException($"No strategy for {kind}");
}

public sealed class CharityRoundUpRule(ICharityTracker tracker) : IRoundingRule
{
    public string Reason => "charity round-up";

    public bool ShouldApply(in TransactionContext c)
        => c.Items.Any(i => i.Sku.StartsWith("CHARITY:", StringComparison.OrdinalIgnoreCase));

    public decimal ComputeDelta(in TransactionContext c)
    {
        var delta = Math.Ceiling(c.GrandTotal) - c.GrandTotal;
        return Math.Round(delta, 2);
    }

    public void OnApplied(TransactionContext c, decimal appliedDelta)
    {
        var charitySku = c.Items.First(i => i.Sku.StartsWith("CHARITY:", StringComparison.OrdinalIgnoreCase)).Sku;
        var name = charitySku["CHARITY:".Length..];
        tracker.Track(name, c.Id, appliedDelta, c.GrandTotal + appliedDelta);
        c.Log.Add($"charity: {name} notified for {appliedDelta:C2}");
    }
}

public sealed class NickelCashOnlyRule : IRoundingRule
{
    public string Reason => "nickel (cash-only)";

    public bool ShouldApply(in TransactionContext c)
        => c.IsCashOnlyTransaction && c.Items.Any(i => string.Equals(i.Sku, "ROUND:NICKEL", StringComparison.OrdinalIgnoreCase));

    public decimal ComputeDelta(in TransactionContext c)
    {
        // Round to nearest 0.05 with midpoint away from zero
        var rounded = Math.Round(c.GrandTotal * 20m, MidpointRounding.AwayFromZero) / 20m;
        return Math.Round(rounded - c.GrandTotal, 2);
    }
}

public static class RoundingPipeline
{
    public static void Apply(
        TransactionContext c,
        IReadOnlyList<IRoundingRule> rules)
    {
        foreach (var rule in rules)
        {
            if (!rule.ShouldApply(in c)) continue;

            var delta = rule.ComputeDelta(in c);
            if (delta == 0m) continue;

            c.ApplyRounding(delta, rule.Reason);
            rule.OnApplied(c, delta); // side-effects for this rule only
            c.Log.Add($"total: {c.GrandTotal:C2}");
            return; // first-match-wins: stop after one rounding
        }

        c.Log.Add("round: none");
        c.Log.Add($"total: {c.GrandTotal:C2}");
    }
}

// ---------- Pipeline demo ----------
public static class MediatedTransactionPipelineDemo
{
    public static (TxResult Result, TransactionContext Ctx) Run(TransactionContext ctx)
    {
        var pipeline = TransactionPipelineBuilder.New()
            .AddPreauth()
            .AddDiscountsAndTax()
            .AddRounding()
            .AddTenderHandling()
            .AddFinalize()
            .Build();

        return pipeline.Run(ctx);
    }
}