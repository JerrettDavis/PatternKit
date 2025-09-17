# Pricing Calculator — async sources, loyalty stacking, bundles, coupons, taxes, rounding

This demo shows how to compose a real-world pricing pipeline using PatternKit’s fluent building blocks.
It resolves unit prices from multiple asynchronous sources, applies loyalty and payment discounts with stacking rules, supports bundles and coupons, computes taxes (with per-region rates and per-SKU tariffs), and finally applies rounding rules (charity round-up and cash nickel rounding) that adjust a target SKU’s price.

---

## What it does

- Asynchronous price resolution from multiple sources
  - Database, API, filesystem (demo stubs) selected by SKU tags
  - Per-input source routing via simple predicate → provider pairs
- Loyalty programs with stack/exclusive semantics
  - Rules can restrict by category/tags; exclusive rules block later stacking on the same line
- Payment-type discounts
  - E.g., cash 2%, store credit card 5%, store gift card 3%
- Bundles and coupons
  - Bundle: unit-off when total bundle quantity meets threshold
  - Coupons: currency or percentage per eligible unit
- Taxes, VAT, tariffs
  - Per-region rates; per-SKU tariff via a `tariff:0.xx` tag; category exemptions
- Rounding rules (first-match wins)
  - Charity round-up to next dollar (post-tax), applied by increasing a charity SKU’s price
  - Nickel rounding for cash payments (round to nearest $0.05) targeting a “nickel” SKU
- Location variance
  - Region drives tax rates, letting totals differ across locales

All composition happens once; per-request pricing runs as tight loops over immutable arrays of delegates.

---

## Quick look

```csharp
using PatternKit.Examples.Pricing;

// Build default pipeline and sample context
var d = PricingDemo.BuildDefault();
var ctx = new PricingContext
{
    Location = new("US-NE", Country: "US", State: "NE"),
    Payment = PaymentKind.Cash,
    Items =
    [
        new LineItem { Sku = new("SKU-APPLE", "Apple", Category: "Grocery", Tags: ["price:db"]), Qty = 2 },
        new LineItem { Sku = new("SKU-CHARITY", "Charity", Tags: ["price:db", "charity", "no-subtotal"]), Qty = 1 },
        new LineItem { Sku = new("SKU-NICKEL", "NickelRounder", BundleKey: "BNDL", Tags: ["price:db", "round:nickel"]), Qty = 2 },
    ]
};
ctx.Loyalty.Add(new("LOY-5"));
ctx.Coupons.Add(new("CASHOFF1", 1.00m));

var result = await d.Pipeline.RunAsync(ctx);
Console.WriteLine(string.Join("\n", result.Log));
// price:... ; loyalty:... ; paydisc:... ; bundle:... ; coupon:... ; tax:... ; round:...
```

---

## How it’s composed

- Domain: `Domain.cs` (Location, Sku, LineItem, PricingContext, PricingResult)
- Sources & routing: `Sources.cs`
  - `IPricingSource` (Db, Api, File) + `SourceRouter` (predicate → provider) with a default
- Pipeline & steps: `Pipeline.cs`
  - `PricingPipelineBuilder` with `.Add(...)` steps
  - Turnkey adders: `.AddPriceResolution`, `.AddLoyalty`, `.AddPaymentDiscounts`, `.AddBundleDiscount`, `.AddCoupons`, `.AddTaxes`, `.AddRounding`
- Rules & policies
  - Loyalty: `ILoyaltyRule`, `PercentLoyaltyRule` (with `CanStack`)
  - Tax: `ITaxPolicy`, `RegionCategoryTaxPolicy` (region rate + category exemptions + tariff tag)
  - Rounding: `IRoundingRule`, `CharityRoundUpRule`, `NickelCashOnlyRule`
- Demo builder: `Demo.cs` (`PricingDemo.BuildDefault`) wires everything together

All artifacts live under `src/PatternKit.Examples/Pricing/`.

---

## Tests (TinyBDD)

See `test/PatternKit.Examples.Tests/Pricing/PricingDemoTests.cs` for scenarios:

- Source routing (db/api/file)
- Loyalty stacking vs exclusive rules
- Payment-type percentage discounts
- Coupons per eligible unit
- Bundle threshold discounts
- Taxes with region, exemptions, and tariffs
- Rounding priority (charity first) and nickel fallback (cash)
- Location variance (different region → different tax)

The assertions read like specs and validate both logs and totals behavior.

---

## Extending it

- Add a new price source: implement `IPricingSource` and register a router predicate
- Add a loyalty program: implement `ILoyaltyRule` (set `CanStack`) and add to `.AddLoyalty(...)`
- Add a bundle/coupon shape: copy the pattern in `.AddBundleDiscount` / `.AddCoupons`
- Add/modify taxes: implement `ITaxPolicy`
- Add rounding: implement `IRoundingRule` and place it before/after others to control priority

Prefer static lambdas and `in` parameters for handlers to keep hot paths allocation-free.

