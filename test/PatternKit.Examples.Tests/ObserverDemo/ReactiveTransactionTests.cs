using PatternKit.Examples.ObserverDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ObserverDemo;

[Feature("Reactive Transaction (dynamic totals/discounts/tax) using Observer")]
public sealed class ReactiveTransactionTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Ctx(ReactiveTransaction Tx, List<decimal> Totals)
    {
        public Ctx() : this(new ReactiveTransaction(), new List<decimal>()) { }
    }

    private static Ctx Build() => new();

    private static Ctx SubscribeTotals(Ctx c)
    {
        c.Tx.Total.Subscribe((_, @new) => c.Totals.Add(@new));
        return c;
    }

    [Scenario("Totals and badges recompute as inputs change")]
    [Fact]
    public async Task Totals_Recompute()
    {
        await Given("a fresh transaction", Build)
            .When("subscribing to totals", SubscribeTotals)
            .When("adding line items", c =>
            {
                c.Tx.AddItem(new LineItem("WIDGET", 2, 19.99m));
                c.Tx.AddItem(new LineItem("GADGET", 1, 49.99m, DiscountPct: 0.10m));
                return c;
            })
            .When("setting promotions", c => { c.Tx.SetTier(LoyaltyTier.Gold); c.Tx.SetPayment(PaymentKind.StoreCard); return c; })
            .When("setting tax rate", c => { c.Tx.SetTaxRate(0.07m); return c; })
            .Then("subtotal is 89.97", c => c.Tx.Subtotal.Value == 89.97m)
            .And("line item discounts sum to 4.999", c => c.Tx.LineItemDiscounts.Value == 4.999m)
            .And("loyalty discount is 5.95", c => c.Tx.LoyaltyDiscount.Value == 5.95m)
            .And("payment discount is 3.95", c => c.Tx.PaymentDiscount.Value == 3.95m)
            .And("tax is 5.95", c => c.Tx.Tax.Value == 5.95m)
            .And("total is 81.02", c => c.Tx.Total.Value == 81.02m)
            .And("checkout is enabled", c => c.Tx.CanCheckout.Value)
            .And("badge shows savings", c => (c.Tx.DiscountBadge.Value ?? string.Empty).Contains("You saved"))
            .When("change tax rate to 10%", c => { c.Tx.SetTaxRate(0.10m); return c; })
            .Then("tax updates", c => c.Tx.Tax.Value == Math.Round((39.98m + (49.99m - 4.999m)) * 0.10m, 2))
            .AssertPassed();
    }
}

