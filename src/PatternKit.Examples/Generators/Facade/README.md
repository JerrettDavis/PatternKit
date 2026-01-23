# Facade Pattern Examples

This directory contains examples demonstrating the **Facade Pattern** using PatternKit's source generators.

## Overview

The Facade Pattern provides a simplified interface to a complex subsystem. PatternKit supports two approaches:

1. **Host-First**: Define implementation methods first, expose them through a generated facade ✅ RECOMMENDED
2. **Contract-First**: Define interface first, map static methods to it ⚠️ Currently has limitations

## Examples

### ✅ ShippingFacadeExample.cs - Host-First Pattern (WORKING)

Demonstrates the recommended **Host-First** approach using `[FacadeExpose]`:

**Pattern:**
- Mark static partial class with `[GenerateFacade(FacadeTypeName = "X")]`
- Mark methods with `[FacadeExpose]` to include in facade
- Method signatures: **dependencies FIRST**, then business parameters
- Generated facade: Constructor takes dependencies, methods have only business parameters

**Key Code:**
```csharp
[GenerateFacade(FacadeTypeName = "ShippingFacade")]
public static partial class ShippingHost
{
    [FacadeExpose]
    public static decimal CalculateShippingCost(
        RateCalculator rateCalc,    // Dependency (injected)
        string destination,           // Business parameter
        decimal weight)               // Business parameter
    {
        // Implementation
    }
}

// Usage:
var facade = new ShippingFacade(estimator, rateCalc, validator);
var cost = facade.CalculateShippingCost("local", 3.5m);  // No dependencies in call
```

**Features Demonstrated:**
- ✅ Multiple subsystem services coordinated
- ✅ Dependency injection via constructor
- ✅ Clean separation of concerns
- ✅ Simplified API for clients
- ✅ Multiple methods with different dependency combinations

**Run the demo:**
```csharp
ShippingFacadeDemo.Run();
```

## 🚧 Known Issues

### Contract-First Pattern Currently Unsupported

The Contract-First approach with `[FacadeMap]` attribute currently has limitations:

**Expected Pattern:**
```csharp
[GenerateFacade]
public partial interface IBillingFacade
{
    string ProcessOrder(string customerId, decimal subtotal, decimal taxRate);
}

public static class BillingOperations
{
    [FacadeMap(MemberName = "ProcessOrder")]
    public static string HandleOrder(
        TaxService taxService,           // Dependencies first
        InvoiceService invoiceService,
        string customerId,                // Then business params
        decimal subtotal,
        decimal taxRate)
    {
        // Implementation
    }
}
```

**Issue:**
Generates diagnostic error `PKFCD004: Method signature does not match contract member` even when signatures appear correct. This prevents compilation in MSBuild context, though unit tests handle it as non-fatal.

**Workaround:**
Use the **Host-First** pattern with `[FacadeExpose]` (see ShippingFacadeExample.cs) which works reliably.

## Pattern Benefits

✅ **Simplified Interface**: Hide complex subsystem interactions behind clean API  
✅ **Dependency Injection**: Services injected once, reused for all method calls  
✅ **Loose Coupling**: Clients depend on facade, not individual subsystems  
✅ **Easy Testing**: Mock subsystems to test facade in isolation  
✅ **Maintainability**: Change subsystems without affecting clients  

## When to Use Facade Pattern

- System has multiple complex subsystems that need coordination
- You want to provide a simple entry point for common operations
- You need to decouple clients from subsystem implementation details
- Multiple subsystems must be used together in specific sequences

## Best Practices

1. **Keep facades focused**: One facade per logical subsystem group
2. **Don't expose everything**: Only include methods clients actually need
3. **Handle errors appropriately**: Translate subsystem errors to facade-level errors
4. **Document clearly**: Facades hide complexity, so document what they do
5. **Test thoroughly**: Integration tests for subsystem coordination

## Related Patterns

- **Adapter**: Changes interface of single class; Facade simplifies interface of subsystem
- **Mediator**: Centralizes communication; Facade provides simplified interface
- **Proxy**: Same interface as subject; Facade provides different, simpler interface

## Additional Resources

- [Facade Pattern (Gang of Four)](https://en.wikipedia.org/wiki/Facade_pattern)
- [Test Suite](../../../../test/PatternKit.Generators.Tests/FacadeGeneratorTests.cs) - See working test patterns
