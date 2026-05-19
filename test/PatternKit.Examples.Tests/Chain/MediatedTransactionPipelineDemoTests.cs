using PatternKit.Examples.Chain;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Chain;

public sealed class CashTenderStrategyTests
{
    [Scenario("Kind Is Cash")]
    [Fact]
    public void Kind_Is_Cash()
    {
        var devices = new DeviceBus();
        var strategy = new CashTenderStrategy(devices);

        ScenarioExpect.Equal(PaymentKind.Cash, strategy.Kind);
    }

    [Scenario("TryApply Applies Payment")]
    [Fact]
    public void TryApply_Applies_Payment()
    {
        var devices = new DeviceBus();
        var strategy = new CashTenderStrategy(devices);
        var ctx = CreateContext(10m);
        var tender = new Tender(PaymentKind.Cash, CashGiven: 20m);

        var result = strategy.TryApply(ctx, tender);

        ScenarioExpect.Null(result); // success
        ScenarioExpect.Equal(10m, ctx.CashChange);
    }

    [Scenario("TryApply Exact Payment No Change")]
    [Fact]
    public void TryApply_Exact_Payment_No_Change()
    {
        var devices = new DeviceBus();
        var strategy = new CashTenderStrategy(devices);
        var ctx = CreateContext(10m);
        var tender = new Tender(PaymentKind.Cash, CashGiven: 10m);

        var result = strategy.TryApply(ctx, tender);

        ScenarioExpect.Null(result); // success
        ScenarioExpect.Null(ctx.CashChange);
    }

    [Scenario("TryApply Returns Null When NoDue")]
    [Fact]
    public void TryApply_Returns_Null_When_NoDue()
    {
        var devices = new DeviceBus();
        var strategy = new CashTenderStrategy(devices);
        var ctx = CreateContext(10m);
        ctx.ApplyPayment(10m, "already paid"); // Fully paid
        var tender = new Tender(PaymentKind.Cash, CashGiven: 10m);

        var result = strategy.TryApply(ctx, tender);

        ScenarioExpect.Null(result);
    }

    private static TransactionContext CreateContext(decimal price)
    {
        var ctx = new TransactionContext
        {
            Customer = new Customer(LoyaltyId: null, AgeYears: 25),
            Items = [new LineItem("TEST", price, 1)]
        };
        ctx.RecomputeSubtotal();
        return ctx;
    }
}

public sealed class CardTenderStrategyTests
{
    [Scenario("Kind Is Card")]
    [Fact]
    public void Kind_Is_Card()
    {
        var processors = new CardProcessors(new Dictionary<CardVendor, ICardProcessor>
        {
            [CardVendor.Unknown] = new GenericProcessor("test")
        });
        var strategy = new CardTenderStrategy(processors);

        ScenarioExpect.Equal(PaymentKind.Card, strategy.Kind);
    }

    [Scenario("TryApply Authorizes And Captures")]
    [Fact]
    public void TryApply_Authorizes_And_Captures()
    {
        var processors = new CardProcessors(new Dictionary<CardVendor, ICardProcessor>
        {
            [CardVendor.Unknown] = new GenericProcessor("test"),
            [CardVendor.Visa] = new GenericProcessor("visa")
        });
        var strategy = new CardTenderStrategy(processors);
        var ctx = CreateContext(50m);
        var tender = new Tender(PaymentKind.Card, Vendor: CardVendor.Visa);

        var result = strategy.TryApply(ctx, tender);

        ScenarioExpect.Null(result); // success
        ScenarioExpect.Contains(ctx.Log, l => l.Contains("captured"));
    }

    [Scenario("TryApply Returns Null When NoDue")]
    [Fact]
    public void TryApply_Returns_Null_When_NoDue()
    {
        var processors = new CardProcessors(new Dictionary<CardVendor, ICardProcessor>
        {
            [CardVendor.Unknown] = new GenericProcessor("test")
        });
        var strategy = new CardTenderStrategy(processors);
        var ctx = CreateContext(10m);
        ctx.ApplyPayment(10m, "already paid"); // Fully paid
        var tender = new Tender(PaymentKind.Card, Vendor: CardVendor.Visa);

        var result = strategy.TryApply(ctx, tender);

        ScenarioExpect.Null(result);
    }

    private static TransactionContext CreateContext(decimal price)
    {
        var ctx = new TransactionContext
        {
            Customer = new Customer(LoyaltyId: null, AgeYears: 25),
            Items = [new LineItem("TEST", price, 1)]
        };
        ctx.RecomputeSubtotal();
        return ctx;
    }
}

public sealed class NoopCharityTrackerTests
{
    [Scenario("Track Does Not Throw")]
    [Fact]
    public void Track_Does_Not_Throw()
    {
        var tracker = new NoopCharityTracker();

        tracker.Track("TestCharity", Guid.NewGuid(), 0.50m, 10.50m);
    }
}

public sealed class CharityRoundUpRuleTests
{
    [Scenario("Reason Is Set")]
    [Fact]
    public void Reason_Is_Set()
    {
        var rule = new CharityRoundUpRule(new NoopCharityTracker());

        ScenarioExpect.Equal("charity round-up", rule.Reason);
    }

    [Scenario("ShouldApply Returns True When CharitySku Present")]
    [Fact]
    public void ShouldApply_Returns_True_When_CharitySku_Present()
    {
        var rule = new CharityRoundUpRule(new NoopCharityTracker());
        var ctx = new TransactionContext
        {
            Customer = new Customer(null, 25),
            Items = [new LineItem("CHARITY:UNICEF", 1m, 1)]
        };

        ScenarioExpect.True(rule.ShouldApply(ctx));
    }

    [Scenario("ShouldApply Returns False When No CharitySku")]
    [Fact]
    public void ShouldApply_Returns_False_When_No_CharitySku()
    {
        var rule = new CharityRoundUpRule(new NoopCharityTracker());
        var ctx = new TransactionContext
        {
            Customer = new Customer(null, 25),
            Items = [new LineItem("REGULAR", 10m, 1)]
        };

        ScenarioExpect.False(rule.ShouldApply(ctx));
    }

    [Scenario("ComputeDelta Rounds Up To Dollar")]
    [Fact]
    public void ComputeDelta_Rounds_Up_To_Dollar()
    {
        var rule = new CharityRoundUpRule(new NoopCharityTracker());
        var ctx = new TransactionContext
        {
            Customer = new Customer(null, 25),
            Items = [new LineItem("CHARITY:TEST", 10.25m, 1)]
        };
        ctx.RecomputeSubtotal();

        var delta = rule.ComputeDelta(ctx);

        ScenarioExpect.Equal(0.75m, delta); // 10.25 -> 11.00
    }
}

public sealed class NickelCashOnlyRuleTests
{
    [Scenario("Reason Is Set")]
    [Fact]
    public void Reason_Is_Set()
    {
        var rule = new NickelCashOnlyRule();

        ScenarioExpect.Equal("nickel (cash-only)", rule.Reason);
    }

    [Scenario("ShouldApply Returns True For Cash With NickelSku")]
    [Fact]
    public void ShouldApply_Returns_True_For_Cash_With_NickelSku()
    {
        var rule = new NickelCashOnlyRule();
        var ctx = new TransactionContext
        {
            Customer = new Customer(null, 25),
            Items = [new LineItem("ROUND:NICKEL", 10m, 1)],
            Tender = new Tender(PaymentKind.Cash)
        };

        ScenarioExpect.True(rule.ShouldApply(ctx));
    }

    [Scenario("ShouldApply Returns False For Card")]
    [Fact]
    public void ShouldApply_Returns_False_For_Card()
    {
        var rule = new NickelCashOnlyRule();
        var ctx = new TransactionContext
        {
            Customer = new Customer(null, 25),
            Items = [new LineItem("ROUND:NICKEL", 10m, 1)],
            Tender = new Tender(PaymentKind.Card)
        };

        ScenarioExpect.False(rule.ShouldApply(ctx));
    }

    [Scenario("ShouldApply Returns False Without NickelSku")]
    [Fact]
    public void ShouldApply_Returns_False_Without_NickelSku()
    {
        var rule = new NickelCashOnlyRule();
        var ctx = new TransactionContext
        {
            Customer = new Customer(null, 25),
            Items = [new LineItem("TEST", 10m, 1)],
            Tender = new Tender(PaymentKind.Cash)
        };

        ScenarioExpect.False(rule.ShouldApply(ctx));
    }

    [Scenario("ComputeDelta Rounds To Nearest Nickel")]
    [Fact]
    public void ComputeDelta_Rounds_To_Nearest_Nickel()
    {
        var rule = new NickelCashOnlyRule();
        var ctx = new TransactionContext
        {
            Customer = new Customer(null, 25),
            Items = [new LineItem("ROUND:NICKEL", 10.03m, 1)]
        };
        ctx.RecomputeSubtotal();

        var delta = rule.ComputeDelta(ctx);

        ScenarioExpect.Equal(0.02m, delta); // 10.03 -> 10.05
    }
}


[Collection("Culture")]
[Feature("Mediated Transaction pipeline – cash + loyalty + cigarettes")]
public sealed class MediatedTransactionPipelineDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static TransactionContext ArrangeCtx()
        => new()
        {
            Customer = new Customer(LoyaltyId: "LOYAL-123", AgeYears: 25),
            Tender = new Tender(PaymentKind.Cash, CashGiven: 50m),
            Items =
            [
                new LineItem("CIGS", 10.96m, Qty: 1, AgeRestricted: true, BundleKey: "CIGS"),
                new LineItem("CIGS", 10.97m, Qty: 1, AgeRestricted: true, BundleKey: "CIGS"),
            ]
        };

    [Scenario("Cash customer with loyalty buys 2 cigarettes -> $20 total, $30 change, success")]
    [Fact]
    public async Task Cash_Cigs_Loyalty_EndToEnd()
    {
        await Given("a cash customer buying 2 age-restricted items with loyalty", ArrangeCtx)
            .When("the mediated pipeline runs", MediatedTransactionPipelineDemo.Run)
            .Then("preauth passes", r => r.Ctx.Log.Contains("preauth: ok"))

            // state-based (culture-proof) checks
            .And("subtotal is 21.93", r => r.Ctx.Subtotal == 21.93m)
            .And("total discounts are 3.54", r => r.Ctx.DiscountTotal == 3.54m)
            .And("tax is 1.61", r => r.Ctx.TaxTotal == 1.61m)
            .And("grand total is 20.00", r => r.Ctx.GrandTotal == 20.00m)
            .And("cash change due is 30.00", r => r.Ctx.CashChange == 30.00m)

            // verify that each discount *rule* fired (without brittle currency text)
            .And("cash discount applied", r => r.Ctx.Log.Any(s => s.StartsWith("discount: cash 2% off")))
            .And("loyalty discount applied", r => r.Ctx.Log.Any(s => s.StartsWith("discount: loyalty ")))
            .And("bundle discount applied", r => r.Ctx.Log.Any(s => s.StartsWith("discount: bundle deal")))
            .And("tax logged", r => r.Ctx.Log.Any(s => s.StartsWith("tax:")))
            .And("total logged", r => r.Ctx.Log.Any(s => s.StartsWith("total: ")))
            .And("transaction succeeds with 'paid' code", r => r.Result is { Ok: true, Code: "paid" })
            .And("pipeline logs completion", r => r.Ctx.Log.Contains("done."))
            .AssertPassed();
    }
}