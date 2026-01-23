# Facade Pattern Examples

This directory contains comprehensive, real-world examples of the **Facade Pattern** using PatternKit's source generators.

## Overview

The Facade Pattern provides a simplified interface to a complex subsystem or set of subsystems. It hides the complexities of the underlying systems and provides a cleaner, more convenient API for common use cases.

### Key Benefits

- **Simplified Interface**: Reduces complexity by providing a higher-level API
- **Subsystem Decoupling**: Clients depend on the facade, not individual subsystems
- **Improved Maintainability**: Changes to subsystems don't affect client code
- **Centralized Coordination**: Complex multi-step workflows are encapsulated
- **Better Testability**: Easier to mock a single facade than multiple subsystems

## PatternKit Facade Approach

PatternKit supports a **Host-First** approach using `[FacadeExpose]` attribute:

### Host-First Pattern ✅ RECOMMENDED

Define static methods in a host class marked with `[FacadeExpose]`, and the generator creates an instance-based facade type with constructor dependency injection.

**When to use:**
- ✅ Implementation-driven development is preferred
- ✅ You're starting with existing static utility methods
- ✅ You want to automatically transform static code to instance-based
- ✅ Simpler scenarios where the contract is obvious from implementation

**Key Pattern:**
```csharp
[GenerateFacade(FacadeTypeName = "ShippingFacade")]
public static partial class ShippingHost
{
    [FacadeExpose]
    public static decimal CalculateShippingCost(
        RateCalculator rateCalc,    // Dependency (injected via constructor)
        DeliveryEstimator estimator, // Dependency (injected via constructor)
        string destination,          // Business parameter
        decimal weight)              // Business parameter
    {
        // Implementation coordinates multiple subsystems
        var baseRate = rateCalc.CalculateBaseRate(destination);
        var surcharge = rateCalc.CalculateWeightSurcharge(weight);
        return baseRate + surcharge;
    }
}

// Generated facade usage:
var facade = new ShippingFacade(estimator, rateCalc, validator);
var cost = facade.CalculateShippingCost("local", 3.5m);  // Dependencies automatically provided
```

**Key Features:**
- Static methods define the facade surface
- Methods marked with `[FacadeExpose]`
- Subsystem dependencies as **first** parameters
- Generator creates instance facade class
- Automatic dependency injection via constructor (alphabetically ordered)
- Generated methods have only business parameters

## Examples

### 1. ShippingFacadeExample.cs

A shipping cost calculation and validation facade demonstrating basic Host-First pattern.

**Subsystems:**
- `RateCalculator` - Base rate and weight surcharge calculation
- `DeliveryEstimator` - Delivery timeframe estimation
- `ShippingValidator` - Shipment validation rules

**Operations:**
- `CalculateShippingCost()` - Calculate total shipping cost
- `EstimateDelivery()` - Estimate delivery timeframe
- `ValidateShipment()` - Validate shipment parameters
- `GetShippingQuote()` - Get complete shipping quote

**Demonstrates:**
- Multiple subsystem coordination
- Dependency injection via constructor
- Clean separation of concerns
- Error handling and validation

**Run the demo:**
```csharp
ShippingFacadeDemo.Run();
```

### 2. BillingFacadeExample.cs

A billing system facade coordinating tax, invoice, payment, and notifications.

**Subsystems:**
- `TaxService` - Tax calculation by jurisdiction
- `InvoiceService` - Invoice generation and management
- `PaymentProcessor` - Payment processing and refunds
- `NotificationService` - Email notifications

**Operations:**
- `ProcessPayment()` - Complete billing transaction with tax and invoice
- `ProcessRefund()` - Process payment refunds
- `CalculateTotalWithTax()` - Calculate totals with tax
- `GetInvoice()` - Retrieve invoice details

**Demonstrates:**
- Complex multi-step workflows
- Four subsystem coordination
- Transaction-like behavior
- Error handling with rollback

**Run the demo:**
```csharp
BillingFacadeDemo.Run();
```

## Usage Patterns

### Basic Synchronous Usage

```csharp
// Create subsystems
var taxService = new TaxService();
var invoiceService = new InvoiceService();
var paymentProcessor = new PaymentProcessor();
var notificationService = new NotificationService();

// Create facade (dependencies injected via constructor)
// Note: Constructor parameters are ALPHABETICALLY ORDERED by type name
var facade = new BillingFacade(
    invoiceService,      // I...
    notificationService, // N...
    paymentProcessor,    // P...
    taxService          // T...
);

// Use simple facade API (no dependencies in method calls)
var result = facade.ProcessPayment(
    customerId: "CUST-001",
    subtotal: 100m,
    jurisdiction: "US-CA",
    paymentMethod: "VISA-****1234"
);
```

### Async with Cancellation (Note: Async support is limited)

While PatternKit supports async methods, complex async scenarios with multiple subsystems may require careful consideration:

```csharp
// Simple async pattern works
[FacadeExpose]
public static async Task<Result> DoSomethingAsync(
    SomeService service,
    string param,
    CancellationToken ct = default)
{
    await service.DoWorkAsync(ct);
    return new Result { Success = true };
}
```

For complex async coordination, consider using the synchronous facade pattern with subsystems that internally handle async operations.

## Best Practices

### 1. Method Parameter Order

**CRITICAL**: In `[FacadeExpose]` methods, dependencies MUST come first:

```csharp
[FacadeExpose]
public static Result DoSomething(
    // 1. Dependencies FIRST (will be injected)
    TaxService taxService,
    PaymentGateway payment,
    // 2. Then business parameters
    string customerId,
    decimal amount)
{
    // Implementation
}
```

### 2. Constructor Parameter Order

Generated facade constructors have parameters **alphabetically ordered by type name**:

```csharp
// If methods use: TaxService, PaymentGateway, InvoiceService
// Constructor will be: (InvoiceService, PaymentGateway, TaxService)
var facade = new MyFacade(invoiceService, paymentGateway, taxService);
```

### 3. Dependency Management

- Inject subsystem dependencies through constructor
- Don't create subsystems inside the facade methods
- Use interfaces for subsystems when possible for better testability
- Keep subsystem references immutable

### 4. Error Handling

- Validate inputs early in facade methods
- Provide meaningful error messages
- Consider rollback logic for multi-step operations
- Use result types instead of exceptions for expected failures

### 5. Async Operations (Limited Support)

- Use `Task<T>` for async methods when needed
- Keep async facades simple with minimal subsystem dependencies
- For complex async coordination, consider synchronous facades with internally async subsystems
- Test thoroughly when using async patterns

### 6. Transaction Behavior

- Coordinate subsystems in logical order
- Implement rollback for failed multi-step operations
- Keep track of what needs cleanup
- Use try-catch blocks for error recovery

## Pattern Comparison

### When to Use Facade

✅ **Use Facade when:**
- You need to simplify a complex subsystem
- You want to decouple clients from subsystem implementation details
- You need to coordinate multiple subsystems for common operations
- You want to provide a default, easy-to-use interface

❌ **Don't use Facade when:**
- The subsystem is already simple
- Clients need fine-grained control over subsystems
- The facade would just be a pass-through with no value added
- A simpler service layer would suffice

### Related Patterns

- **Adapter**: Changes interface of a single class; Facade simplifies multiple classes
- **Mediator**: Centralizes complex communications; Facade simplifies interface
- **Proxy**: Controls access to an object; Facade simplifies subsystem interaction

## Additional Resources

- [PatternKit Documentation](../../../README.md)
- [Facade Pattern (Gang of Four)](https://en.wikipedia.org/wiki/Facade_pattern)
- [Source Generator Attributes](../../../docs/generators.md)

## Running the Examples

Each example file contains a demo class:

```csharp
// Synchronous examples
ShippingFacadeDemo.Run();
BillingFacadeDemo.Run();
```

These demonstrations show realistic usage scenarios and output results to the console.

## Additional Resources

- [Facade Pattern (Gang of Four)](https://en.wikipedia.org/wiki/Facade_pattern)
- [Test Suite](../../../../test/PatternKit.Generators.Tests/FacadeGeneratorTests.cs) - See working test patterns
