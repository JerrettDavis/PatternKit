namespace PatternKit.Examples.Pricing;

public static class PricingDemo
{
    public sealed record DemoArtifacts(
        PricingPipeline Pipeline,
        SourceRouter Sources,
        DbPricingSource Db,
        ApiPricingSource Api,
        FilePricingSource File,
        ILoyaltyRule[] Loyalty,
        ITaxPolicy Taxes,
        IRoundingRule[] Rounding,
        Dictionary<PaymentKind, decimal> PayDiscounts);

    public static DemoArtifacts BuildDefault()
    {
        // Pricing sources (stub data)
        var db = new DbPricingSource(new()
        {
            [("SKU-APPLE", "US-NE")] = 0.99m,
            [("SKU-MILK", "US-NE")] = 3.49m,
            [("SKU-NICKEL", "US-NE")] = 1.00m,
            [("SKU-CHARITY", "US-NE")] = 0.01m,
        });
        var api = new ApiPricingSource(new() { ["SKU-API"] = 4.20m });
        var file = new FilePricingSource(new() { ["SKU-FILE"] = 7.77m });
        var sources = DefaultSourceRouting.Build(db, api, file);

        // Loyalty rules
        var loyalty = new ILoyaltyRule[]
        {
            new PercentLoyaltyRule("LOY-5", canStack: true, pct: 0.05m),
            new PercentLoyaltyRule("LOY-10X", canStack: false, pct: 0.10m),
            new PercentLoyaltyRule("LOY-GROC-3", canStack: true, pct: 0.03m,
                pred: static (li, _) => string.Equals(li.Sku.Category, "Grocery", StringComparison.OrdinalIgnoreCase))
        };

        // Payment discounts
        var pay = new Dictionary<PaymentKind, decimal>
        {
            [PaymentKind.Cash] = 0.02m,
            [PaymentKind.StoreCreditCard] = 0.05m,
            [PaymentKind.StoreGiftCard] = 0.03m,
        };

        // Taxes: region base + optional per-SKU tariff:0.xx and category exemptions
        var taxes = new RegionCategoryTaxPolicy(new() { ["US-NE"] = 0.0875m }, ["Medicine"]);

        // Rounding rules (first-match wins)
        var rounding = new IRoundingRule[] { new CharityRoundUpRule(), new NickelCashOnlyRule() };

        var pipeline = PricingPipelineBuilder.New()
            .AddPriceResolution(sources)
            .AddLoyalty(loyalty)
            .AddPaymentDiscounts(pay)
            .AddBundleDiscount(key: "BNDL", thresholdQty: 2, unitOff: 1.00m)
            .AddCoupons()
            .AddTaxes(taxes)
            .AddRounding(rounding)
            .Build();

        return new DemoArtifacts(pipeline, sources, db, api, file, loyalty, taxes, rounding, pay);
    }

    public static async ValueTask<PricingResult> RunSampleAsync(CancellationToken ct = default)
    {
        var d = BuildDefault();
        var ctx = new PricingContext
        {
            Location = new Location("US-NE", Country: "US", State: "NE"),
            Payment = PaymentKind.Cash,
            Items =
            [
                new LineItem { Sku = new("SKU-APPLE", "Apple", Category: "Grocery", Tags: ["price:db"]), Qty = 2 },
                new LineItem { Sku = new("SKU-MILK", "Milk", Category: "Grocery", Tags: ["price:db", "coupon:eligible"]), Qty = 1 },
                new LineItem { Sku = new("SKU-CHARITY", "Charity", Category: "Misc", Tags: ["price:db", "charity", "no-subtotal"]), Qty = 1 },
                new LineItem
                {
                    Sku = new("SKU-NICKEL", "NickelRounder", Category: "Misc", BundleKey: "BNDL", Tags: ["price:db", "round:nickel"]), Qty = 2
                }
            ]
        };

        ctx.Loyalty.Add(new("LOY-5"));
        ctx.Loyalty.Add(new("LOY-GROC-3"));
        ctx.Coupons.Add(new("CASHOFF1", 1.00m)); // $1 off per eligible unit

        var res = await d.Pipeline.RunAsync(ctx, ct);
        return res;
    }
}

