using PatternKit.Creational.Builder;

namespace PatternKit.Examples.Pricing;

public delegate ValueTask PricingStep(PricingContext ctx, CancellationToken ct);

public sealed class PricingPipeline
{
    private readonly PricingStep[] _steps;
    public PricingPipeline(PricingStep[] steps) => _steps = steps;

    public async ValueTask<PricingResult> RunAsync(PricingContext ctx, CancellationToken ct = default)
    {
        foreach (var step in _steps) await step(ctx, ct);

        // Summarize
        var res = new PricingResult();
        foreach (var li in ctx.Items)
        {
            res.Subtotal += li.Sku.HasTag("no-subtotal") ? 0m : li.BasePrice * li.Qty;
            res.Discounts += li.UnitDiscount * li.Qty;
            res.Taxes += li.UnitTax * li.Qty;
            res.Total += li.LineNet; // includes adjustment
        }
        res.Log.AddRange(ctx.Log);
        ctx.Log.Add($"summary: subtotal={res.Subtotal:0.00} discounts={res.Discounts:0.00} taxes={res.Taxes:0.00} total={res.Total:0.00}");
        return res;
    }
}

public sealed class PricingPipelineBuilder
{
    private readonly ChainBuilder<PricingStep> _steps = ChainBuilder<PricingStep>.Create();

    public static PricingPipelineBuilder New() => new();

    public PricingPipelineBuilder Add(PricingStep step) { _steps.Add(step); return this; }

    public PricingPipeline Build() => _steps.Build(arr => new PricingPipeline(arr));

    // ---- Turnkey adders ----

    public PricingPipelineBuilder AddPriceResolution(SourceRouter router)
        => Add(async (ctx, ct) =>
        {
            foreach (var li in ctx.Items)
            {
                var sku = li.Sku;
                var src = router.Resolve(in sku);
                var price = await src.TryGetUnitPriceAsync(sku, ctx.Location, ct);
                if (price is null)
                {
                    ctx.Log.Add($"price:{li.Sku.Id}:{src.Name}:missing");
                    li.BasePrice = 0m;
                }
                else
                {
                    li.BasePrice = Math.Round(price.Value, 2);
                    ctx.Log.Add($"price:{li.Sku.Id}:{src.Name}:{li.BasePrice:0.00}");
                }
            }
        });

    public PricingPipelineBuilder AddLoyalty(params ILoyaltyRule[] rules)
        => Add((ctx, ct) =>
        {
            _ = ct;
            // For each line, optionally apply multiple loyalty rules respecting exclusivity
            foreach (var li in ctx.Items)
            {
                var appliedExclusive = false;
                foreach (var m in ctx.Loyalty)
                {
                    // find rule by program
                    var rule = Array.Find(rules, r => string.Equals(r.Program, m.ProgramCode, StringComparison.OrdinalIgnoreCase));
                    if (rule is null) continue;
                    if (appliedExclusive && !rule.CanStack) continue; // respect earlier exclusive
                    if (!rule.AppliesTo(li, ctx)) continue;
                    var d = rule.ComputeUnitDiscount(li, ctx);
                    if (d <= 0) continue;
                    li.UnitDiscount += d;
                    ctx.Log.Add($"loyalty:{m.ProgramCode}:{li.Sku.Id}:-{d:0.00}");
                    if (!rule.CanStack) appliedExclusive = true;
                }
            }
            return ValueTask.CompletedTask;
        });

    public PricingPipelineBuilder AddPaymentDiscounts(Dictionary<PaymentKind, decimal> percents)
        => Add((ctx, ct) =>
        {
            _ = ct;
            if (!percents.TryGetValue(ctx.Payment, out var pct) || pct <= 0) return ValueTask.CompletedTask;
            foreach (var li in ctx.Items)
            {
                var d = Math.Round(li.BasePrice * pct, 2);
                li.UnitDiscount += d;
            }
            ctx.Log.Add($"paydisc:{ctx.Payment}:{pct:P0}");
            return ValueTask.CompletedTask;
        });

    public PricingPipelineBuilder AddBundleDiscount(string key, int thresholdQty, decimal unitOff)
        => Add((ctx, ct) =>
        {
            _ = ct;
            var qty = ctx.Items.Where(li => li.Sku.BundleKey == key).Sum(li => li.Qty);
            if (qty < thresholdQty) return ValueTask.CompletedTask;
            foreach (var li in ctx.Items)
                if (li.Sku.BundleKey == key)
                {
                    li.UnitDiscount += unitOff;
                    ctx.Log.Add($"bundle:{key}:{li.Sku.Id}:-{unitOff:0.00}");
                }
            return ValueTask.CompletedTask;
        });

    public PricingPipelineBuilder AddCoupons()
        => Add((ctx, ct) =>
        {
            _ = ct;
            foreach (var c in ctx.Coupons)
            {
                foreach (var li in ctx.Items)
                {
                    if (!li.Sku.HasTag("coupon:eligible")) continue;
                    var d = c.Percent ? Math.Round(li.BasePrice * c.Amount, 2) : c.Amount;
                    li.UnitDiscount += d;
                    ctx.Log.Add($"coupon:{c.Code}:{li.Sku.Id}:-{d:0.00}");
                }
            }
            return ValueTask.CompletedTask;
        });

    public PricingPipelineBuilder AddTaxes(ITaxPolicy policy)
        => Add((ctx, ct) =>
        {
            _ = ct;
            foreach (var li in ctx.Items)
            {
                var t = policy.ComputeUnitTax(li, ctx);
                if (t <= 0) continue;
                li.UnitTax += t;
                ctx.Log.Add($"tax:{li.Sku.Id}:+{t:0.00}");
            }
            return ValueTask.CompletedTask;
        });

    public PricingPipelineBuilder AddRounding(params IRoundingRule[] rules)
        => Add((ctx, ct) =>
        {
            _ = ct;
            // compute total pre-rounding
            decimal total = 0m;
            foreach (var li in ctx.Items) total += li.UnitNet * li.Qty;

            foreach (var r in rules)
            {
                if (r.ShouldApply(ctx, total))
                {
                    var delta = r.ComputeDelta(ctx, total);
                    if (delta != 0m)
                    {
                        // find a target line to absorb delta
                        var target = r.ChooseTargetLine(ctx) ?? ctx.Items.FirstOrDefault();
                        if (target is not null)
                        {
                            target.PriceAdjustment += delta; // applied once per line
                            ctx.Log.Add($"round:{r.Key}:{delta:+0.00;-0.00}");
                            total += delta;
                        }
                    }
                    break; // first-match wins
                }
            }
            return ValueTask.CompletedTask;
        });
}

// ---- Loyalty ----

public interface ILoyaltyRule
{
    string Program { get; }
    bool CanStack { get; }
    bool AppliesTo(LineItem item, PricingContext ctx);
    decimal ComputeUnitDiscount(LineItem item, PricingContext ctx);
}

public sealed class PercentLoyaltyRule : ILoyaltyRule
{
    public string Program { get; }
    public bool CanStack { get; }
    private readonly Func<LineItem, PricingContext, bool> _pred;
    private readonly decimal _pct;
    public PercentLoyaltyRule(string program, bool canStack, decimal pct, Func<LineItem, PricingContext, bool>? pred = null)
    { Program = program; CanStack = canStack; _pct = pct; _pred = pred ?? ((_, _) => true); }
    public bool AppliesTo(LineItem item, PricingContext ctx) => _pred(item, ctx);
    public decimal ComputeUnitDiscount(LineItem item, PricingContext ctx) => Math.Round(item.BasePrice * _pct, 2);
}

// ---- Taxes ----

public interface ITaxPolicy
{
    decimal ComputeUnitTax(LineItem item, PricingContext ctx);
}

public sealed class RegionCategoryTaxPolicy : ITaxPolicy
{
    private readonly Dictionary<string, decimal> _regionRate; // region -> pct
    private readonly HashSet<string> _exemptCategories;
    public RegionCategoryTaxPolicy(Dictionary<string, decimal> regionRate, IEnumerable<string>? exemptCats = null)
    { _regionRate = regionRate; _exemptCategories = new HashSet<string>(exemptCats ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase); }
    public decimal ComputeUnitTax(LineItem item, PricingContext ctx)
    {
        if (item.Sku.Category is not null && _exemptCategories.Contains(item.Sku.Category)) return 0m;
        if (!_regionRate.TryGetValue(ctx.Location.Region, out var pct) || pct <= 0) return 0m;
        // tariffs via tag e.g. tariff:0.05
        var tariff = 0m;
        var tag = item.Sku.Tags?.FirstOrDefault(t => t.StartsWith("tariff:", StringComparison.OrdinalIgnoreCase));
        if (tag is not null && decimal.TryParse(tag.AsSpan(7), out var tv)) tariff = tv;
        var rate = pct + tariff;
        var tax = Math.Round((item.BasePrice - item.UnitDiscount) * rate, 2);
        return Math.Max(0m, tax);
    }
}

// ---- Rounding ----

public interface IRoundingRule
{
    string Key { get; }
    bool ShouldApply(PricingContext ctx, decimal currentTotal);
    decimal ComputeDelta(PricingContext ctx, decimal currentTotal);
    LineItem? ChooseTargetLine(PricingContext ctx);
}

public sealed class CharityRoundUpRule : IRoundingRule
{
    public string Key => "charity-up";
    public bool ShouldApply(PricingContext ctx, decimal total) => ctx.Items.Any(li => li.Sku.HasTag("charity"));
    public decimal ComputeDelta(PricingContext ctx, decimal total)
    {
        var next = Math.Ceiling(total);
        return Math.Round(next - total, 2);
    }
    public LineItem? ChooseTargetLine(PricingContext ctx) => ctx.Items.FirstOrDefault(li => li.Sku.HasTag("charity"));
}

public sealed class NickelCashOnlyRule : IRoundingRule
{
    public string Key => "nickel";
    public bool ShouldApply(PricingContext ctx, decimal total)
        => ctx.Payment == PaymentKind.Cash && ctx.Items.Any(li => li.Sku.HasTag("round:nickel"));
    public decimal ComputeDelta(PricingContext ctx, decimal total)
    {
        // Round to nearest $0.05
        var cents = (int)Math.Round((total * 100m) % 5m, 0);
        // compute nearest nickel delta
        var remainder = (total * 100m) % 5m; // 0..4.999
        var down = remainder;
        var up = (5m - remainder) % 5m;
        var deltaCents = (up <= down) ? up : -down;
        return Math.Round(deltaCents / 100m, 2);
    }
    public LineItem? ChooseTargetLine(PricingContext ctx) => ctx.Items.FirstOrDefault(li => li.Sku.HasTag("round:nickel"));
}

