# Mediated transaction pipeline (demo)

> **TL;DR**
> This sample shows how to build a production-grade checkout pipeline by **composing small, testable stages**.
> We combine:
>
> * **Action chains** for branchless pre-auth, discounts, and tax
> * A **BranchBuilder-powered router** for tender handling
> * A tiny **rounding rule engine** (first-match-wins)
> * A lightweight **pipeline runner** with **short-circuit** semantics

Everything runs on in-memory types so you can exercise it entirely from unit tests.

---

## What we’re building

The pipeline transforms a mutable [xref\:PatternKit.Examples.Chain.TransactionContext](xref:PatternKit.Examples.Chain.TransactionContext) through a series of stages and returns a terminal [xref\:PatternKit.Examples.Chain.TxResult](xref:PatternKit.Examples.Chain.TxResult):

```
[Preauth] → [Discounts & Tax] → [Rounding] → [Tender Handling] → [Finalize]
                 |                    |              |                 |
                 v                    v              v                 v
             updates ctx        updates ctx     updates ctx      sets terminal
             (no return)        (no return)     (no return)      TxResult + beeps
```

* Each stage has signature `bool Stage(TransactionContext ctx)` — **true** = continue, **false** = stop.
* Stages mutate `ctx` (totals, logs, tender progress) and may set `ctx.Result` to a terminal value.

---

## Core concepts

### The `Stage` delegate and the pipeline runner

* `Stage` is just: **mutate ctx, decide continue/stop**.
* [xref\:PatternKit.Examples.Chain.TransactionPipeline](xref:PatternKit.Examples.Chain.TransactionPipeline) runs the ordered stages and enforces a terminal result:

    * If any stage returns **false**: stop and return the current `ctx.Result`.
    * If no stage stopped and `ctx.Result` is still null: force success `("paid", "paid in full")`.

```csharp
var (result, finalCtx) = new TransactionPipeline(stages).Run(ctx);
```

### Adapting action chains to stages

We use [xref\:PatternKit.Behavioral.Chain.ActionChain%601](xref:PatternKit.Behavioral.Chain.ActionChain%601) for **branchless** logic and adapt it to a `Stage` via `ChainStage.From(chain)`.
If an action chain sets a failing `ctx.Result`, the adapted stage returns **false** to short-circuit the pipeline.

---

## Building the pipeline

Use [xref\:PatternKit.Examples.Chain.TransactionPipelineBuilder](xref:PatternKit.Examples.Chain.TransactionPipelineBuilder) to declaratively assemble stages:

```csharp
var pipeline = TransactionPipelineBuilder.New()
    .AddPreauth()          // age restriction, empty basket
    .AddDiscountsAndTax()  // cash 2%, loyalty 5%, coupons, bundle, then tax
    .AddRounding()         // first matching rounding rule
    .AddTenderHandling()   // cash + card handlers via BranchBuilder router
    .AddFinalize()         // terminal result + device beep
    .Build();

var (result, ctx) = pipeline.Run(transactionCtx);
```

### 1) Pre-authorization (no `if/else`)

Implemented as an **ActionChain** that stops on the first failing rule:

* Age restricted items require `Customer.AgeYears ≥ 21`
* Basket must not be empty

On failure: sets `ctx.Result = TxResult.Fail(...)`, logs the reason, **stops** the pipeline.

### 2) Discounts & tax (no `if/else`)

Also an **ActionChain**:

* Cash-first: **2%** off
* Loyalty present: **5%** off
* Manufacturer coupons (sum per item × qty)
* In-house coupons
* Bundle deal: items sharing a `BundleKey` with total `Qty ≥ 2` → `$1 off` per unit in the bundle
* Then compute tax at **8.75%** of `(Subtotal - DiscountTotal)`

All values are rounded to two decimals at the point of application.

### 3) Rounding rules (first-match-wins)

[xref\:PatternKit.Examples.Chain.RoundingPipeline](xref:PatternKit.Examples.Chain.RoundingPipeline) evaluates `IRoundingRule` implementations **in order** and applies the first that matches:

* [xref\:PatternKit.Examples.Chain.CharityRoundUpRule](xref:PatternKit.Examples.Chain.CharityRoundUpRule) — if any `Sku` starts with `CHARITY:`, round up to next dollar and notify [xref\:PatternKit.Examples.Chain.ICharityTracker](xref:PatternKit.Examples.Chain.ICharityTracker)
* [xref\:PatternKit.Examples.Chain.NickelCashOnlyRule](xref:PatternKit.Examples.Chain.NickelCashOnlyRule) — if *all tenders are cash* and a `ROUND:NICKEL` SKU is present, round to nearest \$0.05

Every rule logs the delta and the new total when applied.

You can override rules with:

```csharp
TransactionPipelineBuilder.New()
    .WithRoundingRules(new MyRule(), new NickelCashOnlyRule())
    ...
```

### 4) Tender handling (BranchBuilder router)

Handlers live behind a **zero-`if` router** built by [xref\:PatternKit.Examples.Chain.TenderRouterFactory](xref:PatternKit.Examples.Chain.TenderRouterFactory):

* Each handler implements `ITenderHandler` (from `ConfigDriven` sample) with:

    * `CanHandle(ctx, tender) : bool`
    * `Handle(ctx, tender) : TxResult`
* The router is composed with [xref\:PatternKit.Creational.Builder.BranchBuilder\`2](xref:PatternKit.Creational.Builder.BranchBuilder`2):

    * Evaluate handlers **in order**
    * First predicate that returns **true** runs its handler
    * If none match, return `TxResult.Fail("route", ...)`

By default (if you don’t call `WithTenderHandlers(...)`) we register:

* `CashTender` — opens drawer, applies cash, calculates change
* `CardTender` — resolves a processor by vendor, `Authorize` then `Capture`, applies payment

You can supply your own:

```csharp
builder.WithTenderHandlers(new MyGiftCardHandler(), new CashTender(devices));
```

### 5) Finalization

If no stage failed:

* If `ctx.RemainderDue > 0`: `Fail("insufficient", "...")`
* Else: `Success("paid", "paid in full")` and `devices.Beep("printer", 2)`

Always logs `"done."`.

---

## End-to-end scenario (from tests)

**Feature:** Cash + loyalty + two cigarettes
**Given:** customer age 25, loyalty `"LOYAL-123"`, tenders: `$50` cash, items:

* `CIGS` \$10.96 × 1 (age-restricted, bundle `CIGS`)
* `CIGS` \$10.97 × 1 (age-restricted, bundle `CIGS`)

**Then** the pipeline computes:

* Subtotal = **21.93**
* Discounts:

    * Cash 2% = **0.44**
    * Loyalty 5% = **1.10**
    * Bundle deal = **2.00**
    * **Total discounts = 3.54**
* Tax (8.75% of 21.93 − 3.54 = 18.39) = **1.61**
* Grand total = **20.00**
* Cash given 50 → Change = **30.00**
* Terminal result = **Ok=true, Code="paid"**
* Log contains: `"preauth: ok"`, individual discount entries, `"tax:"`, `"total:"`, and `"done."`

> This is codified in `MediatedTransactionPipelineDemoTests` using TinyBDD.

---

## Extensibility points

* **Devices:** `.WithDeviceBus(IDeviceBus)` (beeper, cash drawer, etc.)
* **Tender handlers:** `.WithTenderHandlers(params ITenderHandler[])`
* **Rounding rules:** `.WithRoundingRules(params IRoundingRule[])`
* **Arbitrary stages:** `.AddStage(Stage)` or `.AddStage(ActionChain<TransactionContext>)`

Because stages are just delegates, you can encapsulate feature slices (fraud checks, gift cards, store credit, EBT, etc.) as separate assemblies and drop them into the builder.

---

## Routing without `if/else`

The tender router is built once and runs hot:

```csharp
public static TenderRouter Build(IEnumerable<ITenderHandler> handlers)
{
    var bb = BranchBuilder<TenderPred, TenderStep>.Create();

    foreach (var h in handlers)
        bb.Add(
            (in c, in t) => h.CanHandle(c, t),   // predicate
            (c,    in t) => h.Handle(c, t));    // handler

    return bb.Build<TenderRouter>(
        fallbackDefault: static (ctx, in t)
            => TxResult.Fail("route", $"no handler for {t.Kind}"),
        projector: static (preds, steps, _, def) => (ctx, in t) =>
        {
            for (var i = 0; i < preds.Length; i++)
                if (preds[i](in ctx, in t)) return steps[i](ctx, in t);
            return def(ctx, in t);
        });
}
```

* **Zero allocations** per call (no lambdas captured, all signatures are exact).
* **Short-circuit** on first match.

---

## Performance notes

* Most delegates are `static` and use **`in` parameters** to avoid copies.
* Arithmetic is rounded at the **moment of application** to keep totals stable.
* The pipeline, chains, and router are **immutable** after `Build()`; safe for concurrent use.

---

## Running the demo programmatically

If you just want the sensible defaults:

```csharp
var ctx = new TransactionContext
{
    Customer = new Customer(LoyaltyId: "LOYAL-123", AgeYears: 25),
    Tender   = new Tender(PaymentKind.Cash, CashGiven: 50m),
    Items =
    [
        new LineItem("CIGS", 10.96m, Qty: 1, AgeRestricted: true, BundleKey: "CIGS"),
        new LineItem("CIGS", 10.97m, Qty: 1, AgeRestricted: true, BundleKey: "CIGS"),
    ]
};

var (result, finalCtx) = MediatedTransactionPipelineDemo.Run(ctx);
// result.Ok == true, result.Code == "paid"
```

---

## Troubleshooting

* **Pipeline stops early**
  Check `ctx.Result` and `ctx.Log`. Any stage can set a failing result and return `false` to short-circuit.

* **Totals don’t add up**
  Ensure you call `RecomputeSubtotal()` before computing discounts and tax (the included chain does this first).

* **Rounding not applied**
  Confirm rule order and `ShouldApply` conditions. Only the **first** matching rule runs.

* **Tender not handled**
  Verify handler order and `CanHandle` predicates. The router is **first-match-wins**; add a fallback handler or rely on the built-in `"route"` failure.

---

## API reference (selected)

* Pipeline

    * [xref\:PatternKit.Examples.Chain.TransactionPipeline](xref:PatternKit.Examples.Chain.TransactionPipeline)
    * [xref\:PatternKit.Examples.Chain.TransactionPipelineBuilder](xref:PatternKit.Examples.Chain.TransactionPipelineBuilder)
    * [xref\:PatternKit.Examples.Chain.MediatedTransactionPipelineDemo](xref:PatternKit.Examples.Chain.MediatedTransactionPipelineDemo)
* Domain types & services

    * [xref\:PatternKit.Examples.Chain.TransactionContext](xref:PatternKit.Examples.Chain.TransactionContext), [xref\:PatternKit.Examples.Chain.TxResult](xref:PatternKit.Examples.Chain.TxResult), [xref\:PatternKit.Examples.Chain.Tender](xref:PatternKit.Examples.Chain.Tender), [xref\:PatternKit.Examples.Chain.LineItem](xref:PatternKit.Examples.Chain.LineItem), [xref\:PatternKit.Examples.Chain.Customer](xref:PatternKit.Examples.Chain.Customer)
    * [xref\:PatternKit.Examples.Chain.IDeviceBus](xref:PatternKit.Examples.Chain.IDeviceBus), [xref\:PatternKit.Examples.Chain.ICardProcessor](xref:PatternKit.Examples.Chain.ICardProcessor), [xref\:PatternKit.Examples.Chain.CardProcessors](xref:PatternKit.Examples.Chain.CardProcessors)
* Rounding

    * [xref\:PatternKit.Examples.Chain.IRoundingRule](xref:PatternKit.Examples.Chain.IRoundingRule), [xref\:PatternKit.Examples.Chain.CharityRoundUpRule](xref:PatternKit.Examples.Chain.CharityRoundUpRule), [xref\:PatternKit.Examples.Chain.NickelCashOnlyRule](xref:PatternKit.Examples.Chain.NickelCashOnlyRule), [xref\:PatternKit.Examples.Chain.RoundingPipeline](xref:PatternKit.Examples.Chain.RoundingPipeline)
* Tender routing

    * [xref\:PatternKit.Examples.Chain.TenderRouterFactory](xref:PatternKit.Examples.Chain.TenderRouterFactory)
    * [xref\:PatternKit.Creational.Builder.BranchBuilder\`2](xref:PatternKit.Creational.Builder.BranchBuilder`2)
    * [xref\:PatternKit.Creational.Builder.ChainBuilder\`1](xref:PatternKit.Creational.Builder.ChainBuilder`1)
* Chains

    * [xref\:PatternKit.Behavioral.Chain.ActionChain%601](xref:PatternKit.Behavioral.Chain.ActionChain%601) (used via `ChainStage.From(...)`)

