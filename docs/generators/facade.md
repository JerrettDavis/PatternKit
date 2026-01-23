# Facade Generator

## Overview

The **Facade Generator** creates GoF-compliant Facade pattern implementations that provide simplified interfaces to complex subsystems. It eliminates boilerplate by automatically generating facade types with clean, deterministic code that coordinates multiple dependencies.

## When to Use

Use the Facade generator when you need to:

- **Simplify complex subsystems**: Provide a clean API over multiple interacting services
- **Reduce coupling**: Hide subsystem complexity from clients
- **Coordinate operations**: Orchestrate calls across multiple subsystems
- **Define explicit boundaries**: Create clear entry points to subsystem functionality

## Installation

The generator is included in the `PatternKit.Generators` package:

```bash
dotnet add package PatternKit.Generators
```

## Quick Start

### Host-First Approach (Recommended)

```csharp
using PatternKit.Generators.Facade;

[GenerateFacade]
public static partial class BillingFacadeHost
{
    [FacadeExpose]
    public static Receipt ProcessPayment(
        IPaymentGateway gateway, 
        ITaxService tax,
        PaymentRequest request)
    {
        var taxAmount = tax.CalculateTax(request.Amount);
        return gateway.Charge(request.Amount + taxAmount);
    }
}
```

Generated:
```csharp
public sealed class BillingFacade
{
    private readonly IPaymentGateway _gateway;
    private readonly ITaxService _tax;

    public BillingFacade(IPaymentGateway gateway, ITaxService tax)
    {
        _gateway = gateway;
        _tax = tax;
    }

    public Receipt ProcessPayment(PaymentRequest request)
    {
        return BillingFacadeHost.ProcessPayment(_gateway, _tax, request);
    }
}
```

## Approaches

### 1. Host-First (Static Methods)

Best for: Clean, explicit facades with clear subsystem coordination.

```csharp
[GenerateFacade(FacadeTypeName = "ShippingFacade")]
public static partial class ShippingHost
{
    [FacadeExpose(MethodName = "CalculateCost")]
    public static decimal GetShippingCost(
        IRateCalculator calculator,
        IShippingValidator validator,
        ShipmentDetails details)
    {
        validator.Validate(details);
        return calculator.Calculate(details);
    }

    [FacadeExpose]
    public static string TrackPackage(
        ITrackingService tracking,
        string trackingNumber)
    {
        return tracking.GetStatus(trackingNumber);
    }
}
```

**Key points:**
- Subsystem dependencies come first in method parameters
- Generated facade has constructor injection
- Static methods become instance methods
- Parameters are automatically filtered (dependencies removed)

### 2. Contract-First (Interface/Class)

Best for: When you have a pre-defined facade interface.

```csharp
// Define the contract
public interface IBillingFacade
{
    Receipt Pay(PaymentRequest req);
    Refund ProcessRefund(RefundRequest req);
}

// Implement with mapped methods
[GenerateFacade]
public partial class BillingFacadeImpl : IBillingFacade
{
    private readonly IPaymentGateway _gateway;
    private readonly IRefundService _refunds;

    [FacadeMap]
    private Receipt Pay(PaymentRequest req) 
        => _gateway.Process(req);

    [FacadeMap]
    private Refund ProcessRefund(RefundRequest req) 
        => _refunds.Process(req);
}
```

## Attributes

### `[GenerateFacade]`

Main attribute for marking facade types.

**Properties:**
- `FacadeTypeName` (string?): Name of generated facade (default: `{Name}Facade` for host-first)
- `GenerateAsync` (bool): Enable async generation (default: `true`, inferred from signatures)
- `ForceAsync` (bool): Force all methods to be async (default: `false`)
- `MissingMap` (FacadeMissingMapPolicy): How to handle unmapped members (default: `Error`)

### `[FacadeExpose]`

Marks host methods to expose as facade operations (host-first only).

**Properties:**
- `MethodName` (string?): Custom name in facade (default: method name)

### `[FacadeMap]`

Maps implementation methods to contract members (contract-first only).

**Properties:**
- `MemberName` (string?): Explicit contract member name (default: auto-match by signature)

### `[FacadeIgnore]`

Excludes contract members from generation.

## Async Support

The generator automatically detects async methods:

```csharp
[GenerateFacade]
public static partial class OrderHost
{
    [FacadeExpose]
    public static async Task<OrderResult> PlaceOrderAsync(
        IInventoryService inventory,
        IPaymentService payment,
        Order order,
        CancellationToken ct = default)
    {
        await inventory.ReserveAsync(order.Items, ct);
        return await payment.ProcessAsync(order.Total, ct);
    }
}
```

Generated methods use `ValueTask<T>` by default for better performance.

## Diagnostics

| ID | Severity | Description |
|---|---|---|
| **PKFCD001** | Error | Type marked `[GenerateFacade]` must be `partial` |
| **PKFCD002** | Error | No mapped methods found for facade members |
| **PKFCD003** | Error | Multiple mappings found for single facade member |
| **PKFCD004** | Error | Map method signature mismatch |
| **PKFCD005** | Error | Facade type name conflicts with existing type |
| **PKFCD006** | Warning | Async mapping detected but async generation disabled |

## Best Practices

### 1. Dependency Order
Always place subsystem dependencies **first** in host method parameters:

```csharp
// ✅ Correct
public static Result DoWork(IServiceA a, IServiceB b, string input) { }

// ❌ Wrong - subsystems must come first
public static Result DoWork(string input, IServiceA a, IServiceB b) { }
```

### 2. Use Host-First for New Code
Host-first provides cleaner, more maintainable facades with explicit subsystem coordination.

### 3. Keep Facades Focused
Facades should coordinate 3-5 subsystems max. If you need more, consider breaking into multiple facades.

### 4. Document Coordination Logic
The facade's value is in *how* it coordinates subsystems. Document that:

```csharp
/// <summary>
/// Processes payment by calculating tax, validating amount, and charging gateway.
/// Rolls back inventory reservation if payment fails.
/// </summary>
[FacadeExpose]
public static PaymentResult ProcessPayment(...)
```

### 5. Error Handling
Handle subsystem errors at the facade level:

```csharp
[FacadeExpose]
public static OrderResult PlaceOrder(
    IInventory inventory,
    IPayment payment,
    Order order)
{
    try
    {
        inventory.Reserve(order.Items);
        return payment.Process(order);
    }
    catch (PaymentException ex)
    {
        inventory.Rollback(order.Items);
        throw;
    }
}
```

## Examples

### E-Commerce Billing Facade

See: [BillingFacadeExample.cs](../../src/PatternKit.Examples/Generators/Facade/BillingFacadeExample.cs)

Demonstrates:
- Multi-subsystem coordination (tax, invoice, payment, notifications)
- Transaction-like behavior with rollback
- Error handling across subsystems

### Shipping Management Facade

See: [ShippingFacadeExample.cs](../../src/PatternKit.Examples/Generators/Facade/ShippingFacadeExample.cs)

Demonstrates:
- Rate calculation coordination
- Validation orchestration
- Clean API design

## Comparison with Other Patterns

| Pattern | Purpose | When to Use |
|---|---|---|
| **Facade** | Simplify subsystem access | Complex subsystem coordination |
| **Adapter** | Convert interfaces | Single interface incompatibility |
| **Proxy** | Control access | Add behavior (caching, logging, lazy loading) |
| **Mediator** | Reduce coupling | Many-to-many object communication |

## Troubleshooting

### PKFCD001: Must be partial

**Cause:** Target type is not marked `partial`.

**Fix:**
```csharp
// ❌ Wrong
[GenerateFacade]
public static class MyHost { }

// ✅ Correct
[GenerateFacade]
public static partial class MyHost { }
```

### PKFCD002: No mapped methods

**Cause:** Contract has members but no `[FacadeMap]` methods found.

**Fix:** Add `[FacadeMap]` methods for each contract member or use `[FacadeIgnore]` to exclude.

### PKFCD004: Signature mismatch

**Cause:** Map method signature doesn't match contract member.

**Fix:** Ensure return type, parameter types, and order match exactly.

## See Also

- [Builder Generator](builder.md)
- [Strategy Generator](../patterns/strategy.md)
- [Visitor Generator](visitor-generator.md)
