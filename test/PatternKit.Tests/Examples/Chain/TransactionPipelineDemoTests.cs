using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Chain;
using PatternKit.Examples.Chain.ConfigDriven;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using PaymentPipeline = PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.PaymentPipeline;

namespace PatternKit.Tests.Examples.Chain;

[Feature("Config-driven transaction pipeline (DI + fluent chains)")]
public sealed class TransactionPipelineDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // --- helpers -------------------------------------------------------------

    private static IConfiguration Config(string[] discounts, string[] rounding)
    {
        var dict = new Dictionary<string, string?>();
        for (var i = 0; i < discounts.Length; i++)
            dict[$"Payment:Pipeline:DiscountRules:{i}"] = discounts[i];
        for (var i = 0; i < rounding.Length; i++)
            dict[$"Payment:Pipeline:Rounding:{i}"] = rounding[i];

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static PaymentPipeline BuildPipeline(string[] discounts, string[] rounding)
    {
        var services = new ServiceCollection();
        var cfg = Config(discounts, rounding);
        services.AddPaymentPipeline(cfg);
        return services.BuildServiceProvider().GetRequiredService<PaymentPipeline>();
    }

    private static TransactionContext Ctx(
        IEnumerable<LineItem> items,
        IEnumerable<Tender>? tenders = null,
        Customer? customer = null)
        => new()
        {
            Customer = customer ?? new Customer(null, 35),
            Items = items.ToList(),
            Tenders = (tenders ?? []).ToList()
        };

    // item helper: choose price so total (with 8.75% tax) == 24.98
    private static readonly LineItem Item_22_97 = new("SKU-1", 22.97m);

    // --- scenarios -----------------------------------------------------------

    [Scenario("Mixed tender: $20 cash then remainder on Visa — no nickel rounding (not cash-only)")]
    [Fact]
    public async Task MixedTender_NoNickelRounding()
    {
        PaymentPipeline Pipe() => BuildPipeline(discounts: [],
            rounding: ["round:nickel-cash-only"]);

        var ctx = Ctx(
            items: [Item_22_97], // Subtotal 22.97 -> tax 2.01 -> total 24.98
            tenders:
            [
                new Tender(PaymentKind.Cash, CashGiven: 20m),
                new Tender(PaymentKind.Card, CardAuthType.Contactless, CardVendor.Visa)
            ]);

        await Given("a pipeline with nickel rounding only", Pipe)
            .When("the pipeline runs", p => p.Run(ctx))
            .Then("preauth passes", r => r.Result.Ok)
            .And("subtotal is 22.97", _ => ctx.Subtotal == 22.97m)
            .And("tax is 2.01", _ => ctx.TaxTotal == 2.01m)
            .And("rounding skipped", _ => ctx.RoundingDelta == 0m && ctx.Log.Any(x => x.Contains("skipped (not cash-only)")))
            .And("grand total remains 24.98", _ => ctx.GrandTotal == 24.98m)
            .And("cash payment applied first ($20.00)", _ => ctx.Log.Any(x => x.Contains("paid: cash $20.00")))
            .And("card captures the remainder ($4.98)", _ => ctx.Log.Any(x => x.Contains("auth: captured via Visa $4.98")))
            .And("paid in full", _ => ctx is { RemainderDue: 0m, AmountPaid: 24.98m } && ctx.Result!.Value.Code == "paid")
            .AssertPassed();
    }

    [Scenario("Cash-only: nickel rounding up to $25.00, single cash tender covers all")]
    [Fact]
    public async Task CashOnly_NickelRounding_Up()
    {
        PaymentPipeline Pipe() => BuildPipeline(discounts: [],
            rounding: ["round:nickel-cash-only"]);

        var ctx = Ctx(
            items: [Item_22_97], // pre-round total 24.98
            tenders: [new Tender(PaymentKind.Cash, CashGiven: 25m)]);

        await Given("a pipeline with nickel rounding only", Pipe)
            .When("the pipeline runs", p => p.Run(ctx))
            .Then("preauth passes", r => r.Result.Ok)
            .And("pre-round total is 24.98", _ => Math.Round(ctx.Subtotal - ctx.DiscountTotal + ctx.TaxTotal, 2) == 24.98m)
            .And("nickel rounding adds $0.02", _ => ctx.RoundingDelta == 0.02m && ctx.Log.Any(x => x.Contains("nickel (cash-only) +$0.02")))
            .And("grand total becomes $25.00", _ => ctx.GrandTotal == 25.00m)
            .And("cash pays $25.00", _ => ctx is { AmountPaid: 25.00m, CashChange: null })
            .And("result is paid", _ => ctx.RemainderDue == 0m && ctx.Result!.Value.Code == "paid")
            .AssertPassed();
    }

    [Scenario("Charity round-up to the next dollar (independent of tender mix)")]
    [Fact]
    public async Task Charity_RoundUp_Works()
    {
        var ctx = Ctx(
            items:
            [
                Item_22_97,
                new LineItem("CHARITY:RedCross", 0m) // signal charity
            ],
            tenders: [new Tender(PaymentKind.Card, CardAuthType.Chip, CardVendor.Visa)]);

        await Given("a pipeline with charity round-up only", Pipe)
            .When("the pipeline runs", p => p.Run(ctx))
            .Then("preauth passes", r => r.Result.Ok)
            .And("charity rounding adds $0.02", _ => ctx.RoundingDelta == 0.02m && ctx.Log.Any(x => x.Contains("charity RedCross")))
            .And("grand total = pre-round + $0.02", _ =>
            {
                var pre = Math.Round(ctx.Subtotal - ctx.DiscountTotal + ctx.TaxTotal, 2);
                return ctx.GrandTotal == pre + 0.02m;
            })
            .And("paid by card", _ => ctx.Result!.Value.Code == "paid" && ctx.Log.Any(x => x.Contains("auth: captured")))
            .AssertPassed();
        return;

        PaymentPipeline Pipe() => BuildPipeline(discounts: [],
            rounding: ["round:charity"]);
    }

    [Scenario("Preauth: age-restricted item blocks underage customer")]
    [Fact]
    public async Task Preauth_AgeBlock()
    {
        var ctx = Ctx(
            items: [new LineItem("CIGS", 9.99m, AgeRestricted: true)],
            customer: new Customer(null, 19));

        await Given("a default pipeline (no discounts/rounding)", Pipe)
            .When("the pipeline runs", p => p.Run(ctx))
            .Then("fails preauth", r => r.Result is { Ok: false, Code: "age" })
            .And("log mentions age block", _ => ctx.Log.Any(x => x.Contains("age")))
            .AssertPassed();
        return;

        PaymentPipeline Pipe() => BuildPipeline(discounts: [], rounding: []);
    }

    [Scenario("No tenders -> insufficient funds")]
    [Fact]
    public async Task NoTenders_Insufficient()
    {
        var ctx = Ctx(items: [new LineItem("SKU", 10m)]); // total > 0, but no payments

        await Given("a default pipeline", Pipe)
            .When("the pipeline runs", p => p.Run(ctx))
            .Then("insufficient funds", r => r.Result is { Ok: false, Code: "insufficient" })
            .And("remainder due > 0", _ => ctx.RemainderDue > 0m)
            .AssertPassed();
        return;

        PaymentPipeline Pipe() => BuildPipeline(discounts: [], rounding: []);
    }
}