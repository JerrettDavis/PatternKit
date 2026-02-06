using PatternKit.Behavioral.Observer;

namespace PatternKit.Examples.ObserverDemo;

/// <summary>Customer loyalty tiers used for discount calculation.</summary>
public enum LoyaltyTier { None, Silver, Gold, Platinum }

/// <summary>Payment methods that may influence discounts or eligibility.</summary>
public enum PaymentKind { None, CreditCard, StoreCard, Cash }

/// <summary>
/// Represents a line item in a transaction.
/// </summary>
/// <param name="Sku">The product SKU.</param>
/// <param name="Qty">The quantity ordered.</param>
/// <param name="UnitPrice">The unit price.</param>
/// <param name="DiscountPct">Optional per-line discount percentage (0..1).</param>
/// <param name="Taxable">Whether the line is taxable.</param>
public readonly record struct LineItem(string Sku, int Qty, decimal UnitPrice, decimal? DiscountPct = null, bool Taxable = true)
{
    /// <summary>Total raw amount before discounts.</summary>
    public decimal Raw => Qty * UnitPrice;

    /// <summary>Discount amount for this line based on <see cref="DiscountPct"/>.</summary>
    public decimal LineDiscount => DiscountPct is { } p ? Raw * p : 0m;

    /// <summary>Net amount after line discount.</summary>
    public decimal Net => Raw - LineDiscount;
}

/// <summary>
/// Reactive transaction shows dependent, computed properties updated via Observer-based subscriptions.
/// It recomputes totals and UI-like flags whenever any input changes.
/// </summary>
public sealed class ReactiveTransaction
{
    // Inputs (reactive)
    /// <summary>Collection of items in the transaction. Publishes add/remove events.</summary>
    public ObservableList<LineItem> Items { get; } = [];

    /// <summary>Current customer loyalty tier.</summary>
    public ObservableVar<LoyaltyTier> Tier { get; } = new();

    /// <summary>Selected payment method.</summary>
    public ObservableVar<PaymentKind> Payment { get; } = new();

    /// <summary>Applicable tax rate for taxable items.</summary>
    public ObservableVar<decimal> TaxRate { get; } = new(0.07m);

    // Outputs (reactive vars so consumers can subscribe to precise changes)
    /// <summary>Subtotal before discounts and tax.</summary>
    public ObservableVar<decimal> Subtotal { get; } = new();

    /// <summary>Total of all per-line discounts.</summary>
    public ObservableVar<decimal> LineItemDiscounts { get; } = new();

    /// <summary>Discount based on loyalty tier.</summary>
    public ObservableVar<decimal> LoyaltyDiscount { get; } = new();

    /// <summary>Discount based on payment method.</summary>
    public ObservableVar<decimal> PaymentDiscount { get; } = new();

    /// <summary>Calculated tax on taxable net amount.</summary>
    public ObservableVar<decimal> Tax { get; } = new();

    /// <summary>Final total after all discounts and tax.</summary>
    public ObservableVar<decimal> Total { get; } = new();

    // UI-ish dependent properties
    /// <summary>Whether the transaction meets basic checkout requirements.</summary>
    public ObservableVar<bool> CanCheckout { get; } = new();

    /// <summary>Optional badge text indicating savings.</summary>
    public ObservableVar<string?> DiscountBadge { get; } = new();

    // Fine-grained change notifications for property names, if needed
    /// <summary>Name-based change hub for UI bindings listening by property name.</summary>
    public PropertyChangedHub PropertyChanged { get; } = new();

    private readonly Observer<string>.Handler _notify;

    /// <summary>
    /// Create the transaction and wire reactive inputs so that any change triggers recomputation of outputs.
    /// </summary>
    public ReactiveTransaction()
    {
        _notify = (in p) => PropertyChanged.Raise(p);

        // Recompute on any input change
        Items.Subscribe((_, _) => Recompute());
        Tier.Subscribe((_, _) => Recompute());
        Payment.Subscribe((_, _) => Recompute());
        TaxRate.Subscribe((_, _) => Recompute());

        // Recompute once on construction
        Recompute();
    }

    private void Recompute()
    {
        var list = Items.Snapshot();
        decimal raw = 0m, lineDisc = 0m, taxableNet = 0m;

        foreach (var it in list)
        {
            raw += it.Raw;
            lineDisc += it.LineDiscount;
            if (it.Taxable) taxableNet += it.Net;
        }

        var tierDiscPct = Tier.Value switch
        {
            LoyaltyTier.Platinum => 0.10m,
            LoyaltyTier.Gold => 0.07m,
            LoyaltyTier.Silver => 0.04m,
            _ => 0m
        };

        var paymentDiscPct = Payment.Value switch
        {
            PaymentKind.StoreCard => 0.05m, // in-house card promo
            _ => 0m
        };

        var loyaltyDisc = Math.Round((raw - lineDisc) * tierDiscPct, 2, MidpointRounding.AwayFromZero);
        var payDisc = Math.Round((raw - lineDisc - loyaltyDisc) * paymentDiscPct, 2, MidpointRounding.AwayFromZero);
        var preTax = raw - lineDisc - loyaltyDisc - payDisc;
        var tax = Math.Round(taxableNet * TaxRate.Value, 2, MidpointRounding.AwayFromZero);
        var total = Math.Round(preTax + tax, 2, MidpointRounding.AwayFromZero);

        // Publish to reactive outputs (triggers subscribers if changed)
        Subtotal.Value = raw;
        LineItemDiscounts.Value = lineDisc;
        LoyaltyDiscount.Value = loyaltyDisc;
        PaymentDiscount.Value = payDisc;
        Tax.Value = tax;
        Total.Value = total;

        // UI-ish dependents
        CanCheckout.Value = total > 0 && Payment.Value != PaymentKind.None;
        DiscountBadge.Value = (loyaltyDisc + payDisc) > 0 ? $"You saved {(loyaltyDisc + payDisc):C}" : null;

        // Optional name-based notifications for UIs that listen by name
        var p1 = nameof(Subtotal); _notify(in p1);
        var p2 = nameof(LineItemDiscounts); _notify(in p2);
        var p3 = nameof(LoyaltyDiscount); _notify(in p3);
        var p4 = nameof(PaymentDiscount); _notify(in p4);
        var p5 = nameof(Tax); _notify(in p5);
        var p6 = nameof(Total); _notify(in p6);
        var p7 = nameof(CanCheckout); _notify(in p7);
        var p8 = nameof(DiscountBadge); _notify(in p8);
    }

    // Convenience API
    /// <summary>Add a line item.</summary>
    public void AddItem(LineItem item) => Items.Add(item);

    /// <summary>Remove a line item.</summary>
    /// <returns><see langword="true"/> if removed; otherwise <see langword="false"/>.</returns>
    public bool RemoveItem(LineItem item) => Items.Remove(item);

    /// <summary>Set the loyalty tier.</summary>
    public void SetTier(LoyaltyTier tier) => Tier.Value = tier;

    /// <summary>Set the payment method.</summary>
    public void SetPayment(PaymentKind kind) => Payment.Value = kind;

    /// <summary>Set the tax rate.</summary>
    public void SetTaxRate(decimal rate) => TaxRate.Value = rate;
}

/// <summary>
/// Minimal reactive ViewModel sample to demonstrate dependent properties and control enablement.
/// </summary>
public sealed class ProfileViewModel
{
    /// <summary>First name input.</summary>
    public ObservableVar<string?> FirstName { get; } = new();

    /// <summary>Last name input.</summary>
    public ObservableVar<string?> LastName { get; } = new();

    /// <summary>Computed full name.</summary>
    public ObservableVar<string> FullName { get; } = new(string.Empty);

    /// <summary>Whether saving is currently allowed.</summary>
    public ObservableVar<bool> CanSave { get; } = new();

    /// <summary>Name-based change hub for UI bindings listening by property name.</summary>
    public PropertyChangedHub PropertyChanged { get; } = new();

    private readonly Observer<string>.Handler _notify;

    /// <summary>Create the view model and wire reactive recompute behavior.</summary>
    public ProfileViewModel()
    {
        _notify = (in p) => PropertyChanged.Raise(p);

        FirstName.Subscribe((_, _) => Recompute());
        LastName.Subscribe((_, _) => Recompute());
        Recompute();
    }

    private void Recompute()
    {
        var fn = FirstName.Value?.Trim();
        var ln = LastName.Value?.Trim();
        var full = string.Join(' ', new[] { fn, ln }.Where(s => !string.IsNullOrWhiteSpace(s)));
        FullName.Value = full;
        CanSave.Value = !string.IsNullOrWhiteSpace(fn) && !string.IsNullOrWhiteSpace(ln);
        var pFull = nameof(FullName); _notify(in pFull);
        var pSave = nameof(CanSave); _notify(in pSave);
    }
}
