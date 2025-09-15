# Config-driven transaction pipeline (DI + fluent chains)

> **Goal**
> Build a checkout pipeline where **what runs** and **in what order** comes from configuration, while the execution remains allocation-lean and testable.

This demo layers a small configuration model over the same primitives used in the mediated pipeline:

* **Action chains** for branchless *discounts → tax* and *rounding*
* **Tender handling** via a first-match router (see the mediated pipeline doc)
* **DI registration** that composes a single immutable [xref\:PatternKit.Examples.Chain.TransactionPipeline](xref:PatternKit.Examples.Chain.TransactionPipeline) at startup

---

## Quick start

1. **Add configuration** (order matters):

```json
// appsettings.json
{
  "Payment": {
    "Pipeline": {
      "DiscountRules": [ "discount:cash-2pc", "discount:loyalty-5pc", "discount:bundle-1off" ],
      "Rounding": [ "round:charity", "round:nickel-cash-only" ],
      "TenderOrder": [ "tender:cash", "tender:card" ] // informational
    }
  }
}
```

2. **Register the pipeline** in DI:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Chain.ConfigDriven;

var services = new ServiceCollection();
services.AddPaymentPipeline(configuration);              // builds a TransactionPipeline from config
var provider = services.BuildServiceProvider();

var pipe = provider.GetRequiredService<ConfigDrivenPipelineDemo.PaymentPipeline>();
var (result, ctx) = pipe.Run(new TransactionContext {
    Customer = new Customer(LoyaltyId: "LOYAL-123", AgeYears: 25),
    Items = [ new LineItem("SKU-1", 22.97m) ],
    Tenders = [ new Tender(PaymentKind.Cash, CashGiven: 20m),
                new Tender(PaymentKind.Card, CardAuthType.Contactless, CardVendor.Visa) ]
});
```

3. **Done** — the runtime pipeline is immutable and safe to reuse concurrently.

---

## What’s in the box

### Configuration model

[xref\:PatternKit.Examples.Chain.ConfigDriven.PipelineOptions](xref:PatternKit.Examples.Chain.ConfigDriven.PipelineOptions) drives ordering:

* `DiscountRules`: keys of discount rules to apply **in order**
* `Rounding`: keys of rounding strategies to apply **in order**
* `TenderOrder`: optional, informational (e.g., control UI ordering)

Unknown keys are **ignored** (we map by key and skip missing entries).

### Strategies provided

**Discount rules** (keys):

* `discount:cash-2pc` → [xref\:PatternKit.Examples.Chain.ConfigDriven.Cash2Pct](xref:PatternKit.Examples.Chain.ConfigDriven.Cash2Pct)
  First tender is cash → 2% off `Subtotal`
* `discount:loyalty-5pc` → [xref\:PatternKit.Examples.Chain.ConfigDriven.Loyalty5Pct](xref:PatternKit.Examples.Chain.ConfigDriven.Loyalty5Pct)
  Loyalty present → 5% off `Subtotal`
* `discount:bundle-1off` → [xref\:PatternKit.Examples.Chain.ConfigDriven.Bundle1OffEach](xref:PatternKit.Examples.Chain.ConfigDriven.Bundle1OffEach)
  Any `BundleKey` with total `Qty ≥ 2` → \$1 off per item in those bundles

**Rounding** (keys):

* `round:charity` → [xref\:PatternKit.Examples.Chain.ConfigDriven.CharityRoundUp](xref:PatternKit.Examples.Chain.ConfigDriven.CharityRoundUp)
  If any `CHARITY:*` SKU is present → round up to next dollar
* `round:nickel-cash-only` → [xref\:PatternKit.Examples.Chain.ConfigDriven.NickelCashOnly](xref:PatternKit.Examples.Chain.ConfigDriven.NickelCashOnly)
  Cash-only transactions → round to nearest \$0.05 (logs “skipped (not cash-only)” otherwise)

**Tender handlers** (DI-registered):

* [xref\:PatternKit.Examples.Chain.ConfigDriven.CashTender](xref:PatternKit.Examples.Chain.ConfigDriven.CashTender) (`tender:cash`)
* [xref\:PatternKit.Examples.Chain.ConfigDriven.CardTender](xref:PatternKit.Examples.Chain.ConfigDriven.CardTender) (`tender:card`)

> The router itself is assembled by the mediated pipeline pieces; we simply **supply handlers via DI** and the builder wires them into the tender stage.

---

## How it composes

### Discounts & tax (config-driven)

[xref\:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineBuilderExtensions.AddConfigDrivenDiscountsAndTax\*](xref:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineBuilderExtensions.AddConfigDrivenDiscountsAndTax*):

* Recomputes `Subtotal`
* Iterates `opts.Value.DiscountRules` and applies each rule that exists in the DI map
* Computes **tax** at **8.75%** of `(Subtotal − DiscountTotal)` and logs `pre-round total`

```csharp
b.AddConfigDrivenDiscountsAndTax(opts, discountRules);
```

### Rounding (config-driven)

[xref\:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineBuilderExtensions.AddConfigDrivenRounding\*](xref:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineBuilderExtensions.AddConfigDrivenRounding*):

* Iterates `opts.Value.Rounding` and calls each strategy in order
* Each strategy decides to apply or log “skipped”
* Logs final `total`

```csharp
b.AddConfigDrivenRounding(opts, rounding);
```

### DI registration

[xref\:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.AddPaymentPipeline\*](xref:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.AddPaymentPipeline*):

* Binds `Payment:Pipeline` to [xref\:PatternKit.Examples.Chain.ConfigDriven.PipelineOptions](xref:PatternKit.Examples.Chain.ConfigDriven.PipelineOptions)
* Registers:

    * Infra: [xref\:PatternKit.Examples.Chain.IDeviceBus](xref:PatternKit.Examples.Chain.IDeviceBus), [xref\:PatternKit.Examples.Chain.CardProcessors](xref:PatternKit.Examples.Chain.CardProcessors)
    * Discounts: `Cash2Pct`, `Loyalty5Pct`, `Bundle1OffEach`
    * Rounding: `CharityRoundUp`, `NickelCashOnly`
    * Tenders: `CashTender`, `CardTender`
* Builds a shared [xref\:PatternKit.Examples.Chain.TransactionPipeline](xref:PatternKit.Examples.Chain.TransactionPipeline):

```csharp
TransactionPipelineBuilder.New()
    .WithDeviceBus(devices)
    .AddPreauth()
    .AddConfigDrivenDiscountsAndTax(opts, discountRules)
    .AddConfigDrivenRounding(opts, rounding)
    .WithTenderHandlers(tenderHandlers)
    .AddTenderHandling()
    .AddFinalize()
    .Build();
```

Consumers receive a thin wrapper: [xref\:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.PaymentPipeline](xref:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.PaymentPipeline) with `Run(ctx)`.

---

## Example scenarios (from tests)

### Mixed tender (cash then card) — **no** nickel rounding

* Config: `Rounding = ["round:nickel-cash-only"]`
* Items: `Subtotal 22.97 → Tax 2.01 → Pre-round 24.98`
* Tenders: `$20 cash`, then `Visa` pays the remainder

**Outcome**

* Rounding skipped (not cash-only)
* Card captures `$4.98`
* Result: `paid`

See: `TransactionPipelineDemoTests.MixedTender_NoNickelRounding`.

### Cash-only nickel rounding **up** to `$25.00`

* Config: `Rounding = ["round:nickel-cash-only"]`
* Pre-round total `24.98`
* Rounding adds `+$0.02`
* Single cash tender `$25.00` → paid

See: `TransactionPipelineDemoTests.CashOnly_NickelRounding_Up`.

### Charity round-up

* Config: `Rounding = ["round:charity"]`
* Presence of `CHARITY:RedCross` SKU causes `+$0.02` to next whole dollar
* Paid by card

See: `TransactionPipelineDemoTests.Charity_RoundUp_Works`.

### Preauth block (age)

* Age-restricted item + underage customer → `TxResult.Fail("age", ...)`
* Pipeline stops early

See: `TransactionPipelineDemoTests.Preauth_AgeBlock`.

---

## Extending with your own rules/strategies/handlers

1. **Implement** the interface and choose a unique key:

```csharp
public sealed class Employee10Pct : IDiscountRule
{
    public string Key => "discount:employee-10pc";
    public void Apply(TransactionContext ctx)
    {
        if (ctx.Customer.LoyaltyId == "EMP")
            ctx.AddDiscount(Math.Round(ctx.Subtotal * 0.10m, 2), "employee 10%");
    }
}
```

2. **Register** it:

```csharp
services.AddSingleton<IDiscountRule, Employee10Pct>();
```

3. **Enable** it in config (order is important):

```json
"Payment": { "Pipeline": { "DiscountRules": [
  "discount:employee-10pc", "discount:bundle-1off"
]}}
```

The same pattern holds for `IRoundingStrategy` and `ITenderHandler`.

---

## FAQ & tips

* **What happens if a key is listed but not registered?**
  It’s skipped; we only apply rules found in the DI map.

* **Where’s the tax rate?**
  Inside `AddConfigDrivenDiscountsAndTax` we compute tax at **8.75%**. Swap this with your own calculator if needed.

* **Thread safety?**
  The composed [xref\:PatternKit.Examples.Chain.TransactionPipeline](xref:PatternKit.Examples.Chain.TransactionPipeline) is immutable and safe for concurrent use. Builders are not thread-safe.

* **Observability**
  Every rule/strategy logs its work to `ctx.Log` using concise, human-readable entries suitable for unit tests and diagnostics.

* **Performance**

    * All composition happens **once** at startup.
    * Execution uses arrays and `static` delegates where possible to minimize allocations.
    * The config-driven action chains still short-circuit *inside* each component when appropriate.

---

## Reference

* Composition

    * [xref\:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineBuilderExtensions](xref:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineBuilderExtensions)
    * [xref\:PatternKit.Examples.Chain.TransactionPipelineBuilder](xref:PatternKit.Examples.Chain.TransactionPipelineBuilder)
    * [xref\:PatternKit.Examples.Chain.TransactionPipeline](xref:PatternKit.Examples.Chain.TransactionPipeline)
* Config & DI

    * [xref\:PatternKit.Examples.Chain.ConfigDriven.PipelineOptions](xref:PatternKit.Examples.Chain.ConfigDriven.PipelineOptions)
    * [xref\:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.AddPaymentPipeline\*](xref:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.AddPaymentPipeline*)
    * [xref\:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.PaymentPipeline](xref:PatternKit.Examples.Chain.ConfigDriven.ConfigDrivenPipelineDemo.PaymentPipeline)
* Strategies

    * Discounts: [xref\:PatternKit.Examples.Chain.ConfigDriven.Cash2Pct](xref:PatternKit.Examples.Chain.ConfigDriven.Cash2Pct), [xref\:PatternKit.Examples.Chain.ConfigDriven.Loyalty5Pct](xref:PatternKit.Examples.Chain.ConfigDriven.Loyalty5Pct), [xref\:PatternKit.Examples.Chain.ConfigDriven.Bundle1OffEach](xref:PatternKit.Examples.Chain.ConfigDriven.Bundle1OffEach)
    * Rounding: [xref\:PatternKit.Examples.Chain.ConfigDriven.CharityRoundUp](xref:PatternKit.Examples.Chain.ConfigDriven.CharityRoundUp), [xref\:PatternKit.Examples.Chain.ConfigDriven.NickelCashOnly](xref:PatternKit.Examples.Chain.ConfigDriven.NickelCashOnly)
    * Tenders: [xref\:PatternKit.Examples.Chain.ConfigDriven.CashTender](xref:PatternKit.Examples.Chain.ConfigDriven.CashTender), [xref\:PatternKit.Examples.Chain.ConfigDriven.CardTender](xref:PatternKit.Examples.Chain.ConfigDriven.CardTender)
* Domain

    * [xref\:PatternKit.Examples.Chain.TransactionContext](xref:PatternKit.Examples.Chain.TransactionContext), [xref\:PatternKit.Examples.Chain.TxResult](xref:PatternKit.Examples.Chain.TxResult),
      [xref\:PatternKit.Examples.Chain.Tender](xref:PatternKit.Examples.Chain.Tender), [xref\:PatternKit.Examples.Chain.LineItem](xref:PatternKit.Examples.Chain.LineItem), [xref\:PatternKit.Examples.Chain.Customer](xref:PatternKit.Examples.Chain.Customer)
