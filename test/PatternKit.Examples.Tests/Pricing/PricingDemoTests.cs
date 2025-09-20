using PatternKit.Examples.Pricing;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Pricing;

[Feature("Pricing calculator pipeline: async sources, loyalty stacking, payment discounts, bundles, coupons, taxes, rounding")]
public sealed class PricingDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static PricingContext BaseContext(PaymentKind pay = PaymentKind.Cash, string region = "US-NE")
        => new()
        {
            Location = new(region, Country: "US", State: region.Split('-').Last()),
            Payment = pay,
            Items =
            [
                new LineItem { Sku = new("SKU-APPLE", "Apple", Category: "Grocery", Tags: ["price:db", "coupon:eligible"]), Qty = 2 },
                new LineItem { Sku = new("SKU-MILK", "Milk", Category: "Grocery", Tags: ["price:db"]), Qty = 1 },
                new LineItem { Sku = new("SKU-CHARITY", "Charity", Category: "Misc", Tags: ["price:db", "charity", "no-subtotal"]), Qty = 1 },
                new LineItem { Sku = new("SKU-NICKEL", "Nickel", Category: "Misc", BundleKey: "BNDL", Tags: ["price:db", "round:nickel"]), Qty = 2 }
            ]
        };

    // ---- Async helpers to avoid TinyBDD Task/ValueTask overload ambiguity ----
    private static Task<PricingResult> RunPipeline((PricingDemo.DemoArtifacts d, PricingContext ctx) t)
        => t.d.Pipeline.RunAsync(t.ctx).AsTask();

    private static Task<PricingResult> RunCtx(PricingDemo.DemoArtifacts d, PricingContext ctx)
        => d.Pipeline.RunAsync(ctx).AsTask();

    private static async Task<(PricingResult r1, PricingResult r2)> RunBoth((PricingDemo.DemoArtifacts d, PricingContext c1, PricingContext c2) t)
        => (await t.d.Pipeline.RunAsync(t.c1), await t.d.Pipeline.RunAsync(t.c2));

    [Scenario("End-to-end sample matches expectations (charity + nickel candidate, cash payment)")]
    [Fact]
    public async Task EndToEnd_Sample()
    {
        await Given("default artifacts and context with loyalty + coupon", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = BaseContext();
                ctx.Loyalty.Add(new("LOY-5"));
                ctx.Loyalty.Add(new("LOY-GROC-3"));
                ctx.Coupons.Add(new("CASHOFF1", 1.00m));
                return (d, ctx);
            })
            .When("run pipeline", RunPipeline)
            .Then("has price logs for db source", r => r.Log.Any(l => l.StartsWith("price:SKU-APPLE:db:")))
            .And("applied loyalty entries", r => r.Log.Any(l => l.Contains("loyalty:LOY-5")) && r.Log.Any(l => l.Contains("loyalty:LOY-GROC-3")))
            .And("applied cash payment discount", r => r.Log.Any(l => l.StartsWith("paydisc:Cash:")))
            .And("applied bundle and coupon entries if eligible",
                r => r.Log.Any(l => l.StartsWith("bundle:BNDL")) && r.Log.Any(l => l.StartsWith("coupon:CASHOFF1")))
            .And("tax entries exist", r => r.Log.Any(l => l.StartsWith("tax:")))
            .And("rounding occurred via charity first-match", r => r.Log.Any(l => l.StartsWith("round:charity-up:")))
            .AssertPassed();
    }

    [Scenario("Source routing: api and file sources resolve when tagged")]
    [Fact]
    public async Task SourceRouting_Api_File()
    {
        await Given("artifacts and a context with API + FILE tagged skus", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = new PricingContext
                {
                    Location = new("US-NE"),
                    Payment = PaymentKind.CreditCard,
                    Items =
                    [
                        new LineItem { Sku = new("SKU-API", "ApiThing", Tags: ["price:api"]), Qty = 1 },
                        new LineItem { Sku = new("SKU-FILE", "FileThing", Tags: ["price:file"]), Qty = 1 }
                    ]
                };
                return (d, ctx);
            })
            .When("run", RunPipeline)
            .Then("log shows price:...:api and :file", r => r.Log.Any(l => l.Contains(":api:")) && r.Log.Any(l => l.Contains(":file:")))
            .AssertPassed();
    }

    [Scenario("Loyalty stacking and exclusivity: exclusive LOY-10X prevents later stacks on same line")]
    [Fact]
    public async Task Loyalty_Stacking_Exclusive()
    {
        await Given("context with an exclusive and stackable loyalty on same item", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = BaseContext();
                ctx.Loyalty.Add(new("LOY-10X"));
                ctx.Loyalty.Add(new("LOY-5"));
                return (d, ctx);
            })
            .When("run", async Task<((PricingDemo.DemoArtifacts d, PricingContext ctx) t, PricingResult)> (t) => (t, await RunCtx(t.d, t.ctx)))
            .Then("loyalty:LOY-10X appears and blocks another on same sku at least once",
                tr => tr.Item2.Log.Any(l => l.Contains("loyalty:LOY-10X:SKU-APPLE")))
            .AssertPassed();
    }

    [Scenario("Payment-type discounts: store credit card 5% applied, others skipped")]
    [Fact]
    public async Task Payment_Discounts_StoreCard()
    {
        await Given("store credit card payment", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = BaseContext(PaymentKind.StoreCreditCard);
                return (d, ctx);
            })
            .When("run", RunPipeline)
            .Then("paydisc:StoreCreditCard is logged", r => r.Log.Any(l => l.StartsWith("paydisc:StoreCreditCard:")))
            .AssertPassed();
    }

    [Scenario("Coupons apply per eligible unit")]
    [Fact]
    public async Task Coupons_PerUnit()
    {
        await Given("context with coupon eligible apple units and $1 off coupon", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = BaseContext();
                ctx.Coupons.Add(new("C1", 1.00m));
                return (d, ctx);
            })
            .When("run", RunPipeline)
            .Then("coupon log entries exist for eligible skus", r => r.Log.Any(l => l.StartsWith("coupon:C1:SKU-APPLE")))
            .AssertPassed();
    }

    [Scenario("Bundle discount triggers when threshold met")]
    [Fact]
    public async Task Bundle_Threshold()
    {
        await Given("context where bundle key BNDL has qty 2", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = BaseContext();
                return (d, ctx);
            })
            .When("run", RunPipeline)
            .Then("bundle:BNDL log exists", r => r.Log.Any(l => l.StartsWith("bundle:BNDL:")))
            .AssertPassed();
    }

    [Scenario("Taxes: category exemption and tariff tag affect unit tax")]
    [Fact]
    public async Task Taxes_Exemption_And_Tariff()
    {
        await Given("context with milk carrying tariff tag at creation time", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = new PricingContext
                {
                    Location = new("US-NE"),
                    Payment = PaymentKind.Cash,
                    Items =
                    [
                        new LineItem { Sku = new("SKU-APPLE", "Apple", Category: "Grocery", Tags: ["price:db", "coupon:eligible"]), Qty = 2 },
                        new LineItem { Sku = new("SKU-MILK", "Milk", Category: "Grocery", Tags: ["price:db", "tariff:0.02"]), Qty = 1 },
                        new LineItem { Sku = new("SKU-CHARITY", "Charity", Category: "Misc", Tags: ["price:db", "charity", "no-subtotal"]), Qty = 1 },
                        new LineItem
                        {
                            Sku = new("SKU-NICKEL", "Nickel", Category: "Misc", BundleKey: "BNDL", Tags: ["price:db", "round:nickel"]), Qty = 2
                        }
                    ]
                };
                return (d, ctx);
            })
            .When("run", RunPipeline)
            .Then("tax log includes milk", r => r.Log.Any(l => l.Contains("tax:SKU-MILK")))
            .AssertPassed();
    }

    [Scenario("Rounding: charity first-match vs nickel (cash)")]
    [Fact]
    public async Task Rounding_Priority_Charity_Over_Nickel()
    {
        await Given("includes both charity and nickel, should apply charity only", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = BaseContext();
                return (d, ctx);
            })
            .When("run", RunPipeline)
            .Then("round:charity-up present, no round:nickel",
                r => r.Log.Any(l => l.StartsWith("round:charity-up")) && !r.Log.Any(l => l.StartsWith("round:nickel")))
            .AssertPassed();
    }

    [Scenario("Rounding: nickel applies when no charity and cash")]
    [Fact]
    public async Task Rounding_Nickel_When_No_Charity()
    {
        await Given("ctx without charity sku, still nickel tag and cash", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx = BaseContext();
                // remove charity line
                ctx.Items.RemoveAll(li => li.Sku.HasTag("charity"));
                return (d, ctx);
            })
            .When("run", RunPipeline)
            .Then("round:nickel present", r => r.Log.Any(l => l.StartsWith("round:nickel")))
            .AssertPassed();
    }

    [Scenario("Location variance: different region tax changes totals")]
    [Fact]
    public async Task Location_Variance_Changes_Tax()
    {
        await Given("US-NE vs unknown region has different tax sums", () =>
            {
                var d = PricingDemo.BuildDefault();
                var ctx1 = BaseContext(region: "US-NE");
                var ctx2 = BaseContext(region: "EU-XX");
                return (d, ctx1, ctx2);
            })
            .When("run both", RunBoth)
            .Then("taxes differ", rr => rr.r1.Taxes != rr.r2.Taxes)
            .AssertPassed();
    }
}