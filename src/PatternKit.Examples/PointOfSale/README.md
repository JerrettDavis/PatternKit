# Point of Sale Payment Processing - Decorator Pattern Example

## Overview

This example demonstrates how to use **PatternKit's Decorator pattern** to build a flexible, composable Point of Sale (POS) payment processing system. The decorator pattern allows you to layer different payment processing concerns (tax calculation, discounts, loyalty programs, rounding) on top of a base payment processor without complex inheritance hierarchies.

## Why Use Decorators for POS Systems?

In a real-world POS system, you need to:
- Calculate taxes based on location
- Apply various discount types (promotional, loyalty, employee)
- Handle different rounding strategies (cash vs. card)
- Log transactions for audit trails
- Calculate loyalty points
- Support conditional logic (birthday specials, regional promotions)

**Without decorators**, you'd need:
- Multiple payment processor classes for each combination
- Complex if/else chains
- Tight coupling between concerns
- Difficulty adding new features

**With decorators**, you get:
- Single responsibility for each decorator
- Composable, reusable components
- Easy to add/remove/reorder features
- Clear separation of concerns

## Key Concepts

### The Core Component

The base payment processor simply calculates the subtotal:

```csharp
private static PaymentReceipt ProcessBasicPayment(PurchaseOrder order)
{
    var subtotal = order.Items.Sum(item => item.UnitPrice * item.Quantity);
    // ... create basic receipt
    return receipt;
}
```

### Decorator Types Used

1. **Before Decorators** - Validate input before processing
2. **After Decorators** - Transform the receipt after base processing
3. **Around Decorators** - Wrap the entire process (logging, transactions)

## Examples Included

### 1. Simple Small Business Processor

Perfect for businesses with straightforward tax-only requirements:

```csharp
var processor = PaymentProcessorDemo.CreateSimpleProcessor();
// Applies: Tax Calculation only
```

### 2. Standard Retail Processor

Most common retail scenario with tax and rounding:

```csharp
var processor = PaymentProcessorDemo.CreateStandardRetailProcessor();
// Applies: Tax → Banker's Rounding
```

### 3. Full E-commerce Processor

Complete featured processor with promotions and loyalty:

```csharp
var processor = PaymentProcessorDemo.CreateEcommerceProcessor(promotions);
// Applies: Validation → Promotions → Loyalty Discount → Tax → 
//          Loyalty Points → Rounding → Audit Logging
```

### 4. Cash Register with Employee Discount

Physical store with employee benefits and nickel rounding:

```csharp
var processor = PaymentProcessorDemo.CreateCashRegisterProcessor();
// Applies: Employee Discount → Tax → Nickel Rounding → Transaction Log
```

### 5. Birthday Special Processor

Demonstrates **conditional decorators** - decorators applied dynamically:

```csharp
var processor = PaymentProcessorDemo.CreateBirthdaySpecialProcessor(order);
// Applies: Tax → [Birthday Discount if applicable] → 
//          [Loyalty if member] → Rounding
```

## Running the Demo

```csharp
using PatternKit.Examples.PointOfSale;

// Run all scenarios
Demo.Run();
```

**Output includes:**
- 6 different payment scenarios
- Detailed receipts with line items
- Applied discounts and promotions
- Tax calculations
- Loyalty points earned
- Processing logs showing which decorators ran

## Decorator Deep Dive

### Before Decorator: Validation

Validates order before any processing occurs:

```csharp
.Before(ValidateOrder)

private static PurchaseOrder ValidateOrder(PurchaseOrder order)
{
    if (order.Items.Count == 0)
        throw new InvalidOperationException("Order must contain at least one item");
    return order;
}
```

**Why use Before?** Fail fast - don't waste CPU on invalid orders.

### After Decorator: Tax Calculation

Modifies the receipt after base processing:

```csharp
.After(ApplyTaxCalculation)

private static PaymentReceipt ApplyTaxCalculation(
    PurchaseOrder order, 
    PaymentReceipt receipt)
{
    // Calculate tax based on location
    var totalTax = CalculateTax(order, receipt);
    
    return receipt with 
    { 
        TaxAmount = totalTax,
        FinalTotal = receipt.Subtotal - receipt.DiscountAmount + totalTax
    };
}
```

**Why use After?** The receipt exists and we can modify it based on order context.

### After Decorator: Promotional Discounts

Shows how to create decorator factories:

```csharp
.After(ApplyPromotionalDiscounts(activePromotions))

private static AfterTransform ApplyPromotionalDiscounts(
    List<PromotionConfig> promotions)
{
    return (order, receipt) =>
    {
        // Apply valid promotions
        foreach (var promo in promotions.Where(p => p.IsValid(order.OrderDate)))
        {
            // ... apply discount logic
        }
        return updatedReceipt;
    };
}
```

**Why use a factory?** Captures configuration (promotions) while creating the decorator.

### After Decorator: Rounding Strategies

Different rounding for different payment methods:

```csharp
.After(ApplyRounding(RoundingStrategy.ToNickel))

private static AfterTransform ApplyRounding(RoundingStrategy strategy)
{
    return (order, receipt) =>
    {
        var rounded = strategy switch
        {
            RoundingStrategy.Bankers => Math.Round(total, 2, MidpointRounding.ToEven),
            RoundingStrategy.ToNickel => Math.Round(total * 20) / 20,
            // ... other strategies
        };
        return receipt with { FinalTotal = rounded };
    };
}
```

**Why parametrize?** Same decorator logic, different behavior based on config.

### Around Decorator: Audit Logging

Wraps the entire process with logging:

```csharp
.Around(AddAuditLogging)

private static PaymentReceipt AddAuditLogging(
    PurchaseOrder order,
    Component next)
{
    var startTime = DateTime.UtcNow;
    Console.WriteLine($"[AUDIT] Starting payment for {order.OrderId}");
    
    try
    {
        var receipt = next(order);  // Execute the pipeline
        var elapsed = DateTime.UtcNow - startTime;
        Console.WriteLine($"[AUDIT] Completed in {elapsed.TotalMilliseconds}ms");
        return receipt;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[AUDIT] Failed: {ex.Message}");
        throw;
    }
}
```

**Why use Around?** Full control over execution, can log before/after, handle errors.

## Real-World Applications

### Scenario: Regional Store Chain

Different stores need different processing rules:

```csharp
public Decorator<PurchaseOrder, PaymentReceipt> CreateProcessorForStore(
    string storeId)
{
    var builder = Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
        .After(ApplyTaxCalculation);
    
    // California stores: apply redemption value tax
    if (storeId.StartsWith("CA-"))
        builder = builder.After(ApplyCRVTax);
    
    // Canadian stores: nickel rounding
    if (storeId.StartsWith("CAN-"))
        builder = builder.After(ApplyRounding(RoundingStrategy.ToNickel));
    
    return builder.Build();
}
```

### Scenario: Seasonal Promotions

Temporarily add promotional decorators:

```csharp
public Decorator<PurchaseOrder, PaymentReceipt> CreateHolidayProcessor()
{
    var builder = CreateStandardProcessor();
    
    // Only during November-December
    if (DateTime.UtcNow.Month >= 11)
    {
        builder = builder
            .After(ApplyHolidayBonus)
            .After(ApplyGiftWrapping);
    }
    
    return builder.Build();
}
```

### Scenario: A/B Testing

Test different discount strategies:

```csharp
public Decorator<PurchaseOrder, PaymentReceipt> CreateProcessorForCustomer(
    string customerId)
{
    var isTestGroup = IsInTestGroup(customerId);
    
    return Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
        .After(ApplyTaxCalculation)
        .After(isTestGroup 
            ? ApplyNewDiscountAlgorithm 
            : ApplyStandardDiscount)
        .Build();
}
```

## Benefits Demonstrated

✅ **Flexibility**: Easy to add/remove/reorder processing steps  
✅ **Reusability**: Each decorator is self-contained and reusable  
✅ **Testability**: Test each decorator independently  
✅ **Maintainability**: Single responsibility - each decorator does one thing  
✅ **Composability**: Mix and match decorators for different scenarios  
✅ **Readability**: Clear pipeline of transformations  

## Learning Points for Developers

### For Novices

1. **Start simple**: Begin with a base component, add one decorator
2. **Understand order**: Decorators apply in registration order
3. **Use Before/After**: Most common cases are input/output transformation
4. **Factories for config**: Use factory methods to configure decorators

### For Experienced Developers

1. **Decorator vs. Pipeline**: Decorators wrap, pipelines chain
2. **Immutability**: Use `with` expressions for clean transformations
3. **Performance**: Build once, execute many times
4. **Conditional composition**: Build decorators dynamically based on context
5. **Audit trails**: Around decorators perfect for cross-cutting concerns

## Comparison with Other Patterns

| Pattern | When to Use | Key Difference |
|---------|-------------|----------------|
| **Decorator** | Layer enhancements on component | Wraps with same interface |
| **Chain of Responsibility** | Stop-on-match processing | First handler wins |
| **Pipeline** | Sequential transformations | Each step different input/output |
| **Strategy** | Pluggable algorithms | Choose one of many |

## Extension Ideas

Try implementing these yourself to practice:

1. **Tip Calculator**: Add decorator for automatic tip calculation
2. **Split Payment**: Decorator to divide total across multiple payment methods
3. **Gift Cards**: Apply gift card balance before charging credit card
4. **Store Credit**: Handle store credit redemption
5. **Installment Plans**: Calculate installment amounts
6. **Returns Processing**: Negative amounts with refund logic

## See Also

- [Decorator Pattern Documentation](../../../docs/patterns/structural/decorator/decorator.md)
- [PatternKit.Structural.Decorator API](xref:PatternKit.Structural.Decorator)
- [Strategy Pattern Example](../Strategies/README.md)

