# State Machine Pattern Generator

The State Machine Pattern Generator automatically creates deterministic finite state machines with explicit states, triggers, guards, and lifecycle hooks. It eliminates boilerplate code for state management while providing compile-time type safety, async/await support, and configurable error handling policies.

## Overview

The generator produces:

- **State property** to track the current state
- **Fire method** for synchronous state transitions
- **FireAsync method** for asynchronous workflows with ValueTask and CancellationToken support
- **CanFire method** to check if a trigger is valid for the current state
- **Deterministic transition resolution** based on (FromState, Trigger) pairs
- **Guard evaluation** with configurable failure policies
- **Entry/exit hooks** for state lifecycle management
- **Zero runtime overhead** through source generation

## Quick Start

### 1. Define Your States and Triggers

Define enums for your states and triggers:

```csharp
using PatternKit.Generators.State;

public enum OrderState
{
    Draft,
    Submitted,
    Paid,
    Shipped,
    Cancelled
}

public enum OrderTrigger
{
    Submit,
    Pay,
    Ship,
    Cancel
}
```

### 2. Create Your State Machine Host

Mark your class with `[StateMachine]` and define transitions:

```csharp
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderFlow
{
    public string Id { get; }
    public decimal Amount { get; }
    
    public OrderFlow(string id, decimal amount)
    {
        Id = id;
        Amount = amount;
        State = OrderState.Draft; // Set initial state
    }

    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
    private void OnSubmit()
    {
        Console.WriteLine($"Order {Id} submitted");
    }

    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
    private void OnPay()
    {
        Console.WriteLine($"Payment processed for {Id}");
    }

    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Ship, To = OrderState.Shipped)]
    private void OnShip()
    {
        Console.WriteLine($"Order {Id} shipped");
    }
}
```

### 3. Build Your Project

The generator runs during compilation and produces the state machine implementation:

```csharp
var order = new OrderFlow("ORD-001", 299.99m);

Console.WriteLine($"Current state: {order.State}"); // Draft

order.Fire(OrderTrigger.Submit);
Console.WriteLine($"Current state: {order.State}"); // Submitted

order.Fire(OrderTrigger.Pay);
Console.WriteLine($"Current state: {order.State}"); // Paid

order.Fire(OrderTrigger.Ship);
Console.WriteLine($"Current state: {order.State}"); // Shipped
```

### 4. Generated Code

```csharp
partial class OrderFlow
{
    public OrderState State { get; private set; }
    
    public bool CanFire(OrderTrigger trigger)
    {
        return (State, trigger) switch
        {
            (OrderState.Draft, OrderTrigger.Submit) => true,
            (OrderState.Submitted, OrderTrigger.Pay) => true,
            (OrderState.Paid, OrderTrigger.Ship) => true,
            _ => false
        };
    }
    
    public void Fire(OrderTrigger trigger)
    {
        switch (State)
        {
            case OrderState.Draft:
                switch (trigger)
                {
                    case OrderTrigger.Submit:
                        OnSubmit();
                        State = OrderState.Submitted;
                        return;
                }
                break;
            // ... more cases
        }
        
        throw new InvalidOperationException($"Invalid trigger {trigger} for state {State}");
    }
}
```

## Core Features

### Guards

Guards control whether a transition is allowed based on runtime conditions:

```csharp
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderFlow
{
    public decimal Amount { get; }
    public bool IsPaymentAuthorized { get; set; }

    [StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
    private bool CanPay()
    {
        return Amount > 0 && Amount < 10000 && IsPaymentAuthorized;
    }

    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
    private void OnPay()
    {
        Console.WriteLine($"Processing payment of ${Amount}");
    }
}
```

**Usage:**
```csharp
var order = new OrderFlow("ORD-001", 150.00m);
order.Fire(OrderTrigger.Submit);

if (order.CanFire(OrderTrigger.Pay))
{
    order.Fire(OrderTrigger.Pay); // Only fires if guard passes
}
```

### Entry and Exit Hooks

Execute code when entering or exiting specific states:

```csharp
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderFlow
{
    [StateExit(OrderState.Paid)]
    private void OnExitPaid()
    {
        Console.WriteLine("Finalizing payment transaction");
        // Send payment confirmation email
        // Update inventory
    }

    [StateEntry(OrderState.Shipped)]
    private void OnEnterShipped()
    {
        Console.WriteLine("Order is being shipped");
        // Send shipping notification
        // Generate tracking number
        // Update shipping status
    }

    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Ship, To = OrderState.Shipped)]
    private void OnShip()
    {
        Console.WriteLine("Preparing shipment");
    }
}
```

**Execution Order:**
1. Exit hooks for `FromState` (if any)
2. Transition action method (`[StateTransition]`) (if any)
3. Update `State = ToState`
4. Entry hooks for `ToState` (if any)

### Async Support

The generator automatically detects async methods and generates `FireAsync`:

```csharp
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderFlow
{
    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
    private async ValueTask OnPayAsync(CancellationToken ct)
    {
        Console.WriteLine("Processing payment...");
        await ProcessPaymentAsync(ct);
        await SendConfirmationEmailAsync(ct);
    }

    [StateEntry(OrderState.Shipped)]
    private async ValueTask OnEnterShippedAsync(CancellationToken ct)
    {
        await NotifyShippingServiceAsync(ct);
        await UpdateTrackingSystemAsync(ct);
    }
}
```

**Usage:**
```csharp
var order = new OrderFlow("ORD-001", 299.99m);
order.Fire(OrderTrigger.Submit);

// Use async method for async transitions
await order.FireAsync(OrderTrigger.Pay, cancellationToken);
await order.FireAsync(OrderTrigger.Ship, cancellationToken);
```

### Async Guards

Guards can also be async to support I/O operations:

```csharp
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderFlow
{
    [StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
    private async ValueTask<bool> CanPayAsync(CancellationToken ct)
    {
        // Check with payment service
        return await PaymentService.IsAuthorizedAsync(Id, Amount, ct);
    }

    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
    private async ValueTask OnPayAsync(CancellationToken ct)
    {
        await ProcessPaymentAsync(ct);
    }
}
```

**Note:** Async guards are evaluated synchronously in `CanFire()` using `GetAwaiter().GetResult()`. Use `FireAsync()` for proper async execution.

### Multiple Transitions from Same State

You can define multiple valid transitions from a single state:

```csharp
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderFlow
{
    // Allow cancellation from multiple states
    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    private void OnCancel()
    {
        Console.WriteLine($"Order {Id} cancelled");
    }

    [StateEntry(OrderState.Cancelled)]
    private void OnEnterCancelled()
    {
        // Process refund if applicable
        // Send cancellation notification
    }
}
```

## Configuration Options

### Custom Method Names

Customize the names of generated methods:

```csharp
[StateMachine(
    typeof(OrderState), 
    typeof(OrderTrigger),
    FireMethodName = "Transition",
    FireAsyncMethodName = "TransitionAsync",
    CanFireMethodName = "CanTransition")]
public partial class OrderFlow
{
    // Will generate: Transition(), TransitionAsync(), CanTransition()
}
```

### Error Handling Policies

#### Invalid Trigger Policy

Controls what happens when an invalid trigger is fired:

```csharp
[StateMachine(
    typeof(OrderState), 
    typeof(OrderTrigger),
    InvalidTrigger = StateMachineInvalidTriggerPolicy.Throw)] // Default
public partial class OrderFlow
{
    // Throws InvalidOperationException on invalid trigger
}

[StateMachine(
    typeof(OrderState), 
    typeof(OrderTrigger),
    InvalidTrigger = StateMachineInvalidTriggerPolicy.Ignore)]
public partial class OrderFlow
{
    // Silently ignores invalid triggers
}
```

**Available Policies:**
- `Throw` (default) - Throws `InvalidOperationException`
- `Ignore` - Does nothing, returns silently

#### Guard Failure Policy

Controls what happens when a guard returns false:

```csharp
[StateMachine(
    typeof(OrderState), 
    typeof(OrderTrigger),
    GuardFailure = StateMachineGuardFailurePolicy.Throw)] // Default
public partial class OrderFlow
{
    // Throws InvalidOperationException when guard fails
}

[StateMachine(
    typeof(OrderState), 
    typeof(OrderTrigger),
    GuardFailure = StateMachineGuardFailurePolicy.Ignore)]
public partial class OrderFlow
{
    // Silently ignores when guard fails
}
```

**Available Policies:**
- `Throw` (default) - Throws `InvalidOperationException`
- `Ignore` - Does nothing, returns silently

### Async Generation Control

Control async method generation explicitly:

```csharp
// Force async generation even without async methods
[StateMachine(
    typeof(OrderState), 
    typeof(OrderTrigger),
    ForceAsync = true)]
public partial class OrderFlow
{
    // Always generates FireAsync even for sync-only transitions
}

// Explicitly disable async generation (warning if async methods exist)
[StateMachine(
    typeof(OrderState), 
    typeof(OrderTrigger),
    GenerateAsync = false)]
public partial class OrderFlow
{
    // Will emit PKST008 warning if async methods are present
}
```

## Supported Target Types

The state machine generator supports:

- **partial class**
- **partial struct**
- **partial record class**
- **partial record struct**

```csharp
// Class
[StateMachine(typeof(State), typeof(Trigger))]
public partial class OrderStateMachine { }

// Struct (for high-performance scenarios)
[StateMachine(typeof(State), typeof(Trigger))]
public partial struct LightweightStateMachine { }

// Record class (immutable by convention)
[StateMachine(typeof(State), typeof(Trigger))]
public partial record class OrderRecord { }

// Record struct
[StateMachine(typeof(State), typeof(Trigger))]
public partial record struct CompactStateMachine { }
```

## Real-World Examples

### Order Processing Workflow

```csharp
public enum OrderState { Draft, Submitted, Paid, Shipped, Delivered, Cancelled, Refunded }
public enum OrderTrigger { Submit, Pay, Ship, Deliver, Cancel, Refund }

[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderWorkflow
{
    public string OrderId { get; }
    public decimal Amount { get; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? ShippedAt { get; private set; }

    public OrderWorkflow(string orderId, decimal amount)
    {
        OrderId = orderId;
        Amount = amount;
        State = OrderState.Draft;
    }

    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
    private void OnSubmit()
    {
        // Validate order
        Console.WriteLine($"Order {OrderId} submitted");
    }

    [StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
    private bool CanPay() => Amount > 0 && Amount < 100000;

    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
    private async ValueTask OnPayAsync(CancellationToken ct)
    {
        await ProcessPaymentAsync(ct);
        PaidAt = DateTime.UtcNow;
        Console.WriteLine($"Payment of ${Amount} processed for order {OrderId}");
    }

    [StateExit(OrderState.Paid)]
    private void OnExitPaid()
    {
        Console.WriteLine("Finalizing payment records");
    }

    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Ship, To = OrderState.Shipped)]
    private async ValueTask OnShipAsync(CancellationToken ct)
    {
        await NotifyShippingServiceAsync(ct);
        ShippedAt = DateTime.UtcNow;
        Console.WriteLine($"Order {OrderId} shipped");
    }

    [StateEntry(OrderState.Shipped)]
    private async ValueTask OnEnterShippedAsync(CancellationToken ct)
    {
        await SendTrackingNotificationAsync(ct);
        Console.WriteLine("Tracking notification sent");
    }

    [StateTransition(From = OrderState.Shipped, Trigger = OrderTrigger.Deliver, To = OrderState.Delivered)]
    private void OnDeliver()
    {
        Console.WriteLine($"Order {OrderId} delivered");
    }

    // Cancellation transitions
    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    private void OnCancel()
    {
        Console.WriteLine($"Order {OrderId} cancelled");
    }

    // Refund transition
    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Refund, To = OrderState.Refunded)]
    [StateTransition(From = OrderState.Shipped, Trigger = OrderTrigger.Refund, To = OrderState.Refunded)]
    [StateTransition(From = OrderState.Delivered, Trigger = OrderTrigger.Refund, To = OrderState.Refunded)]
    private async ValueTask OnRefundAsync(CancellationToken ct)
    {
        await ProcessRefundAsync(ct);
        Console.WriteLine($"Refund processed for order {OrderId}");
    }

    private Task ProcessPaymentAsync(CancellationToken ct) => Task.Delay(100, ct);
    private Task NotifyShippingServiceAsync(CancellationToken ct) => Task.Delay(50, ct);
    private Task SendTrackingNotificationAsync(CancellationToken ct) => Task.Delay(50, ct);
    private Task ProcessRefundAsync(CancellationToken ct) => Task.Delay(100, ct);
}
```

### Document Approval Workflow

```csharp
public enum DocumentState { Draft, PendingReview, Approved, Rejected, Published, Archived }
public enum DocumentAction { SubmitForReview, Approve, Reject, Publish, Archive, Revise }

[StateMachine(typeof(DocumentState), typeof(DocumentAction))]
public partial class DocumentWorkflow
{
    public string DocumentId { get; }
    public string CurrentReviewer { get; private set; } = string.Empty;
    public List<string> ApprovalHistory { get; } = new();

    public DocumentWorkflow(string documentId)
    {
        DocumentId = documentId;
        State = DocumentState.Draft;
    }

    [StateTransition(From = DocumentState.Draft, Trigger = DocumentAction.SubmitForReview, To = DocumentState.PendingReview)]
    private void OnSubmitForReview()
    {
        CurrentReviewer = "reviewer@example.com";
        Console.WriteLine($"Document {DocumentId} submitted for review to {CurrentReviewer}");
    }

    [StateGuard(From = DocumentState.PendingReview, Trigger = DocumentAction.Approve)]
    private bool CanApprove()
    {
        return !string.IsNullOrEmpty(CurrentReviewer);
    }

    [StateTransition(From = DocumentState.PendingReview, Trigger = DocumentAction.Approve, To = DocumentState.Approved)]
    private void OnApprove()
    {
        ApprovalHistory.Add($"{CurrentReviewer} approved at {DateTime.UtcNow}");
        Console.WriteLine($"Document {DocumentId} approved by {CurrentReviewer}");
    }

    [StateTransition(From = DocumentState.PendingReview, Trigger = DocumentAction.Reject, To = DocumentState.Rejected)]
    private void OnReject()
    {
        ApprovalHistory.Add($"{CurrentReviewer} rejected at {DateTime.UtcNow}");
        Console.WriteLine($"Document {DocumentId} rejected by {CurrentReviewer}");
    }

    [StateTransition(From = DocumentState.Rejected, Trigger = DocumentAction.Revise, To = DocumentState.Draft)]
    private void OnRevise()
    {
        CurrentReviewer = string.Empty;
        Console.WriteLine($"Document {DocumentId} sent back to draft for revision");
    }

    [StateTransition(From = DocumentState.Approved, Trigger = DocumentAction.Publish, To = DocumentState.Published)]
    private async ValueTask OnPublishAsync(CancellationToken ct)
    {
        await PublishToContentManagementSystemAsync(ct);
        Console.WriteLine($"Document {DocumentId} published");
    }

    [StateEntry(DocumentState.Published)]
    private void OnEnterPublished()
    {
        Console.WriteLine("Document is now publicly visible");
    }

    [StateTransition(From = DocumentState.Published, Trigger = DocumentAction.Archive, To = DocumentState.Archived)]
    private void OnArchive()
    {
        Console.WriteLine($"Document {DocumentId} archived");
    }

    private Task PublishToContentManagementSystemAsync(CancellationToken ct) => Task.Delay(200, ct);
}
```

## Best Practices

### 1. Define Clear State Enums

Use descriptive names that reflect business states:

```csharp
// Good
public enum OrderState { Draft, Submitted, Paid, Shipped, Delivered }

// Avoid
public enum State { S1, S2, S3, S4, S5 }
```

### 2. Use Guards for Business Rules

Centralize validation logic in guards:

```csharp
[StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
private bool CanPay()
{
    return Amount > 0 && 
           Amount < MaxAllowedAmount && 
           PaymentMethod != null &&
           !IsBlacklisted;
}
```

### 3. Keep Transition Methods Focused

Each transition method should do one thing:

```csharp
[StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
private async ValueTask OnPayAsync(CancellationToken ct)
{
    await ProcessPaymentAsync(ct);
    // Don't mix concerns - handle notification in entry hook
}

[StateEntry(OrderState.Paid)]
private async ValueTask OnEnterPaidAsync(CancellationToken ct)
{
    await SendPaymentConfirmationAsync(ct);
}
```

### 4. Use Async for I/O Operations

Prefer `ValueTask` for async operations:

```csharp
[StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Ship, To = OrderState.Shipped)]
private async ValueTask OnShipAsync(CancellationToken ct)
{
    await ShippingService.CreateShipmentAsync(Id, ct);
}
```

### 5. Document Complex Workflows

Add XML documentation to help users understand the state machine:

```csharp
/// <summary>
/// Manages the order fulfillment workflow from creation to delivery.
/// States: Draft -> Submitted -> Paid -> Shipped -> Delivered
/// </summary>
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderWorkflow
{
    /// <summary>
    /// Processes payment and transitions to Paid state.
    /// Guard: Amount must be > 0 and < $100,000
    /// </summary>
    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
    private async ValueTask OnPayAsync(CancellationToken ct)
    {
        await ProcessPaymentAsync(ct);
    }
}
```

## Diagnostics

The generator provides comprehensive compile-time diagnostics:

| ID | Severity | Description |
|----|----------|-------------|
| **PKST001** | Error | Type marked with [StateMachine] must be partial |
| **PKST002** | Error | State type must be an enum |
| **PKST003** | Error | Trigger type must be an enum |
| **PKST004** | Error | Duplicate transition detected for (From, Trigger) |
| **PKST005** | Error | Transition method signature invalid |
| **PKST006** | Error | Guard method signature invalid |
| **PKST007** | Error | Entry/Exit hook signature invalid |
| **PKST008** | Warning | Async method detected but async generation disabled |
| **PKST009** | Error | Generic types not supported |
| **PKST010** | Error | Nested types not supported |

### Common Errors and Solutions

#### PKST001: Type not partial

**Error:**
```csharp
[StateMachine(typeof(State), typeof(Trigger))]
public class OrderFlow // Missing 'partial'
{
}
```

**Solution:**
```csharp
[StateMachine(typeof(State), typeof(Trigger))]
public partial class OrderFlow // Add 'partial'
{
}
```

#### PKST004: Duplicate transitions

**Error:**
```csharp
[StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
private void OnSubmit1() { }

[StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Paid)]
private void OnSubmit2() { } // Duplicate!
```

**Solution:** Each (From, Trigger) pair must be unique. Consolidate or use guards.

#### PKST005: Invalid transition signature

**Error:**
```csharp
[StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
private int OnSubmit() // Must return void or ValueTask
{
    return 42;
}
```

**Solution:**
```csharp
[StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
private void OnSubmit() // Correct
{
}
```

## Performance Considerations

### Zero Allocation Path

The generator produces zero-allocation code for synchronous transitions:

```csharp
// No boxing, no delegates, no allocations
order.Fire(OrderTrigger.Submit);
```

### ValueTask for Async

Async operations use `ValueTask` to minimize allocations:

```csharp
// ValueTask can complete synchronously without allocation
await order.FireAsync(OrderTrigger.Pay, ct);
```

### Struct State Machines

For ultra-high-performance scenarios, use struct:

```csharp
[StateMachine(typeof(State), typeof(Trigger))]
public partial struct HighPerformanceStateMachine
{
    // Entire state machine on the stack
}
```

## Migration Guide

### From Manual Switch Statements

**Before:**
```csharp
public class OrderFlow
{
    public OrderState State { get; private set; }

    public void Fire(OrderTrigger trigger)
    {
        switch (State)
        {
            case OrderState.Draft when trigger == OrderTrigger.Submit:
                State = OrderState.Submitted;
                OnSubmit();
                break;
            // ... many more cases
        }
    }
}
```

**After:**
```csharp
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderFlow
{
    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
    private void OnSubmit() { }
    
    // Compiler generates Fire(), CanFire(), etc.
}
```

### From Other State Machine Libraries

Most state machine libraries use runtime configuration. This generator uses compile-time generation for:
- Better performance (no reflection)
- Better IntelliSense
- Compile-time validation
- Easier debugging

## FAQ

### Can I use custom types instead of enums?

Currently, only enums are supported for states and triggers (v1 limitation). This ensures:
- Compile-time validation
- Optimal performance
- Clear, unambiguous state representation

### Can I have multiple state machines in one class?

No, each class can only have one `[StateMachine]` attribute. Consider composition:

```csharp
public class Order
{
    public OrderWorkflow Workflow { get; }
    public PaymentProcessor Payment { get; }
}
```

### Is it thread-safe?

No, the generated state machine is not thread-safe by default. Use external synchronization if needed:

```csharp
private readonly object _lock = new();

public void SafeFire(OrderTrigger trigger)
{
    lock (_lock)
    {
        _order.Fire(trigger);
    }
}
```

### Can I persist the state?

Yes, serialize the `State` property:

```csharp
var json = JsonSerializer.Serialize(new { order.State, order.OrderId });
// Save to database

// Later, restore:
var data = JsonSerializer.Deserialize<OrderData>(json);
var order = new OrderFlow(data.OrderId, amount);
order.State = data.State; // Set via constructor or property
```

### How do I test state machines?

Test transitions independently:

```csharp
[Fact]
public void Submit_TransitionsToDraftToSubmitted()
{
    var order = new OrderFlow("TEST-001", 100m);
    Assert.Equal(OrderState.Draft, order.State);
    
    order.Fire(OrderTrigger.Submit);
    Assert.Equal(OrderState.Submitted, order.State);
}

[Fact]
public void CanPay_ReturnsFalse_WhenAmountIsZero()
{
    var order = new OrderFlow("TEST-002", 0m);
    order.Fire(OrderTrigger.Submit);
    
    Assert.False(order.CanFire(OrderTrigger.Pay));
}
```

## See Also

- [State Pattern Examples](../examples/state-machine-examples.md)
- [Template Method Generator](template-method-generator.md) - For sequential workflows
- [Builder Pattern](builder.md) - For object construction
- [Visitor Pattern](visitor-generator.md) - For operation dispatch
