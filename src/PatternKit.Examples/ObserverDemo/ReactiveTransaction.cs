using PatternKit.Behavioral.Observer;

namespace PatternKit.Examples.ObserverDemo;

public enum LoyaltyTier { None, Silver, Gold, Platinum }
public enum PaymentKind { None, CreditCard, StoreCard, Cash }

public readonly record struct LineItem(string Sku, int Qty, decimal UnitPrice, decimal? DiscountPct = null, bool Taxable = true)
{
    public decimal Raw => Qty * UnitPrice;
    public decimal LineDiscount => DiscountPct is { } p ? Raw * p : 0m;
    public decimal Net => Raw - LineDiscount;
}

/// <summary>
/// Reactive transaction shows dependent, computed properties updated via Observer-based subscriptions.
/// </summary>
public sealed class ReactiveTransaction
{
    // Inputs (reactive)
    public ObservableList<LineItem> Items { get; } = new();
    public ObservableVar<LoyaltyTier> Tier { get; } = new();
    public ObservableVar<PaymentKind> Payment { get; } = new();
    public ObservableVar<decimal> TaxRate { get; } = new(0.07m);

    // Outputs (reactive vars so consumers can subscribe to precise changes)
    public ObservableVar<decimal> Subtotal { get; } = new();
    public ObservableVar<decimal> LineItemDiscounts { get; } = new();
    public ObservableVar<decimal> LoyaltyDiscount { get; } = new();
    public ObservableVar<decimal> PaymentDiscount { get; } = new();
    public ObservableVar<decimal> Tax { get; } = new();
    public ObservableVar<decimal> Total { get; } = new();

    // UI-ish dependent properties
    public ObservableVar<bool> CanCheckout { get; } = new();
    public ObservableVar<string?> DiscountBadge { get; } = new();

    // Fine-grained change notifications for property names, if needed
    public PropertyChangedHub PropertyChanged { get; } = new();

    private readonly Observer<string>.Handler _notify;

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
            LoyaltyTier.Gold     => 0.07m,
            LoyaltyTier.Silver   => 0.04m,
            _ => 0m
        };

        var paymentDiscPct = Payment.Value switch
        {
            PaymentKind.StoreCard => 0.05m, // in-house card promo
            _ => 0m
        };

        var loyaltyDisc = Math.Round((raw - lineDisc) * tierDiscPct, 2, MidpointRounding.AwayFromZero);
        var payDisc     = Math.Round((raw - lineDisc - loyaltyDisc) * paymentDiscPct, 2, MidpointRounding.AwayFromZero);
        var preTax      = raw - lineDisc - loyaltyDisc - payDisc;
        var tax         = Math.Round(taxableNet * TaxRate.Value, 2, MidpointRounding.AwayFromZero);
        var total       = Math.Round(preTax + tax, 2, MidpointRounding.AwayFromZero);

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
        var p1 = nameof(Subtotal);           _notify(in p1);
        var p2 = nameof(LineItemDiscounts);  _notify(in p2);
        var p3 = nameof(LoyaltyDiscount);    _notify(in p3);
        var p4 = nameof(PaymentDiscount);    _notify(in p4);
        var p5 = nameof(Tax);                _notify(in p5);
        var p6 = nameof(Total);              _notify(in p6);
        var p7 = nameof(CanCheckout);        _notify(in p7);
        var p8 = nameof(DiscountBadge);      _notify(in p8);
    }

    // Convenience API
    public void AddItem(LineItem item) => Items.Add(item);
    public bool RemoveItem(LineItem item) => Items.Remove(item);
    public void SetTier(LoyaltyTier tier) => Tier.Value = tier;
    public void SetPayment(PaymentKind kind) => Payment.Value = kind;
    public void SetTaxRate(decimal rate) => TaxRate.Value = rate;
}

/// <summary>
/// Minimal reactive ViewModel sample to demonstrate dependent properties and control enablement.
/// </summary>
public sealed class ProfileViewModel
{
    public ObservableVar<string?> FirstName { get; } = new();
    public ObservableVar<string?> LastName  { get; } = new();
    public ObservableVar<string>  FullName  { get; } = new(string.Empty);
    public ObservableVar<bool>    CanSave   { get; } = new();
    public PropertyChangedHub PropertyChanged { get; } = new();

    private readonly Observer<string>.Handler _notify;

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
        var pSave = nameof(CanSave);  _notify(in pSave);
    }
}
