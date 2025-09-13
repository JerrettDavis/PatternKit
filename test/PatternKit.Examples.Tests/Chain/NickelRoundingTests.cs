using PatternKit.Examples.Chain;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Chain;

[Feature("Nickel rounding")]
public sealed class NickelRoundingTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Total 22.97 subtotal; first tender cash triggers 2% discount; then card pays remainder; no nickel rounding")]
    [Fact]
    public async Task Cash_Then_Card_No_Nickel_Round_With_Cash_Discount()
    {
        var ctx = new TransactionContext
        {
            Customer = new Customer(LoyaltyId: null, AgeYears: 30),
            Items = [new("SKU-1", 22.97m)], // subtotal 22.97
            Tenders =
            [
                new(PaymentKind.Cash, CashGiven: 20m), // first-tender cash => 2% discount applies
                new(PaymentKind.Card, AuthType: CardAuthType.Chip, Vendor: CardVendor.Visa)
            ]
        };

        await Given("a 22.97 subtotal basket with two tenders (20 cash, remainder card)", () => ctx)
            .When("the mediated pipeline runs", c => MediatedTransactionPipelineDemo.Run(c).Ctx)
            // totals
            .Then("preauth passes", c => c.Result is null || c.Result.Value.Ok)
            .And("subtotal is $22.97", c => c.Subtotal == 22.97m)
            .And("cash 2% discount applied ($0.46)", c =>
                c.DiscountTotal == 0.46m && c.Log.Any(l => l.Contains("cash 2% off")))
            .And("tax is $1.97 (8.75% on 22.51)", c => c.TaxTotal == 1.97m)
            .And("no nickel rounding applied", c => c.RoundingDelta == 0m)
            .And("grand total is $24.48", c => c.GrandTotal == 24.48m)
            // tendering
            .And("cash applied is $20.00", c => c.AmountPaid >= 20m && c.Log.Any(l => l.Contains("paid: cash $20.00")))
            .And("no cash change is due", c => c.CashChange is null or 0m)
            .And("card captured the remainder $4.48", c =>
                c.Log.Any(l => l.Contains("auth: captured via Visa $4.48")) ||
                c.Log.Any(l => l.Contains("VisaNet: captured $4.48")))
            .And("amount paid equals total", c => c.AmountPaid == 24.48m && c.RemainderDue == 0m)
            // result & rounding log
            .And("transaction succeeds", c => c.Result?.Ok == true)
            .And("rounding logged as none", c => c.Log.Any(l => l.Contains("round: none")))
            .AssertPassed();
    }

    [Scenario("Subtotal 22.97; ROUND:NICKEL present; cash-only; nickel rounds 24.48 -> 24.50")]
    [Fact]
    public async Task CashOnly_Nickel_Rounds_Up_To_Nearest_0_05()
    {
        // Math:
        //   Subtotal = 22.97
        //   Cash 2% discount = 0.46  => taxable = 22.51
        //   Tax 8.75% on 22.51 = 1.97
        //   Pre-round total = 22.97 - 0.46 + 1.97 = 24.48
        //   Nickel rounding (cash-only) => +0.02 -> 24.50
        var ctx = new TransactionContext
        {
            Customer = new Customer(LoyaltyId: null, AgeYears: 30),
            Items =
            [
                new("SKU-1", 22.97m),
                new("ROUND:NICKEL", 0m) // flag to enable cash-only nickel rounding
            ],
            Tenders =
            [
                new(PaymentKind.Cash, CashGiven: 24.50m) // exact cash after rounding
            ]
        };

        await Given("a 22.97 subtotal basket with ROUND:NICKEL and cash-only tender", () => ctx)
            .When("the mediated pipeline runs", c => MediatedTransactionPipelineDemo.Run(c).Ctx)
            // totals before rounding
            .Then("preauth passes", c => c.Result is null || c.Result.Value.Ok)
            .And("subtotal is $22.97", c => c.Subtotal == 22.97m)
            .And("cash 2% discount applied ($0.46)", c =>
                c.DiscountTotal == 0.46m && c.Log.Any(l => l.Contains("cash 2% off")))
            .And("tax is $1.97", c => c.TaxTotal == 1.97m)
            // rounding
            .And("nickel rounding applied +$0.02", c =>
                c.RoundingDelta == 0.02m &&
                c.Log.Any(l => l.Contains("round: nickel (cash-only) +$0.02")))
            .And("grand total is $24.50", c => c.GrandTotal == 24.50m)
            // tendering
            .And("cash applied is $24.50", c => c.AmountPaid == 24.50m && c.RemainderDue == 0m)
            .And("no cash change is due", c => c.CashChange is null or 0m)
            // result
            .And("transaction succeeds", c => c.Result?.Ok == true)
            .AssertPassed();
    }
}