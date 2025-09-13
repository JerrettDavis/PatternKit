using PatternKit.Behavioral.Chain;
using PatternKit.Creational.Builder;
using PatternKit.Examples.Chain.ConfigDriven;

namespace PatternKit.Examples.Chain;

/// <summary>
/// Represents a single pipeline stage that mutates a <see cref="TransactionContext"/> and signals whether
/// the pipeline should continue (<see langword="true"/>) or stop (<see langword="false"/>).
/// </summary>
/// <param name="ctx">The working transaction context being processed.</param>
/// <returns><see langword="true"/> to continue with the next stage; <see langword="false"/> to stop the pipeline.</returns>
public delegate bool Stage(TransactionContext ctx);

// ---------- Tender router (BranchBuilder-powered strategy selection) ----------

/// <summary>
/// Predicate used by the tender router to decide whether a handler can process a specific tender.
/// </summary>
/// <param name="c">The current transaction context (passed by <see langword="in"/> for performance).</param>
/// <param name="t">The tender candidate (passed by <see langword="in"/> for performance).</param>
/// <returns><see langword="true"/> if the handler should handle the tender; otherwise <see langword="false"/>.</returns>
public delegate bool TenderPred(in TransactionContext c, in Tender t);

/// <summary>
/// Handler step invoked by the tender router once a predicate matches.
/// </summary>
/// <param name="c">The transaction context.</param>
/// <param name="t">The tender to handle.</param>
/// <returns>A <see cref="TxResult"/> describing the outcome of handling the tender.</returns>
public delegate TxResult TenderStep(TransactionContext c, in Tender t);

/// <summary>
/// A composite delegate that routes a tender to an appropriate handler.
/// </summary>
/// <param name="c">The transaction context.</param>
/// <param name="t">The tender to route.</param>
/// <returns>The <see cref="TxResult"/> returned by the matched handler or a failure result if none match.</returns>
public delegate TxResult TenderRouter(TransactionContext c, in Tender t);

/// <summary>
/// Factory that composes a <see cref="TenderRouter"/> from a sequence of <see cref="ITenderHandler"/> instances
/// using a branch builder for zero-<c>if</c> routing.
/// </summary>
public static class TenderRouterFactory
{
    /// <summary>
    /// Builds a <see cref="TenderRouter"/> that evaluates handlers in registration order and uses the first
    /// handler whose <see cref="ITenderHandler.CanHandle(TransactionContext, Tender)"/> returns <see langword="true"/>.
    /// </summary>
    /// <param name="handlers">The handlers to consider for routing (order matters).</param>
    /// <returns>A fast router function that encapsulates predicate checks and handler execution.</returns>
    public static TenderRouter Build(IEnumerable<ITenderHandler> handlers)
    {
        var bb = BranchBuilder<TenderPred, TenderStep>.Create();

        foreach (var handler in handlers)
        {
            var h = handler; // avoid modified-closure issue
            bb.Add(
                // wrap CanHandle so it matches: (in TransactionContext, in Tender) -> bool
                (in c, in t) => h.CanHandle(c, t),
                // wrap Handle so it matches: (TransactionContext, in Tender) -> TxResult
                (c, in t) => h.Handle(c, t)
            );
        }

        return bb.Build<TenderRouter>(
            fallbackDefault: static (ctx, in t)
                => TxResult.Fail("route", $"no handler for {t.Kind}"),
            projector: static (predicates, steps, _, @default) =>
            {
                return (ctx, in t) =>
                {
                    for (var i = 0; i < predicates.Length; i++)
                        if (predicates[i](in ctx, in t))
                            return steps[i](ctx, in t);

                    return @default(ctx, in t);
                };
            });
    }
}

// ---------- Mini helper to wrap ActionChain into a Stage ----------

/// <summary>
/// Helpers for adapting <see cref="ActionChain{T}"/> pipelines to <see cref="Stage"/> delegates.
/// </summary>
public static class ChainStage
{
    /// <summary>
    /// Wraps an <see cref="ActionChain{T}"/> so it can be used as a pipeline <see cref="Stage"/>.
    /// </summary>
    /// <param name="chain">The action chain to execute.</param>
    /// <returns>A stage that executes the chain and continues unless it set a failing <see cref="TransactionContext.Result"/>.</returns>
    public static Stage From(ActionChain<TransactionContext> chain)
        => ctx =>
        {
            chain.Execute(ctx);
            return ctx.Result?.Ok != false;
        };
}

// ---------- Builder that composes the pipeline declaratively ----------

/// <summary>
/// A small, immutable runner for a composed transaction pipeline.
/// </summary>
/// <param name="stages">The ordered list of stages to execute.</param>
public sealed class TransactionPipeline(Stage[] stages)
{
    /// <summary>
    /// Executes the pipeline against the provided context.
    /// </summary>
    /// <param name="ctx">The transaction context to process.</param>
    /// <returns>
    /// A tuple containing the terminal <see cref="TxResult"/> and the (mutated) <see cref="TransactionContext"/>.
    /// If no stage produced a terminal result, the outcome is forced to <c>paid</c>.
    /// </returns>
    public (TxResult Result, TransactionContext Ctx) Run(TransactionContext ctx)
        => stages.Any(s => !s(ctx))
            ? (ctx.Result!.Value, ctx)
            : (ctx.Result ?? TxResult.Success("paid", "paid in full"), ctx);
}

/// <summary>
/// Fluent builder that composes a production-grade transaction pipeline using small, focused stages.
/// </summary>
public sealed class TransactionPipelineBuilder
{
    private readonly ChainBuilder<Stage> _chain = ChainBuilder<Stage>.Create();

    // deps
    private IDeviceBus _devices = new DeviceBus();
    private readonly List<ITenderHandler> _tenderHandlers = [];

    private IReadOnlyList<IRoundingRule> _roundingRules =
    [
        new CharityRoundUpRule(new NoopCharityTracker()),
        new NickelCashOnlyRule()
    ];

    /// <summary>
    /// Creates a new builder with sensible defaults.
    /// </summary>
    public static TransactionPipelineBuilder New() => new();

    /// <summary>
    /// Injects the device bus used by cash handling and completion beeps.
    /// </summary>
    /// <param name="devices">The device bus implementation.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public TransactionPipelineBuilder WithDeviceBus(IDeviceBus devices)
    {
        _devices = devices;
        return this;
    }

    /// <summary>
    /// Supplies a set of <see cref="ITenderHandler"/> implementations used by the tender router.
    /// </summary>
    /// <param name="handlers">Handlers to add in the given order.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public TransactionPipelineBuilder WithTenderHandlers(params ITenderHandler[] handlers)
    {
        _tenderHandlers.AddRange(handlers);
        return this;
    }

    /// <summary>
    /// Overrides the default rounding rules applied by <see cref="AddRounding"/>.
    /// </summary>
    /// <param name="rules">A list of rules evaluated in order (first-match wins).</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public TransactionPipelineBuilder WithRoundingRules(params IRoundingRule[] rules)
    {
        _roundingRules = rules;
        return this;
    }

    /// <summary>
    /// Appends an arbitrary stage to the pipeline.
    /// </summary>
    /// <param name="stage">The stage delegate.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public TransactionPipelineBuilder AddStage(Stage stage)
    {
        _chain.Add(stage);
        return this;
    }

    /// <summary>
    /// Appends an <see cref="ActionChain{T}"/> stage to the pipeline.
    /// </summary>
    /// <param name="chain">The action chain to adapt.</param>
    /// <returns>The same builder for fluent chaining.</returns>
    public TransactionPipelineBuilder AddStage(ActionChain<TransactionContext> chain)
        => AddStage(ChainStage.From(chain));


    // --- PREAUTH via ActionChain (no if/else) --------------------------------
    /// <summary>
    /// Adds pre-authorization checks (age restriction, non-empty basket) without <c>if</c>/<c>else</c> branching.
    /// </summary>
    /// <remarks>
    /// If any rule fails, a terminal failure result is set and the pipeline stops.
    /// </remarks>
    public TransactionPipelineBuilder AddPreauth()
    {
        var preauth = ActionChain<TransactionContext>.Create()
            .When(static (in c) => c.Items.Any(i => i.AgeRestricted) && c.Customer.AgeYears < 21)
            .ThenStop(static c =>
            {
                c.Result = TxResult.Fail("age", "age verification failed");
                c.Log.Add("preauth: blocked by age restriction");
            })
            .When(static (in c) => c.Items.Count == 0)
            .ThenStop(static c =>
            {
                c.Result = TxResult.Fail("empty", "no items");
                c.Log.Add("preauth: empty basket");
            })
            .Finally(static (in c, next) =>
            {
                c.Log.Add("preauth: ok");
                next(in c);
            })
            .Build();

        _chain.Add(ChainStage.From(preauth));
        return this;
    }

    // --- DISCOUNTS & TAX via ActionChain (no if/else) ------------------------
    /// <summary>
    /// Adds subtotal computation, discount policies, and tax calculation as a branchless action chain.
    /// </summary>
    /// <remarks>
    /// The following discount examples are included: cash 2% off (if cash-first), loyalty 5% off,
    /// manufacturer coupons, in-house coupons, and a bundle deal when two or more items share a bundle key.
    /// </remarks>
    public TransactionPipelineBuilder AddDiscountsAndTax()
    {
        var totals = ActionChain<TransactionContext>.Create()
            .Use(static (in c, next) =>
            {
                c.RecomputeSubtotal();
                c.Log.Add($"subtotal: {c.Subtotal:C2}");
                next(in c);
            })
            .When(static (in c) => (c.Tenders.Count > 0 ? c.Tenders[0] : c.Tender)?.Kind == PaymentKind.Cash)
            .ThenContinue(static c =>
            {
                var off = Math.Round(c.Subtotal * 0.02m, 2);
                c.AddDiscount(off, "cash 2% off");
            })
            .When(static (in c) => !string.IsNullOrWhiteSpace(c.Customer.LoyaltyId))
            .ThenContinue(static c =>
            {
                var off = Math.Round(c.Subtotal * 0.05m, 2);
                c.AddDiscount(off, $"loyalty {c.Customer.LoyaltyId}");
            })
            .When(static (in c) => c.Items.Any(i => i.ManufacturerCoupon > 0m))
            .ThenContinue(static c =>
            {
                var off = c.Items.Sum(i => i.ManufacturerCoupon * i.Qty);
                c.AddDiscount(off, "manufacturer coupons");
            })
            .When(static (in c) => c.Items.Any(i => i.InHouseCoupon > 0m))
            .ThenContinue(static c =>
            {
                var off = c.Items.Sum(i => i.InHouseCoupon * i.Qty);
                c.AddDiscount(off, "in-house coupons");
            })
            .When(static (in c) =>
                c.Items.GroupBy(i => i.BundleKey).Any(g => g.Key is not null && g.Sum(i => i.Qty) >= 2))
            .ThenContinue(static c =>
            {
                var off = c.Items.GroupBy(i => i.BundleKey)
                    .Where(g => g.Key is not null && g.Sum(i => i.Qty) >= 2)
                    .Sum(g => g.Sum(i => i.Qty) * 1.00m);
                c.AddDiscount(off, "bundle deal");
            })
            .Finally(static (in c, next) =>
            {
                var taxable = Math.Max(0m, c.Subtotal - c.DiscountTotal);
                var tax = Math.Round(taxable * 0.0875m, 2);
                c.SetTax(tax);
                c.Log.Add($"pre-round total: {c.GrandTotal:C2}");
                next(in c);
            })
            .Build();

        _chain.Add(ChainStage.From(totals));
        return this;
    }

    // --- ROUNDING via strategy list -----------------------------------------
    /// <summary>
    /// Adds a rounding stage that evaluates configured <see cref="IRoundingRule"/> strategies in order
    /// and applies the first that matches.
    /// </summary>
    public TransactionPipelineBuilder AddRounding()
    {
        _chain.Add(ctx =>
        {
            RoundingPipeline.Apply(ctx, _roundingRules);
            return true;
        });
        return this;
    }


    // --- TENDER handling via BranchBuilder-powered router --------------------
    /// <summary>
    /// Adds tender handling using a branch-built router. If no handlers are supplied, cash and card
    /// defaults are registered automatically.
    /// </summary>
    public TransactionPipelineBuilder AddTenderHandling()
    {
        // if not supplied, default to your existing cash/card strategies
        if (_tenderHandlers.Count == 0)
        {
            var processors = new CardProcessors(new()
            {
                [CardVendor.Visa] = new GenericProcessor("VisaNet"),
                [CardVendor.Mastercard] = new GenericProcessor("MC"),
                [CardVendor.Amex] = new GenericProcessor("Amex"),
                [CardVendor.Chase] = new GenericProcessor("ChaseNet"),
                [CardVendor.InHouse] = new GenericProcessor("InHouse"),
                [CardVendor.Unknown] = new GenericProcessor("FallbackNet"),
            });

            _tenderHandlers.AddRange([
                new CashTender(_devices),
                new CardTender(processors)
            ]);
        }

        var route = TenderRouterFactory.Build(_tenderHandlers);

        _chain.Add(ctx =>
        {
            var tenders = ctx.Tenders.Count > 0
                ? ctx.Tenders
                : ctx.Tender is null
                    ? new List<Tender>()
                    : new List<Tender> { ctx.Tender };

            ctx.Result = tenders
                .TakeWhile(_ => ctx.RemainderDue > 0m)
                .Select(t => route(ctx, in t))
                .Cast<TxResult?>()
                .LastOrDefault(res => res is { Ok: true });

            return ctx.Result is null or { Ok: true };
        });

        return this;
    }

    // --- finalize ------------------------------------------------------------
    /// <summary>
    /// Adds a finalization stage that turns the remaining balance into a terminal result and performs
    /// small side-effects like beeping a device.
    /// </summary>
    public TransactionPipelineBuilder AddFinalize()
    {
        var devices = _devices;
        _chain.Add(ctx =>
        {
            if (ctx.Result is { Ok: false }) return false;

            ctx.Result = ctx.RemainderDue > 0m
                ? TxResult.Fail("insufficient", $"still due {ctx.RemainderDue:C2}")
                : TxResult.Success("paid", "paid in full");

            if (ctx.Result.Value.Ok) devices.Beep("printer", 2);
            ctx.Log.Add("done.");
            return true;
        });
        return this;
    }

    /// <summary>
    /// Materializes the configured stages into a runnable <see cref="TransactionPipeline"/>.
    /// </summary>
    public TransactionPipeline Build() => _chain.Build(stages => new TransactionPipeline(stages));
}

// ---------- Domain ----------

/// <summary>
/// Supported payment kinds.
/// </summary>
public enum PaymentKind
{
    /// <summary>Cash payment at the point of sale.</summary>
    Cash,
    /// <summary>Card payment (debit/credit).</summary>
    Card /*, Crypto, Check ...*/
}

/// <summary>
/// How a card authorization was performed.
/// </summary>
public enum CardAuthType
{
    /// <summary>Chip insert (EMV).</summary>
    Chip,
    /// <summary>Magnetic stripe swipe.</summary>
    Swipe,
    /// <summary>Contactless/NFC (tap).</summary>
    Contactless
}

/// <summary>
/// Card networks/vendors recognized by the demo.
/// </summary>
public enum CardVendor
{
    Visa,
    Mastercard,
    Amex,
    Chase,
    InHouse,
    Unknown
}

/// <summary>
/// Customer information relevant to the checkout flow.
/// </summary>
/// <param name="LoyaltyId">Optional loyalty identifier used by discounts.</param>
/// <param name="AgeYears">Customer age in years for age-restricted checks.</param>
public sealed record Customer(string? LoyaltyId, int AgeYears);

/// <summary>
/// Represents an item being purchased.
/// </summary>
/// <param name="Sku">The item SKU; special SKUs (e.g., <c>CHARITY:*</c>) drive certain behaviors.</param>
/// <param name="UnitPrice">Unit price before discounts and taxes.</param>
/// <param name="Qty">Quantity of units.</param>
/// <param name="AgeRestricted">Whether the item is age restricted.</param>
/// <param name="BundleKey">Optional key used to detect bundle promotions.</param>
/// <param name="ManufacturerCoupon">Manufacturer coupon amount per unit.</param>
/// <param name="InHouseCoupon">In-house coupon amount per unit.</param>
public sealed record LineItem(
    string Sku,
    decimal UnitPrice,
    int Qty = 1,
    bool AgeRestricted = false,
    string? BundleKey = null,
    decimal ManufacturerCoupon = 0m,
    decimal InHouseCoupon = 0m
);

/// <summary>
/// Represents a payment attempt made by the customer.
/// </summary>
/// <param name="Kind">The payment kind.</param>
/// <param name="AuthType">Card authorization type (for card payments).</param>
/// <param name="Vendor">Card network/vendor (for card payments).</param>
/// <param name="CashGiven">Cash presented by the customer (for cash payments).</param>
public sealed record Tender(
    PaymentKind Kind,
    CardAuthType? AuthType = null,
    CardVendor? Vendor = null,
    decimal CashGiven = 0m
);

/// <summary>
/// Mutable transaction state that flows through all pipeline stages.
/// </summary>
public sealed class TransactionContext
{
    /// <summary>
    /// Unique transaction identifier.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Customer participating in the transaction.
    /// </summary>
    public required Customer Customer { get; init; }

    /// <summary>
    /// Single tender used by legacy callers; prefer <see cref="Tenders"/>.
    /// </summary>
    public Tender? Tender { get; set; }

    /// <summary>
    /// A list of tenders to process in order.
    /// </summary>
    public List<Tender> Tenders { get; init; } = [];

    /// <summary>
    /// Items being purchased.
    /// </summary>
    public required List<LineItem> Items { get; init; }

    // Running totals
    /// <summary>The running subtotal before discounts and tax.</summary>
    public decimal Subtotal { get; private set; }
    /// <summary>Total amount discounted so far.</summary>
    public decimal DiscountTotal { get; private set; }
    /// <summary>Tax total computed for the purchase.</summary>
    public decimal TaxTotal { get; private set; }
    /// <summary>Rounding delta applied by rounding rules.</summary>
    public decimal RoundingDelta { get; private set; }

    /// <summary>
    /// Grand total including discounts, taxes, and rounding.
    /// </summary>
    public decimal GrandTotal => Subtotal - DiscountTotal + TaxTotal + RoundingDelta;

    // Tendering
    /// <summary>Total amount paid across all tenders.</summary>
    public decimal AmountPaid { get; private set; }
    /// <summary>Remaining amount due after payments, never negative.</summary>
    public decimal RemainderDue => Math.Max(0m, Math.Round(GrandTotal - AmountPaid, 2));
    /// <summary>Cash change due to the customer, if any.</summary>
    public decimal? CashChange { get; set; }

    // Card processors read this per-authorization
    /// <summary>
    /// The amount to authorize for card payments for the current step.
    /// </summary>
    public decimal AuthorizationAmount { get; set; }

    // Side-effects & logs
    /// <summary>
    /// Human-readable log recording applied rules and decisions.
    /// </summary>
    public List<string> Log { get; } = [];

    // Terminal outcome
    /// <summary>
    /// Terminal result set by stages. When set to a failing result the pipeline stops.
    /// </summary>
    public TxResult? Result { get; set; }

    /// <summary>
    /// True when all configured tenders are cash; used for cash-only rounding.
    /// </summary>
    public bool IsCashOnlyTransaction =>
        (Tenders.Count > 0
            ? Tenders
            : Tender is null
                ? []
                : new List<Tender> { Tender })
        .All(t => t.Kind == PaymentKind.Cash);

    /// <summary>
    /// Recomputes the <see cref="Subtotal"/> from <see cref="Items"/>.
    /// </summary>
    public void RecomputeSubtotal() =>
        Subtotal = Items.Sum(i => i.UnitPrice * i.Qty);

    /// <summary>
    /// Adds a discount amount with a reason and updates the running total.
    /// </summary>
    /// <param name="amount">The discount amount to add; ignored if not positive.</param>
    /// <param name="reason">Human-readable reason recorded in <see cref="Log"/>.</param>
    public void AddDiscount(decimal amount, string reason)
    {
        if (amount <= 0m) return;
        DiscountTotal += amount;
        Log.Add($"discount: {reason} {amount:C2}");
    }

    /// <summary>
    /// Sets the total tax and logs it.
    /// </summary>
    /// <param name="amount">The computed tax amount.</param>
    public void SetTax(decimal amount)
    {
        TaxTotal = amount;
        Log.Add($"tax: {amount:C2}");
    }

    /// <summary>
    /// Applies a rounding delta and logs the change.
    /// </summary>
    /// <param name="delta">The rounding delta (can be positive or negative).</param>
    /// <param name="reason">Human-readable reason recorded in <see cref="Log"/>.</param>
    public void ApplyRounding(decimal delta, string reason)
    {
        if (delta == 0m) return;
        RoundingDelta = Math.Round(delta, 2);
        Log.Add($"round: {reason} {(delta >= 0 ? "+" : "")}{RoundingDelta:C2}");
    }

    /// <summary>
    /// Applies a payment amount and logs the new remaining balance.
    /// </summary>
    /// <param name="amount">The amount paid.</param>
    /// <param name="how">Descriptor of how the payment was made (e.g., "cash").</param>
    public void ApplyPayment(decimal amount, string how)
    {
        if (amount <= 0m) return;
        AmountPaid = Math.Round(AmountPaid + amount, 2);
        Log.Add($"paid: {how} {amount:C2} (remaining {RemainderDue:C2})");
    }
}

/// <summary>
/// Lightweight result for pipeline stages and handlers.
/// </summary>
/// <param name="Ok">True for success; false for failure.</param>
/// <param name="Code">Machine-readable code (e.g., "paid", "age").</param>
/// <param name="Message">Human-readable message.</param>
public readonly record struct TxResult(bool Ok, string Code, string Message)
{
    /// <summary>
    /// Creates a success result.
    /// </summary>
    /// <param name="code">Optional success code (default: <c>ok</c>).</param>
    /// <param name="msg">Optional message (default: <c>approved</c>).</param>
    public static TxResult Success(string code = "ok", string msg = "approved") => new(true, code, msg);

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    /// <param name="code">Failure code.</param>
    /// <param name="msg">Human-readable message.</param>
    public static TxResult Fail(string code, string msg) => new(false, code, msg);
}

// ---------- External devices / services (stubs) ----------

/// <summary>
/// Abstraction over external peripherals used by the pipeline (cash drawer, beeper, etc.).
/// </summary>
public interface IDeviceBus
{
    /// <summary>Opens the cash drawer.</summary>
    /// <param name="drawer">Drawer number (default: 1).</param>
    void OpenCashDrawer(int drawer = 1);

    /// <summary>Beeps a device for a given duration.</summary>
    /// <param name="device">Device name.</param>
    /// <param name="units">Arbitrary duration/units (default: 1).</param>
    void Beep(string device, int units = 1);
}

/// <summary>
/// Minimal card processor abstraction used by the demo handlers.
/// </summary>
public interface ICardProcessor
{
    /// <summary>Performs a card authorization for <see cref="TransactionContext.AuthorizationAmount"/>.</summary>
    TxResult Authorize(TransactionContext ctx);
    /// <summary>Captures the previously authorized amount.</summary>
    TxResult Capture(TransactionContext ctx);
}

/// <summary>
/// No-op device bus implementation used by the sample pipeline.
/// </summary>
public sealed class DeviceBus : IDeviceBus
{
    /// <inheritdoc />
    public void OpenCashDrawer(int drawer = 1)
    {
        /* talk to POS */
    }

    /// <inheritdoc />
    public void Beep(string device, int units = 1)
    {
        /* talk to peripherals */
    }
}

/// <summary>
/// Registry of <see cref="ICardProcessor"/> instances keyed by <see cref="CardVendor"/>.
/// </summary>
public sealed class CardProcessors
{
    private readonly Dictionary<CardVendor, ICardProcessor> _map;
    /// <summary>Creates a new registry.</summary>
    public CardProcessors(Dictionary<CardVendor, ICardProcessor> map) => _map = map;

    /// <summary>
    /// Resolves a processor for the specified vendor, falling back to <see cref="CardVendor.Unknown"/>.
    /// </summary>
    public ICardProcessor Resolve(CardVendor? v) =>
        v is not null && _map.TryGetValue(v.Value, out var p) ? p : _map[CardVendor.Unknown];
}

// ---------- Demo processors ----------

/// <summary>
/// Simple demo processor that always approves authorization and capture.
/// </summary>
public sealed class GenericProcessor(string name) : ICardProcessor
{
    /// <inheritdoc />
    public TxResult Authorize(TransactionContext ctx) =>
        TxResult.Success("auth", $"{name}: authorized {ctx.AuthorizationAmount:C2}");

    /// <inheritdoc />
    public TxResult Capture(TransactionContext ctx) =>
        TxResult.Success("capture", $"{name}: captured {ctx.AuthorizationAmount:C2}");
}

// ---------- Tender strategies (flat, declarative) ----------

/// <summary>
/// Strategy contract for declarative tender processing.
/// </summary>
public interface ITenderStrategy
{
    /// <summary>The payment kind this strategy supports.</summary>
    PaymentKind Kind { get; }

    /// <summary>
    /// Attempts to apply this tender to the context. Returns a failure result if tendering should stop.
    /// Returns null to indicate success/continue.
    /// </summary>
    TxResult? TryApply(TransactionContext ctx, Tender tender);
}

/// <summary>
/// Observability hook invoked when charity rounding is applied.
/// </summary>
public interface ICharityTracker
{
    /// <summary>Tracks a charity round-up event.</summary>
    void Track(string charity, Guid transactionId, decimal delta, decimal newTotal);
}

/// <summary>
/// No-op charity tracker used by the sample.
/// </summary>
public sealed class NoopCharityTracker : ICharityTracker
{
    /// <inheritdoc />
    public void Track(string charity, Guid transactionId, decimal delta, decimal newTotal)
    {
        /* noop */
    }
}

/// <summary>
/// Contract for precise rounding rules evaluated by the rounding pipeline.
/// </summary>
public interface IRoundingRule
{
    /// <summary>Human readable reason (used for logging).</summary>
    string Reason { get; }

    /// <summary>Returns true if this rule should be considered for the current context.</summary>
    bool ShouldApply(in TransactionContext c);

    /// <summary>Computes the delta to apply. Return 0m for no change.</summary>
    decimal ComputeDelta(in TransactionContext c);

    /// <summary>Optional side-effects when the delta was actually applied.</summary>
    void OnApplied(TransactionContext c, decimal appliedDelta)
    {
    }
}

/// <summary>
/// Applies cash tenders, opening the drawer and calculating any change due.
/// </summary>
public sealed class CashTenderStrategy(IDeviceBus devices) : ITenderStrategy
{
    /// <inheritdoc />
    public PaymentKind Kind => PaymentKind.Cash;

    /// <inheritdoc />
    public TxResult? TryApply(TransactionContext ctx, Tender tender)
    {
        if (ctx.RemainderDue <= 0m) return null;

        devices.OpenCashDrawer();

        var applied = Math.Min(tender.CashGiven, ctx.RemainderDue);
        ctx.ApplyPayment(applied, "cash");

        // change only if this cash overpays AFTER covering all due
        var leftover = Math.Round(tender.CashGiven - applied, 2);
        if (leftover > 0m)
        {
            ctx.CashChange = (ctx.CashChange ?? 0m) + leftover;
            ctx.Log.Add($"cash: change {leftover:C2}");
        }

        return null; // success, continue
    }
}

/// <summary>
/// Applies card tenders by authorizing and capturing the current remainder.
/// </summary>
public sealed class CardTenderStrategy(CardProcessors processors) : ITenderStrategy
{
    /// <inheritdoc />
    public PaymentKind Kind => PaymentKind.Card;

    /// <inheritdoc />
    public TxResult? TryApply(TransactionContext ctx, Tender tender)
    {
        if (ctx.RemainderDue <= 0m) return null;

        ctx.AuthorizationAmount = ctx.RemainderDue;
        if (ctx.AuthorizationAmount <= 0m) return null;

        var proc = processors.Resolve(tender.Vendor);
        var auth = proc.Authorize(ctx);
        if (!auth.Ok)
        {
            ctx.Log.Add($"auth: declined ({auth.Code})");
            return auth;
        }

        var cap = proc.Capture(ctx);
        if (!cap.Ok)
        {
            ctx.Log.Add("auth: capture failed");
            return cap;
        }

        ctx.ApplyPayment(ctx.AuthorizationAmount,
            $"card {tender.Vendor} {tender.AuthType?.ToString() ?? "Unknown"}");
        ctx.Log.Add($"auth: captured via {tender.Vendor} {ctx.AuthorizationAmount:C2}");
        return null; // success, continue
    }
}

/// <summary>
/// Small registry that resolves a single <see cref="ITenderStrategy"/> by <see cref="PaymentKind"/>.
/// </summary>
public sealed class TenderStrategyRegistry
{
    private readonly Dictionary<PaymentKind, ITenderStrategy> _map;

    /// <summary>Creates a registry from the provided strategies.</summary>
    public TenderStrategyRegistry(IEnumerable<ITenderStrategy> strategies)
        => _map = strategies.ToDictionary(s => s.Kind);

    /// <summary>Resolves the strategy for the specified <paramref name="kind"/>.</summary>
    public ITenderStrategy Resolve(PaymentKind kind)
        => _map.TryGetValue(kind, out var s)
            ? s
            : throw new InvalidOperationException($"No strategy for {kind}");
}

/// <summary>
/// Rounds up to the next dollar when a charity SKU is present and notifies a tracker.
/// </summary>
public sealed class CharityRoundUpRule(ICharityTracker tracker) : IRoundingRule
{
    /// <inheritdoc />
    public string Reason => "charity round-up";

    /// <inheritdoc />
    public bool ShouldApply(in TransactionContext c)
        => c.Items.Any(i => i.Sku.StartsWith("CHARITY:", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public decimal ComputeDelta(in TransactionContext c)
    {
        var delta = Math.Ceiling(c.GrandTotal) - c.GrandTotal;
        return Math.Round(delta, 2);
    }

    /// <inheritdoc />
    public void OnApplied(TransactionContext c, decimal appliedDelta)
    {
        var charitySku = c.Items.First(i => i.Sku.StartsWith("CHARITY:", StringComparison.OrdinalIgnoreCase)).Sku;
        var name = charitySku["CHARITY:".Length..];
        tracker.Track(name, c.Id, appliedDelta, c.GrandTotal + appliedDelta);
        c.Log.Add($"charity: {name} notified for {appliedDelta:C2}");
    }
}

/// <summary>
/// Rounds to the nearest nickel, but only for cash-only transactions that include the special SKU.
/// </summary>
public sealed class NickelCashOnlyRule : IRoundingRule
{
    /// <inheritdoc />
    public string Reason => "nickel (cash-only)";

    /// <inheritdoc />
    public bool ShouldApply(in TransactionContext c)
        => c.IsCashOnlyTransaction && c.Items.Any(i => string.Equals(i.Sku, "ROUND:NICKEL", StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public decimal ComputeDelta(in TransactionContext c)
    {
        // Round to nearest 0.05 with midpoint away from zero
        var rounded = Math.Round(c.GrandTotal * 20m, MidpointRounding.AwayFromZero) / 20m;
        return Math.Round(rounded - c.GrandTotal, 2);
    }
}

/// <summary>
/// Evaluates a list of rounding rules and applies the first matching rule (first-match wins).
/// </summary>
public static class RoundingPipeline
{
    /// <summary>
    /// Applies the first matching rounding rule and logs the resulting total (or that no rounding was applied).
    /// </summary>
    /// <param name="c">The transaction context.</param>
    /// <param name="rules">Ordered list of rules to evaluate.</param>
    public static void Apply(
        TransactionContext c,
        IReadOnlyList<IRoundingRule> rules)
    {
        foreach (var rule in rules)
        {
            if (!rule.ShouldApply(in c)) continue;

            var delta = rule.ComputeDelta(in c);
            if (delta == 0m) continue;

            c.ApplyRounding(delta, rule.Reason);
            rule.OnApplied(c, delta); // side-effects for this rule only
            c.Log.Add($"total: {c.GrandTotal:C2}");
            return; // first-match-wins: stop after one rounding
        }

        c.Log.Add("round: none");
        c.Log.Add($"total: {c.GrandTotal:C2}");
    }
}

// ---------- Pipeline demo ----------

/// <summary>
/// Entry point that builds and runs the mediated transaction pipeline using in-code stages.
/// </summary>
public static class MediatedTransactionPipelineDemo
{
    /// <summary>
    /// Builds the default pipeline and executes it against the provided <paramref name="ctx"/>.
    /// </summary>
    /// <param name="ctx">The transaction context to run.</param>
    /// <returns>The terminal result and the (mutated) context.</returns>
    public static (TxResult Result, TransactionContext Ctx) Run(TransactionContext ctx)
    {
        var pipeline = TransactionPipelineBuilder.New()
            .AddPreauth()
            .AddDiscountsAndTax()
            .AddRounding()
            .AddTenderHandling()
            .AddFinalize()
            .Build();

        return pipeline.Run(ctx);
    }
}