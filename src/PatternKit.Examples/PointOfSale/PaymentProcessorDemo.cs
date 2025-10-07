using PatternKit.Structural.Decorator;

namespace PatternKit.Examples.PointOfSale;

/// <summary>
/// Demonstrates fluent decorator pattern for building a Point of Sale payment processing pipeline.
/// This example shows how decorators can layer functionality like tax calculation, discounts,
/// loyalty programs, and rounding strategies on top of a base payment processor.
/// </summary>
public static class PaymentProcessorDemo
{
    /// <summary>
    /// Creates a basic payment processor that calculates subtotal from line items.
    /// This is the core component that all decorators will wrap.
    /// </summary>
    private static PaymentReceipt ProcessBasicPayment(PurchaseOrder order)
    {
        var subtotal = order.Items.Sum(item => item.UnitPrice * item.Quantity);
        
        var lineItems = order.Items.Select(item => new ReceiptLineItem
        {
            ProductName = item.ProductName,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            LineTotal = item.UnitPrice * item.Quantity
        }).ToList();

        return new PaymentReceipt
        {
            OrderId = order.OrderId,
            Subtotal = subtotal,
            TaxAmount = 0m,
            DiscountAmount = 0m,
            LoyaltyPointsEarned = 0m,
            FinalTotal = subtotal,
            AppliedPromotions = [],
            LineItems = lineItems
        };
    }

    /// <summary>
    /// Example 1: Simple payment processor with only tax calculation.
    /// Perfect for small businesses with straightforward tax requirements.
    /// </summary>
    public static Decorator<PurchaseOrder, PaymentReceipt> CreateSimpleProcessor()
    {
        return Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
            .After(ApplyTaxCalculation)
            .Build();
    }

    /// <summary>
    /// Example 2: Standard retail processor with tax and basic rounding.
    /// Suitable for most retail scenarios.
    /// </summary>
    public static Decorator<PurchaseOrder, PaymentReceipt> CreateStandardRetailProcessor()
    {
        return Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
            .After(ApplyRounding(RoundingStrategy.Bankers))
            .After(ApplyTaxCalculation)
            .Build();
    }

    /// <summary>
    /// Example 3: Full-featured e-commerce processor with loyalty program.
    /// Demonstrates complex decorator chaining with multiple concerns.
    /// </summary>
    public static Decorator<PurchaseOrder, PaymentReceipt> CreateEcommerceProcessor(
        List<PromotionConfig> activePromotions)
    {
        return Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
            .Before(ValidateOrder)
            .After(ApplyRounding(RoundingStrategy.Bankers))
            .After(CalculateLoyaltyPoints)
            .After(ApplyTaxCalculation)
            .After(ApplyLoyaltyDiscount)
            .After(ApplyPromotionalDiscounts(activePromotions))
            .Around(AddAuditLogging)
            .Build();
    }

    /// <summary>
    /// Example 4: Cash register processor with nickel rounding.
    /// Common in countries that have eliminated penny currency.
    /// </summary>
    public static Decorator<PurchaseOrder, PaymentReceipt> CreateCashRegisterProcessor()
    {
        return Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
            .After(ApplyRounding(RoundingStrategy.ToNickel))
            .After(ApplyTaxCalculation)
            .After(ApplyEmployeeDiscount)
            .Around(AddTransactionLogging)
            .Build();
    }

    /// <summary>
    /// Example 5: Birthday special processor with conditional decorators.
    /// Shows how to dynamically apply decorators based on business rules.
    /// </summary>
    public static Decorator<PurchaseOrder, PaymentReceipt> CreateBirthdaySpecialProcessor(
        PurchaseOrder order)
    {
        var builder = Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
            .After(ApplyRounding(RoundingStrategy.Bankers));

        // Calculate loyalty points if applicable (executes before rounding)
        if (!string.IsNullOrEmpty(order.Customer.LoyaltyTier))
        {
            builder = builder.After(CalculateLoyaltyPoints);
        }

        // Always apply tax after discounts (executes before loyalty points)
        builder = builder.After(ApplyTaxCalculation);

        // Apply loyalty benefits if customer is a member (executes before tax)
        if (!string.IsNullOrEmpty(order.Customer.LoyaltyTier))
        {
            builder = builder.After(ApplyLoyaltyDiscount);
        }

        // Apply birthday discount if it's customer's birthday month (executes before loyalty)
        if (IsBirthdayMonth(order.Customer))
        {
            builder = builder.After(ApplyBirthdayDiscount);
        }

        return builder.Build();
    }

    #region Decorator Functions

    /// <summary>
    /// Validates the order before processing (Before decorator).
    /// </summary>
    private static PurchaseOrder ValidateOrder(PurchaseOrder order)
    {
        if (order.Items.Count == 0)
            throw new InvalidOperationException("Order must contain at least one item");

        if (order.Items.Any(i => i.Quantity <= 0))
            throw new InvalidOperationException("Item quantities must be positive");

        if (order.Items.Any(i => i.UnitPrice < 0))
            throw new InvalidOperationException("Item prices cannot be negative");

        return order;
    }

    /// <summary>
    /// Calculates and applies sales tax (After decorator).
    /// Respects tax-exempt items and uses store location tax rates.
    /// </summary>
    private static PaymentReceipt ApplyTaxCalculation(PurchaseOrder order, PaymentReceipt receipt)
    {
        // Calculate the tax base: subtotal minus any order-level discounts
        var taxableSubtotal = receipt.Subtotal - receipt.DiscountAmount;
        
        // Calculate tax proportionally on taxable items only
        var totalTax = 0m;
        var taxableItemsTotal = 0m;
        var updatedLineItems = new List<ReceiptLineItem>();

        // First pass: determine total of taxable items
        for (int i = 0; i < order.Items.Count; i++)
        {
            var item = order.Items[i];
            var lineItem = receipt.LineItems[i];
            
            if (!item.IsTaxExempt)
            {
                taxableItemsTotal += lineItem.LineTotal - lineItem.Discount;
            }
        }

        // Second pass: calculate tax proportionally for each taxable item
        for (int i = 0; i < order.Items.Count; i++)
        {
            var item = order.Items[i];
            var lineItem = receipt.LineItems[i];

            if (!item.IsTaxExempt && taxableItemsTotal > 0)
            {
                // Calculate this item's proportion of the total taxable amount
                var itemTaxableAmount = lineItem.LineTotal - lineItem.Discount;
                var itemProportion = itemTaxableAmount / taxableItemsTotal;
                
                // Apply order-level discounts proportionally
                var itemDiscountedAmount = taxableSubtotal * itemProportion;
                
                var stateTax = itemDiscountedAmount * order.Store.StateTaxRate;
                var localTax = itemDiscountedAmount * order.Store.LocalTaxRate;
                var itemTax = stateTax + localTax;

                totalTax += itemTax;

                updatedLineItems.Add(lineItem with { Tax = itemTax });
            }
            else
            {
                updatedLineItems.Add(lineItem);
            }
        }

        receipt.ProcessingLog.Add($"Tax calculated: ${totalTax:F2} (State: {order.Store.StateTaxRate:P}, Local: {order.Store.LocalTaxRate:P})");

        return receipt with
        {
            TaxAmount = totalTax,
            FinalTotal = receipt.Subtotal - receipt.DiscountAmount + totalTax,
            LineItems = updatedLineItems
        };
    }

    /// <summary>
    /// Applies promotional discounts from active campaigns (After decorator).
    /// </summary>
    private static Decorator<PurchaseOrder, PaymentReceipt>.AfterTransform ApplyPromotionalDiscounts(
        List<PromotionConfig> promotions)
    {
        return (order, receipt) =>
        {
            var totalDiscount = receipt.DiscountAmount;
            var appliedPromotions = new List<string>(receipt.AppliedPromotions);
            var updatedLineItems = new List<ReceiptLineItem>(receipt.LineItems);

            foreach (var promo in promotions.Where(p => p.IsValid(order.OrderDate)))
            {
                // Check minimum purchase requirement
                if (receipt.Subtotal < promo.MinimumPurchase)
                    continue;

                // Apply category-specific or order-wide discount
                if (!string.IsNullOrEmpty(promo.ApplicableCategory))
                {
                    for (int i = 0; i < order.Items.Count; i++)
                    {
                        var item = order.Items[i];
                        if (item.Category == promo.ApplicableCategory)
                        {
                            var lineItem = updatedLineItems[i];
                            var itemDiscount = promo.DiscountPercent > 0
                                ? lineItem.LineTotal * promo.DiscountPercent
                                : promo.DiscountAmount;

                            updatedLineItems[i] = lineItem with
                            {
                                Discount = lineItem.Discount + itemDiscount
                            };
                            totalDiscount += itemDiscount;
                        }
                    }
                }
                else
                {
                    // Order-wide discount
                    var orderDiscount = promo.DiscountPercent > 0
                        ? receipt.Subtotal * promo.DiscountPercent
                        : promo.DiscountAmount;
                    totalDiscount += orderDiscount;
                }

                appliedPromotions.Add(promo.Description);
                receipt.ProcessingLog.Add($"Promotion applied: {promo.Description} (-${totalDiscount - receipt.DiscountAmount:F2})");
            }

            return receipt with
            {
                DiscountAmount = totalDiscount,
                FinalTotal = receipt.Subtotal - totalDiscount + receipt.TaxAmount,
                AppliedPromotions = appliedPromotions,
                LineItems = updatedLineItems
            };
        };
    }

    /// <summary>
    /// Applies loyalty tier discounts (After decorator).
    /// </summary>
    private static PaymentReceipt ApplyLoyaltyDiscount(PurchaseOrder order, PaymentReceipt receipt)
    {
        var discountPercent = order.Customer.LoyaltyTier switch
        {
            "Silver" => 0.05m,    // 5% off
            "Gold" => 0.10m,      // 10% off
            "Platinum" => 0.15m,  // 15% off
            _ => 0m
        };

        if (discountPercent == 0m)
            return receipt;

        var loyaltyDiscount = receipt.Subtotal * discountPercent;
        var totalDiscount = receipt.DiscountAmount + loyaltyDiscount;

        receipt.ProcessingLog.Add($"Loyalty discount ({order.Customer.LoyaltyTier}): -{discountPercent:P} (-${loyaltyDiscount:F2})");

        return receipt with
        {
            DiscountAmount = totalDiscount,
            FinalTotal = receipt.Subtotal - totalDiscount + receipt.TaxAmount,
            AppliedPromotions = receipt.AppliedPromotions.Concat([$"{order.Customer.LoyaltyTier} Member Discount"]).ToList()
        };
    }

    /// <summary>
    /// Applies employee discount (After decorator).
    /// </summary>
    private static PaymentReceipt ApplyEmployeeDiscount(PurchaseOrder order, PaymentReceipt receipt)
    {
        if (!order.Customer.IsEmployee)
            return receipt;

        var employeeDiscount = receipt.Subtotal * 0.20m; // 20% employee discount
        var totalDiscount = receipt.DiscountAmount + employeeDiscount;

        receipt.ProcessingLog.Add($"Employee discount: -20% (-${employeeDiscount:F2})");

        return receipt with
        {
            DiscountAmount = totalDiscount,
            FinalTotal = receipt.Subtotal - totalDiscount + receipt.TaxAmount,
            AppliedPromotions = receipt.AppliedPromotions.Concat(["Employee Discount"]).ToList()
        };
    }

    /// <summary>
    /// Applies birthday month discount (After decorator).
    /// </summary>
    private static PaymentReceipt ApplyBirthdayDiscount(PurchaseOrder order, PaymentReceipt receipt)
    {
        var birthdayDiscount = Math.Min(25m, receipt.Subtotal * 0.10m); // 10% off, max $25
        var totalDiscount = receipt.DiscountAmount + birthdayDiscount;

        receipt.ProcessingLog.Add($"Birthday discount: -${birthdayDiscount:F2}");

        return receipt with
        {
            DiscountAmount = totalDiscount,
            FinalTotal = receipt.Subtotal - totalDiscount + receipt.TaxAmount,
            AppliedPromotions = receipt.AppliedPromotions.Concat(["Birthday Month Special"]).ToList()
        };
    }

    /// <summary>
    /// Calculates loyalty points earned from purchase (After decorator).
    /// </summary>
    private static PaymentReceipt CalculateLoyaltyPoints(PurchaseOrder order, PaymentReceipt receipt)
    {
        if (string.IsNullOrEmpty(order.Customer.LoyaltyTier))
            return receipt;

        var pointsMultiplier = order.Customer.LoyaltyTier switch
        {
            "Silver" => 1.0m,
            "Gold" => 1.5m,
            "Platinum" => 2.0m,
            _ => 1.0m
        };

        // Earn 1 point per dollar spent (after discounts, before tax)
        var pointsBase = receipt.Subtotal - receipt.DiscountAmount;
        var pointsEarned = Math.Floor(pointsBase * pointsMultiplier);

        receipt.ProcessingLog.Add($"Loyalty points earned: {pointsEarned} ({pointsMultiplier}x multiplier)");

        return receipt with { LoyaltyPointsEarned = pointsEarned };
    }

    /// <summary>
    /// Applies rounding strategy to final total (After decorator).
    /// </summary>
    private static Decorator<PurchaseOrder, PaymentReceipt>.AfterTransform ApplyRounding(
        RoundingStrategy strategy)
    {
        return (_, receipt) =>
        {
            var originalTotal = receipt.FinalTotal;
            var roundedTotal = strategy switch
            {
                RoundingStrategy.Bankers => Math.Round(receipt.FinalTotal, 2, MidpointRounding.ToEven),
                RoundingStrategy.Up => Math.Ceiling(receipt.FinalTotal * 100) / 100,
                RoundingStrategy.Down => Math.Floor(receipt.FinalTotal * 100) / 100,
                RoundingStrategy.ToNickel => Math.Round(receipt.FinalTotal * 20, MidpointRounding.AwayFromZero) / 20,
                RoundingStrategy.ToDime => Math.Round(receipt.FinalTotal * 10, MidpointRounding.AwayFromZero) / 10,
                _ => receipt.FinalTotal
            };

            if (Math.Abs(originalTotal - roundedTotal) > 0.0001m)
            {
                receipt.ProcessingLog.Add($"Rounding applied ({strategy}): ${originalTotal:F4} → ${roundedTotal:F2}");
            }

            return receipt with { FinalTotal = roundedTotal };
        };
    }

    /// <summary>
    /// Adds audit logging around the entire payment processing (Around decorator).
    /// </summary>
    private static PaymentReceipt AddAuditLogging(PurchaseOrder order, 
        Decorator<PurchaseOrder, PaymentReceipt>.Component next)
    {
        var startTime = DateTime.UtcNow;
        
        Console.WriteLine($"[AUDIT] Starting payment processing for order {order.OrderId}");
        Console.WriteLine($"[AUDIT] Customer: {order.Customer.CustomerId} (Tier: {order.Customer.LoyaltyTier ?? "None"})");
        Console.WriteLine($"[AUDIT] Items: {order.Items.Count}, Store: {order.Store.StoreId}");

        try
        {
            var receipt = next(order);
            var elapsed = DateTime.UtcNow - startTime;

            Console.WriteLine($"[AUDIT] Payment processed successfully in {elapsed.TotalMilliseconds:F2}ms");
            Console.WriteLine($"[AUDIT] Final total: ${receipt.FinalTotal:F2}");
            
            receipt.ProcessingLog.Insert(0, $"Payment processing started at {startTime:yyyy-MM-dd HH:mm:ss} UTC");
            receipt.ProcessingLog.Add($"Payment processing completed in {elapsed.TotalMilliseconds:F0}ms");

            return receipt;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUDIT] Payment processing failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Adds transaction logging for cash register (Around decorator).
    /// </summary>
    private static PaymentReceipt AddTransactionLogging(PurchaseOrder order,
        Decorator<PurchaseOrder, PaymentReceipt>.Component next)
    {
        var transactionId = Guid.NewGuid().ToString("N")[..8];
        
        var receipt = next(order);
        
        receipt.ProcessingLog.Add($"Transaction ID: {transactionId}");
        receipt.ProcessingLog.Add($"Register: {order.Store.StoreId}");
        receipt.ProcessingLog.Add($"Transaction completed: {transactionId}");
        
        return receipt;
    }

    private static bool IsBirthdayMonth(CustomerInfo customer)
    {
        return customer.BirthDate.HasValue &&
               customer.BirthDate.Value.Month == DateTime.UtcNow.Month;
    }

    #endregion
}
