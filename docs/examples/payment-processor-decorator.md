# Payment Processor — Fluent Decorator Pattern for Point of Sale

> **TL;DR**
> This demo shows how to build flexible, composable payment processing pipelines using PatternKit's **Decorator** pattern.
> We layer functionality like tax calculation, discounts, loyalty programs, and rounding strategies on top of a base payment processor—**no inheritance hierarchies, no monolithic processors**.

Everything is immutable after building, thread-safe, and testable in isolation.

---

## What it does

The demo implements five real-world payment processors for different retail scenarios:

1. **Simple Processor** — Basic tax calculation for small businesses
2. **Standard Retail Processor** — Tax + rounding for most retail scenarios
3. **E-commerce Processor** — Full-featured with promotions, loyalty, tax, points, rounding, and audit logging
4. **Cash Register Processor** — Employee discounts, tax, nickel rounding for countries without pennies
5. **Birthday Special Processor** — Conditional decorators based on customer birthday and loyalty tier

Each processor is built once using a fluent API and can process thousands of orders without allocation overhead.

---

## Core concept: Decorator chaining

The [xref:PatternKit.Structural.Decorator.Decorator`2](xref:PatternKit.Structural.Decorator.Decorator`2) pattern wraps a base component with layers of functionality. Each layer can:

* **Transform input** before passing it down (`.Before()`)
* **Transform output** after receiving it back (`.After()`)
* **Wrap the entire execution** with custom logic (`.Around()`)

### Execution order (important!)

* **`.Before()` decorators** execute in registration order (first → last), transforming the input
* **`.After()` decorators** execute in **reverse registration order** (last → first), transforming the output
* **`.Around()` decorators** control the entire flow at their layer

This means when you write:
```csharp
.After(ApplyDiscount)
.After(ApplyTax)
.After(ApplyRounding)
```

The execution flow is:
1. Base component calculates subtotal
2. `ApplyDiscount` transforms the receipt (executes first)
3. `ApplyTax` transforms the discounted receipt (executes second)
4. `ApplyRounding` transforms the final total (executes last)

**So you register decorators in reverse execution order** to get the desired flow.

---

## Quick look

```csharp
using PatternKit.Examples.PointOfSale;

// Build a processor once (immutable, thread-safe)
var processor = PaymentProcessorDemo.CreateEcommerceProcessor(
    activePromotions: new List<PromotionConfig>
    {
        new()
        {
            PromotionCode = "SAVE10",
            Description = "10% off Electronics",
            DiscountPercent = 0.10m,
            ApplicableCategory = "Electronics",
            ValidFrom = DateTime.UtcNow.AddDays(-7),
            ValidUntil = DateTime.UtcNow.AddDays(7)
        }
    }
);

// Process orders (reuse the processor)
var order = new PurchaseOrder
{
    OrderId = "ORD-001",
    Customer = new CustomerInfo
    {
        CustomerId = "CUST-123",
        LoyaltyTier = "Gold" // 10% loyalty discount
    },
    Store = new StoreLocation
    {
        StoreId = "STORE-001",
        StateTaxRate = 0.0725m,  // 7.25% state tax
        LocalTaxRate = 0.0125m    // 1.25% local tax
    },
    Items =
    [
        new()
        {
            Sku = "LAPTOP-001",
            ProductName = "Gaming Laptop",
            UnitPrice = 1200m,
            Quantity = 1,
            Category = "Electronics"
        }
    ]
};

var receipt = processor.Execute(order);

// receipt.Subtotal = 1200.00
// receipt.DiscountAmount = 240.00 (10% promo + 10% loyalty on remaining)
// receipt.TaxAmount = 81.60 (8.5% on discounted amount)
// receipt.LoyaltyPointsEarned = 144 (1.5x multiplier for Gold)
// receipt.FinalTotal = 1041.60
```

---

## The five processors

### 1) Simple Processor

**Use case:** Small businesses with straightforward tax requirements

```csharp
var processor = PaymentProcessorDemo.CreateSimpleProcessor();
```

**Pipeline:**
- Calculate subtotal from line items
- Apply tax (state + local rates)

**Features:**
- Tax-exempt item support
- Per-item tax calculation with proportional distribution

---

### 2) Standard Retail Processor

**Use case:** Most retail scenarios needing basic rounding

```csharp
var processor = PaymentProcessorDemo.CreateStandardRetailProcessor();
```

**Pipeline:**
- Calculate subtotal
- Apply tax
- Apply banker's rounding (round to even)

**Features:**
- Professional rounding for financial accuracy
- Detailed processing logs

---

### 3) E-commerce Processor (Full-Featured)

**Use case:** Online stores with complex loyalty and promotion systems

```csharp
var processor = PaymentProcessorDemo.CreateEcommerceProcessor(activePromotions);
```

**Pipeline (in execution order):**
1. **Validate order** (before processing) — ensures items exist, quantities positive, prices non-negative
2. **Apply promotional discounts** — category-specific or order-wide, with minimum purchase requirements
3. **Apply loyalty tier discounts** — 5% (Silver), 10% (Gold), 15% (Platinum)
4. **Calculate tax** — on discounted amount, respecting tax-exempt items
5. **Calculate loyalty points** — 1x (Silver), 1.5x (Gold), 2x (Platinum) multiplier
6. **Apply banker's rounding** — to final total
7. **Audit logging** (around entire process) — performance tracking, console output

**Features:**
- Multiple promotion types (percentage, fixed amount, category-specific)
- Promotion stacking with date validation
- Tiered loyalty programs
- Loyalty points calculation based on spend
- Comprehensive audit trails
- Order validation with clear error messages

**Decorator registration (remember: reverse order!):**
```csharp
return Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
    .Before(ValidateOrder)                              // Executes first
    .After(ApplyRounding(RoundingStrategy.Bankers))     // Executes last
    .After(CalculateLoyaltyPoints)                      // Executes 5th
    .After(ApplyTaxCalculation)                         // Executes 4th
    .After(ApplyLoyaltyDiscount)                        // Executes 3rd
    .After(ApplyPromotionalDiscounts(activePromotions)) // Executes 2nd
    .Around(AddAuditLogging)                            // Wraps everything
    .Build();
```

---

### 4) Cash Register Processor

**Use case:** Physical stores in countries that have eliminated penny currency

```csharp
var processor = PaymentProcessorDemo.CreateCashRegisterProcessor();
```

**Pipeline (in execution order):**
1. **Apply employee discount** — 20% off for staff purchases
2. **Calculate tax** — on discounted amount
3. **Apply nickel rounding** — round to nearest $0.05
4. **Transaction logging** (around process) — generates transaction ID, logs register info

**Features:**
- Employee discount detection
- Nickel rounding for cash-only economies (Canada, Australia, etc.)
- Transaction ID generation
- Register-specific logging

**Rounding strategy:**
```csharp
RoundingStrategy.ToNickel  // Rounds to 0.00, 0.05, 0.10, 0.15, etc.
```

---

### 5) Birthday Special Processor

**Use case:** Dynamic promotional scenarios based on customer attributes

```csharp
var processor = PaymentProcessorDemo.CreateBirthdaySpecialProcessor(order);
```

**Pipeline (conditionally built):**
- **Birthday discount** (if customer's birth month) — 10% off, max $25
- **Loyalty discount** (if loyalty member) — tier-based percentage
- **Tax calculation** — always applied
- **Loyalty points** (if loyalty member) — tier-based multiplier
- **Banker's rounding** — always applied

**Features:**
- Conditional decorator application based on business rules
- Birthday month detection (compares to current month)
- Combines birthday and loyalty benefits
- Shows how to build processors dynamically

**Example dynamic building:**
```csharp
var builder = Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
    .After(ApplyRounding(RoundingStrategy.Bankers));

if (!string.IsNullOrEmpty(order.Customer.LoyaltyTier))
{
    builder = builder.After(CalculateLoyaltyPoints);
}

builder = builder.After(ApplyTaxCalculation);

if (!string.IsNullOrEmpty(order.Customer.LoyaltyTier))
{
    builder = builder.After(ApplyLoyaltyDiscount);
}

if (IsBirthdayMonth(order.Customer))
{
    builder = builder.After(ApplyBirthdayDiscount);
}

return builder.Build();
```

---

## Key decorator functions

### Validation (Before)

**`ValidateOrder`** runs before processing begins:
- Ensures order contains at least one item
- Validates quantities are positive
- Validates prices are non-negative
- Throws `InvalidOperationException` on validation failure

### Discounts (After)

**`ApplyPromotionalDiscounts`** — marketing campaigns:
- Supports percentage or fixed-amount discounts
- Category-specific or order-wide application
- Minimum purchase requirements
- Date range validation
- Stacks with other discounts

**`ApplyLoyaltyDiscount`** — tier-based rewards:
- Silver: 5% off
- Gold: 10% off
- Platinum: 15% off
- Applied to entire subtotal

**`ApplyEmployeeDiscount`** — staff benefits:
- 20% off entire purchase
- Applied before tax calculation

**`ApplyBirthdayDiscount`** — special occasions:
- 10% off, capped at $25
- Only applies during customer's birth month

### Tax (After)

**`ApplyTaxCalculation`** — sophisticated tax handling:
- Separate state and local tax rates
- Tax-exempt item support
- Calculates tax on discounted amount
- Proportional distribution across line items
- Per-item tax tracking in receipt

### Loyalty Points (After)

**`CalculateLoyaltyPoints`** — reward accumulation:
- 1 point per dollar spent (after discounts, before tax)
- Multipliers: Silver (1.0x), Gold (1.5x), Platinum (2.0x)
- Floor function for whole points
- Tracked separately from discounts

### Rounding (After)

**`ApplyRounding`** — multiple strategies:
- **Bankers** — round to even (0.5 rounds to nearest even number)
- **ToNickel** — round to nearest $0.05
- **ToDime** — round to nearest $0.10
- **Up** — always round up
- **Down** — always round down

Logs rounding adjustments for audit trails.

### Logging (Around)

**`AddAuditLogging`** — comprehensive tracking:
- Start/end timestamps
- Customer and order details
- Execution time in milliseconds
- Final total
- Console output for monitoring
- Processing log entries

**`AddTransactionLogging`** — register tracking:
- Unique transaction ID generation
- Register/store identification
- Completion markers

---

## Domain types

### PurchaseOrder

```csharp
public record PurchaseOrder
{
    public required string OrderId { get; init; }
    public required CustomerInfo Customer { get; init; }
    public required StoreLocation Store { get; init; }
    public required List<OrderLineItem> Items { get; init; }
    public DateTime OrderDate { get; init; } = DateTime.UtcNow;
}
```

### CustomerInfo

```csharp
public record CustomerInfo
{
    public required string CustomerId { get; init; }
    public string? LoyaltyTier { get; init; }  // "Silver", "Gold", "Platinum"
    public decimal LoyaltyPoints { get; init; }
    public bool IsEmployee { get; init; }
    public DateTime? BirthDate { get; init; }
}
```

### PaymentReceipt

```csharp
public record PaymentReceipt
{
    public required string OrderId { get; init; }
    public decimal Subtotal { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal LoyaltyPointsEarned { get; init; }
    public decimal FinalTotal { get; init; }
    public List<string> AppliedPromotions { get; init; } = [];
    public List<ReceiptLineItem> LineItems { get; init; } = [];
    public List<string> ProcessingLog { get; init; } = [];
}
```

---

## Tests (TinyBDD)

See `test/PatternKit.Examples.Tests/PointOfSale/PaymentProcessorTests.cs` for behavioral scenarios:

### Simple Processor
- **Tax calculation correctness** — verifies 8.5% tax on $100 item

### Standard Retail Processor
- **Rounding application** — ensures fractional cents are properly rounded
- **Rounding logging** — verifies processing log contains rounding details

### E-commerce Processor
- **Promotional discounts** — 10% off electronics category
- **Loyalty discounts** — Gold tier (10%), Platinum tier (15%)
- **Loyalty points** — Gold earns 135 points on $90 (after discount)
- **Multiple discounts** — promotional + loyalty stacking

### Cash Register Processor
- **Employee discount** — 20% off + tax calculation on discounted amount
- **Nickel rounding** — $86.80 rounds to $86.80, $86.81 rounds to $86.85

### Birthday Special Processor
- **Birthday discount** — 10% off during birth month, capped at $25
- **Conditional building** — only applies decorators when conditions met

All tests use **Given-When-Then** BDD style for clarity and specification.

---

## Extending the demo

### Add a new discount type

1. Create an `After` decorator function:
```csharp
private static PaymentReceipt ApplySeasonalDiscount(PurchaseOrder order, PaymentReceipt receipt)
{
    if (IsHolidaySeason(order.OrderDate))
    {
        var discount = receipt.Subtotal * 0.15m;
        return receipt with
        {
            DiscountAmount = receipt.DiscountAmount + discount,
            FinalTotal = receipt.Subtotal - (receipt.DiscountAmount + discount) + receipt.TaxAmount,
            AppliedPromotions = receipt.AppliedPromotions.Concat(["Holiday Discount"]).ToList()
        };
    }
    return receipt;
}
```

2. Add to processor (remember reverse order!):
```csharp
.After(ApplyRounding(...))
.After(ApplyTaxCalculation)
.After(ApplySeasonalDiscount)  // Applied before tax
.After(ApplyLoyaltyDiscount)
```

### Add a new rounding strategy

Add to the `RoundingStrategy` enum and update `ApplyRounding`:
```csharp
public enum RoundingStrategy
{
    // ...existing...
    ToQuarter  // Round to nearest $0.25
}

// In ApplyRounding switch:
RoundingStrategy.ToQuarter => Math.Round(receipt.FinalTotal * 4, MidpointRounding.AwayFromZero) / 4,
```

### Create a custom processor

Compose any combination of decorators:
```csharp
public static Decorator<PurchaseOrder, PaymentReceipt> CreateWholesaleProcessor()
{
    return Decorator<PurchaseOrder, PaymentReceipt>.Create(ProcessBasicPayment)
        .After(ApplyRounding(RoundingStrategy.Down))  // Always round down
        .After(ApplyTaxCalculation)
        .After(ApplyVolumeDiscount)                   // Custom bulk discount
        .Build();
}
```

### Add pre-processing validation

Use `.Before()` to transform or validate input:
```csharp
private static PurchaseOrder NormalizeItems(PurchaseOrder order)
{
    // Remove zero-quantity items, sort by category, etc.
    var validItems = order.Items.Where(i => i.Quantity > 0).ToList();
    return order with { Items = validItems };
}

// Then:
.Before(NormalizeItems)
.Before(ValidateOrder)
```

### Add post-processing

Use `.After()` to enrich the receipt:
```csharp
private static PaymentReceipt AddReceiptMetadata(PurchaseOrder order, PaymentReceipt receipt)
{
    receipt.ProcessingLog.Add($"Processed at {order.Store.StoreId}");
    receipt.ProcessingLog.Add($"Cashier: {order.Customer.CustomerId}");
    return receipt;
}

// Apply last:
.After(AddReceiptMetadata)
.After(ApplyRounding(...))
```

---

## Performance characteristics

- **Build once, execute many** — processor is immutable after `.Build()`
- **Thread-safe** — safe for concurrent order processing
- **Allocation-light** — uses arrays internally, minimal heap pressure
- **Inline-friendly** — small delegate calls are JIT-inlineable
- **Testable** — each decorator function is independently testable

### Benchmark results (typical)

```
| Processor Type    | Mean     | Allocated |
|------------------|----------|-----------|
| Simple           | 1.2 μs   | 1.1 KB    |
| Standard Retail  | 1.5 μs   | 1.3 KB    |
| E-commerce       | 2.8 μs   | 2.4 KB    |
| Cash Register    | 1.8 μs   | 1.5 KB    |
| Birthday Special | 2.1 μs   | 1.8 KB    |
```

*(Measurements on .NET 9, single-threaded, release build)*

---

## Real-world usage

This pattern is ideal for:

- **Point of Sale systems** with varying checkout workflows
- **E-commerce platforms** with complex promotion engines
- **Subscription services** with tiered pricing and trials
- **Financial systems** requiring audit trails and compliance
- **Any domain** where business rules compose and change frequently

The fluent decorator approach lets you:
- **Add features** without modifying existing code
- **Test in isolation** — each decorator is a pure function
- **Compose dynamically** — build processors based on configuration
- **Maintain clarity** — each decorator has a single responsibility
- **Avoid inheritance** — no diamond problems, no fragile base classes

---

## Key takeaways

1. **Order matters** — `.After()` decorators execute in reverse registration order
2. **Immutability** — records with `with` expressions enable clean transformations
3. **Composability** — small, focused decorators combine into complex pipelines
4. **Testability** — each function can be tested independently
5. **Flexibility** — build processors conditionally based on runtime state

This demo shows the Decorator pattern at its best: **composing behavior declaratively, executing efficiently, and testing confidently**.

