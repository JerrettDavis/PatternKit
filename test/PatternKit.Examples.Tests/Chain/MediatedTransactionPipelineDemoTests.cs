using PatternKit.Examples.Chain;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Chain;

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