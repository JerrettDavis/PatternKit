namespace PatternKit.Examples.PointOfSale;

/// <summary>
/// Represents a purchase order with line items ready for payment processing.
/// </summary>
public sealed class PurchaseOrder
{
    public required string OrderId { get; init; }
    public required List<OrderLineItem> Items { get; init; }
    public required CustomerInfo Customer { get; init; }
    public required StoreLocation Store { get; init; }
    public DateTime OrderDate { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// A single line item in an order.
/// </summary>
public sealed record OrderLineItem
{
    public required string Sku { get; init; }
    public required string ProductName { get; init; }
    public required decimal UnitPrice { get; init; }
    public required int Quantity { get; init; }
    public string? Category { get; init; }
    public bool IsTaxExempt { get; init; }
}

/// <summary>
/// Customer information for loyalty programs and personalized pricing.
/// </summary>
public sealed record CustomerInfo
{
    public required string CustomerId { get; init; }
    public string? LoyaltyTier { get; init; }  // null, "Silver", "Gold", "Platinum"
    public int LoyaltyPoints { get; init; }
    public bool IsEmployee { get; init; }
    public DateTime? BirthDate { get; init; }
}

/// <summary>
/// Store location information for tax jurisdiction and regional pricing.
/// </summary>
public sealed class StoreLocation
{
    public required string StoreId { get; init; }
    public required string State { get; init; }
    public required string Country { get; init; }
    public decimal LocalTaxRate { get; init; }
    public decimal StateTaxRate { get; init; }
}

/// <summary>
/// The final payment receipt with all calculations applied.
/// </summary>
public sealed record PaymentReceipt
{
    public required string OrderId { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal TaxAmount { get; init; }
    public required decimal DiscountAmount { get; init; }
    public required decimal LoyaltyPointsEarned { get; init; }
    public required decimal FinalTotal { get; init; }
    public required List<string> AppliedPromotions { get; init; }
    public required List<ReceiptLineItem> LineItems { get; init; }

    /// <summary>
    /// Audit trail showing which decorators modified the receipt.
    /// </summary>
    public List<string> ProcessingLog { get; } = new();
}

/// <summary>
/// Individual line item on the receipt with all adjustments.
/// </summary>
public sealed record ReceiptLineItem
{
    public required string ProductName { get; init; }
    public required int Quantity { get; init; }
    public required decimal UnitPrice { get; init; }
    public decimal Discount { get; init; }
    public decimal Tax { get; init; }
    public required decimal LineTotal { get; init; }
}

/// <summary>
/// Rounding strategy for currency calculations.
/// </summary>
public enum RoundingStrategy
{
    /// <summary>Standard banker's rounding (to even).</summary>
    Bankers,

    /// <summary>Always round up (ceiling).</summary>
    Up,

    /// <summary>Always round down (floor).</summary>
    Down,

    /// <summary>Round to nearest nickel (0.05).</summary>
    ToNickel,

    /// <summary>Round to nearest dime (0.10).</summary>
    ToDime
}

/// <summary>
/// Configuration for promotional campaigns.
/// </summary>
public sealed class PromotionConfig
{
    public required string PromotionCode { get; init; }
    public required string Description { get; init; }
    public decimal DiscountPercent { get; init; }
    public decimal DiscountAmount { get; init; }
    public string? ApplicableCategory { get; init; }
    public decimal MinimumPurchase { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }

    public bool IsValid(DateTime date) =>
        (!ValidFrom.HasValue || date >= ValidFrom.Value) &&
        (!ValidUntil.HasValue || date <= ValidUntil.Value);
}
