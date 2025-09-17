namespace PatternKit.Examples.Pricing;

public enum PaymentKind { Cash, StoreGiftCard, StoreCreditCard, CreditCard }

public sealed record Location(string Region, string? Country = null, string? State = null);

public sealed record Sku(
    string Id,
    string Name,
    string? Category = null,
    string? BundleKey = null,
    string[]? Tags = null)
{
    public bool HasTag(string tag) => Tags is not null && Array.IndexOf(Tags, tag) >= 0;
}

public sealed class LineItem
{
    public required Sku Sku { get; init; }
    public required int Qty { get; init; }

    // Resolved pricing & adjustments
    public decimal BasePrice;           // unit price before discounts/tax
    public decimal UnitDiscount;        // per-unit discount accumulated from item rules/loyalty
    public decimal UnitTax;             // per-unit tax

    // For rounding rules that adjust a specific SKU’s price
    public decimal PriceAdjustment;     // applied to one unit to absorb rounding delta

    public decimal UnitNet => Math.Max(0m, BasePrice - UnitDiscount + UnitTax);
    public decimal LineNet => UnitNet * Qty + PriceAdjustment; // adjustment applied once per line
}

public sealed record LoyaltyMembership(string ProgramCode);

public sealed record Coupon(string Code, decimal Amount, bool Percent = false);

public sealed class PricingContext
{
    public required Location Location { get; init; }
    public required PaymentKind Payment { get; init; }
    public required List<LineItem> Items { get; init; }
    public List<LoyaltyMembership> Loyalty { get; } = new();
    public List<Coupon> Coupons { get; } = new();

    public List<string> Log { get; } = new();
}

public sealed class PricingResult
{
    public decimal Subtotal;     // sum BasePrice*Qty
    public decimal Discounts;    // sum of all discounts
    public decimal Taxes;        // sum of all taxes
    public decimal Total;        // Subtotal - Discounts + Taxes + Adjustments
    public List<string> Log { get; } = new();
}

