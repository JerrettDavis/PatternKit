using PatternKit.Examples.Chain;
using PatternKit.Examples.Chain.ConfigDriven;
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

public sealed class MediatedTransactionPipelineCoverageTests
{
    [Scenario("TransactionPipelineBuilder FluentHooks Cover CustomStagesRulesAndCouponDiscounts")]
    [Fact]
    public void TransactionPipelineBuilder_FluentHooks_Cover_CustomStagesRulesAndCouponDiscounts()
    {
        var couponContext = new TransactionContext
        {
            Customer = new Customer(null, 30),
            Items =
            [
                new LineItem("MFG", 10m, Qty: 2, ManufacturerCoupon: 1m),
                new LineItem("HOUSE", 5m, Qty: 1, InHouseCoupon: 0.50m),
            ]
        };
        var couponPipeline = TransactionPipelineBuilder.New()
            .WithRoundingRules()
            .AddDiscountsAndTax()
            .Build();

        var couponResult = couponPipeline.Run(couponContext);

        ScenarioExpect.True(couponResult.Result.Ok);
        ScenarioExpect.Contains(couponResult.Ctx.Log, entry => entry.StartsWith("discount: manufacturer coupons", StringComparison.Ordinal));
        ScenarioExpect.Contains(couponResult.Ctx.Log, entry => entry.StartsWith("discount: in-house coupons", StringComparison.Ordinal));

        var customContext = new TransactionContext
        {
            Customer = new Customer(null, 30),
            Items = [new LineItem("STOP", 1m)]
        };
        var customPipeline = TransactionPipelineBuilder.New()
            .AddStage(ctx =>
            {
                ctx.Result = TxResult.Fail("custom", "custom stop");
                return false;
            })
            .Build();

        var customResult = customPipeline.Run(customContext);

        ScenarioExpect.False(customResult.Result.Ok);
        ScenarioExpect.Equal("custom", customResult.Result.Code);
    }

    [Scenario("TransactionPipelineBuilder Preauth Blocks EmptyBaskets")]
    [Fact]
    public void TransactionPipelineBuilder_Preauth_Blocks_EmptyBaskets()
    {
        var context = new TransactionContext
        {
            Customer = new Customer(null, 30),
            Items = []
        };
        var pipeline = TransactionPipelineBuilder.New()
            .AddPreauth()
            .Build();

        var result = pipeline.Run(context);

        ScenarioExpect.False(result.Result.Ok);
        ScenarioExpect.Equal("empty", result.Result.Code);
        ScenarioExpect.True(result.Ctx.Log.Contains("preauth: empty basket"));
    }

    [Scenario("CardTenderHandlers Surface AuthorizationAndCaptureFailures")]
    [Fact]
    public void CardTenderHandlers_Surface_AuthorizationAndCaptureFailures()
    {
        var authProcessor = new ConfigurableProcessor(
            TxResult.Fail("declined", "declined"),
            TxResult.Success("capture"));
        var captureProcessor = new ConfigurableProcessor(
            TxResult.Success("auth"),
            TxResult.Fail("capture-failed", "capture failed"));
        var authContext = CreateContext(20m);
        var captureContext = CreateContext(20m);

        var configAuthTender = new CardTender(new CardProcessors(new()
        {
            [CardVendor.Unknown] = authProcessor
        }));
        var configCaptureTender = new CardTender(new CardProcessors(new()
        {
            [CardVendor.Unknown] = captureProcessor
        }));
        var strategyAuthTender = new CardTenderStrategy(new CardProcessors(new()
        {
            [CardVendor.Unknown] = authProcessor
        }));
        var strategyCaptureTender = new CardTenderStrategy(new CardProcessors(new()
        {
            [CardVendor.Unknown] = captureProcessor
        }));

        var tender = new Tender(PaymentKind.Card, Vendor: CardVendor.Unknown);
        var configAuthResult = configAuthTender.Handle(authContext, tender);
        var configCaptureResult = configCaptureTender.Handle(captureContext, tender);
        var strategyAuthResult = strategyAuthTender.TryApply(CreateContext(20m), tender);
        var strategyCaptureResult = strategyCaptureTender.TryApply(CreateContext(20m), tender);

        ScenarioExpect.Equal("tender:card", configAuthTender.Key);
        ScenarioExpect.False(configAuthResult.Ok);
        ScenarioExpect.Equal("declined", configAuthResult.Code);
        ScenarioExpect.True(authContext.Log.Contains("auth: declined (declined)"));
        ScenarioExpect.False(configCaptureResult.Ok);
        ScenarioExpect.Equal("capture-failed", configCaptureResult.Code);
        ScenarioExpect.True(captureContext.Log.Contains("auth: capture failed"));
        ScenarioExpect.NotNull(strategyAuthResult);
        ScenarioExpect.Equal("declined", strategyAuthResult!.Value.Code);
        ScenarioExpect.NotNull(strategyCaptureResult);
        ScenarioExpect.Equal("capture-failed", strategyCaptureResult!.Value.Code);
    }

    [Scenario("CharityRoundUpRule NotifiesTrackerWhenApplied")]
    [Fact]
    public void CharityRoundUpRule_NotifiesTracker_WhenApplied()
    {
        var tracker = new RecordingCharityTracker();
        var rule = new CharityRoundUpRule(tracker);
        var context = new TransactionContext
        {
            Customer = new Customer(null, 30),
            Items = [new LineItem("CHARITY:KidsFund", 10.25m)]
        };
        context.RecomputeSubtotal();

        RoundingPipeline.Apply(context, [rule]);

        ScenarioExpect.Equal("KidsFund", tracker.Charity);
        ScenarioExpect.Equal(0.75m, tracker.Delta);
        ScenarioExpect.Contains(context.Log, entry => entry.StartsWith("charity: KidsFund notified", StringComparison.Ordinal));
    }

    private static TransactionContext CreateContext(decimal price)
    {
        var context = new TransactionContext
        {
            Customer = new Customer(null, 30),
            Items = [new LineItem("CARD", price)]
        };
        context.RecomputeSubtotal();
        return context;
    }

    private sealed class ConfigurableProcessor(TxResult authorization, TxResult capture) : ICardProcessor
    {
        public TxResult Authorize(TransactionContext ctx) => authorization;

        public TxResult Capture(TransactionContext ctx) => capture;
    }

    private sealed class RecordingCharityTracker : ICharityTracker
    {
        public string? Charity { get; private set; }
        public decimal Delta { get; private set; }

        public void Track(string charity, Guid transactionId, decimal delta, decimal newTotal)
        {
            Charity = charity;
            Delta = delta;
        }
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
