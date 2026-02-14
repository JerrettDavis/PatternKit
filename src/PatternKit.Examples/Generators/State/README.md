# State Machine Pattern Examples

This directory contains real-world examples demonstrating the State Machine Pattern Source Generator in action.

## Overview

The State Machine Pattern Generator creates deterministic finite state machines with:
- ✅ Explicit states and triggers (enum-based)
- ✅ Compile-time validation
- ✅ Guards for conditional transitions
- ✅ Entry/Exit lifecycle hooks
- ✅ Sync and async support (ValueTask)
- ✅ Zero runtime dependencies

## Examples in This Directory

### OrderFlowDemo.cs

A comprehensive order processing workflow demonstrating:

1. **Basic State Transitions** - Order lifecycle from Draft to Delivered
2. **Guards** - Payment validation based on amount
3. **Async Transitions** - Payment processing with async/await
4. **Entry/Exit Hooks** - Notifications and cleanup actions
5. **Multiple Transition Sources** - Cancellation from multiple states
6. **Error Handling** - Guard failures and invalid triggers

#### States
- `Draft` - Initial state when order is created
- `Submitted` - Order submitted for processing
- `Paid` - Payment successfully processed
- `Shipped` - Order shipped to customer
- `Cancelled` - Order cancelled

#### Triggers
- `Submit` - Submit order for processing
- `Pay` - Process payment
- `Ship` - Ship the order
- `Cancel` - Cancel the order

#### Running the Demo

```csharp
using PatternKit.Examples.Generators.State;

// Happy path: Draft -> Submitted -> Paid -> Shipped
OrderFlowDemo.Run();

// Cancellation scenario
OrderFlowDemo.CancellationDemo();

// Guard failure scenario
OrderFlowDemo.GuardFailureDemo();
```

#### Sample Output

```
=== Order Flow State Machine Demo ===

Order: ORD-001, Amount: $299.99
Initial State: Draft

1. Submitting order...
   >> Transition: Submitting order ORD-001
   State: Submitted

2. Attempting to pay...
3. Processing payment...
   >> Transition: Processing payment for ORD-001...
   >> Payment of $299.99 processed
   State: Paid

4. Shipping order...
   >> Exit Hook: Finalizing payment for ORD-001
   >> Transition: Shipping order ORD-001
   >> Entry Hook: Order ORD-001 is now shipped, sending notification
   State: Shipped

=== Order processing complete ===
```

## Key Concepts Demonstrated

### 1. Synchronous Transitions

Simple state changes with synchronous actions:

```csharp
[StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
private void OnSubmit()
{
    Console.WriteLine($"   >> Transition: Submitting order {Id}");
}
```

### 2. Asynchronous Transitions

Async operations with proper cancellation token handling:

```csharp
[StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
private async ValueTask OnPayAsync(CancellationToken ct)
{
    Console.WriteLine($"   >> Transition: Processing payment for {Id}...");
    await Task.Delay(500, ct); // Simulate payment processing
    Console.WriteLine($"   >> Payment of ${Amount:F2} processed");
}
```

### 3. Guards for Validation

Prevent invalid transitions based on business rules:

```csharp
[StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
private bool CanPay()
{
    return Amount > 0; // Only allow payment for valid amounts
}
```

**Usage:**
```csharp
if (order.CanFire(OrderTrigger.Pay))
{
    await order.FireAsync(OrderTrigger.Pay, CancellationToken.None);
}
else
{
    Console.WriteLine("   Payment blocked by guard (invalid amount)");
}
```

### 4. Entry and Exit Hooks

Execute side effects when entering or leaving states:

```csharp
// Exit hook - runs before leaving Paid state
[StateExit(OrderState.Paid)]
private void OnExitPaid()
{
    Console.WriteLine($"   >> Exit Hook: Finalizing payment for {Id}");
}

// Entry hook - runs after entering Shipped state
[StateEntry(OrderState.Shipped)]
private void OnEnterShipped()
{
    Console.WriteLine($"   >> Entry Hook: Order {Id} is now shipped, sending notification");
}
```

**Execution Order (when transitioning Paid -> Shipped):**
1. `OnExitPaid()` - Exit hook
2. `OnShip()` - Transition action
3. `State = Shipped` - State update
4. `OnEnterShipped()` - Entry hook

### 5. Multiple Transitions from Same Source

Handle common actions like cancellation from multiple states:

```csharp
[StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
[StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
[StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
private void OnCancel()
{
    Console.WriteLine($"   >> Transition: Cancelling order {Id}");
}
```

## Usage Patterns

### Pattern 1: Check Before Fire

Use `CanFire` to check if a trigger is valid:

```csharp
if (order.CanFire(OrderTrigger.Pay))
{
    order.Fire(OrderTrigger.Pay);
}
else
{
    Console.WriteLine("Cannot process payment at this time");
}
```

### Pattern 2: Async Workflows

Use `FireAsync` for async transitions:

```csharp
try
{
    await order.FireAsync(OrderTrigger.Pay, cancellationToken);
    Console.WriteLine("Payment processed successfully");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Payment cancelled");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Invalid transition: {ex.Message}");
}
```

### Pattern 3: State-Based Logic

Make decisions based on current state:

```csharp
switch (order.State)
{
    case OrderState.Draft:
        Console.WriteLine("Order is still being prepared");
        break;
    case OrderState.Submitted:
        Console.WriteLine("Waiting for payment");
        break;
    case OrderState.Shipped:
        Console.WriteLine("Order is on its way!");
        break;
}
```

### Pattern 4: Transition History

Track state changes by wrapping Fire methods:

```csharp
public class TrackedOrderFlow
{
    private readonly OrderFlow _flow;
    private readonly List<(OrderState From, OrderTrigger Trigger, OrderState To)> _history = new();

    public void Fire(OrderTrigger trigger)
    {
        var from = _flow.State;
        _flow.Fire(trigger);
        var to = _flow.State;
        _history.Add((from, trigger, to));
    }

    public IReadOnlyList<(OrderState From, OrderTrigger Trigger, OrderState To)> History => _history;
}
```

## Common Scenarios

### Scenario 1: Happy Path Processing

```csharp
var order = new OrderFlow("ORD-001", 299.99m);

// Draft -> Submitted
order.Fire(OrderTrigger.Submit);

// Submitted -> Paid
await order.FireAsync(OrderTrigger.Pay, ct);

// Paid -> Shipped
order.Fire(OrderTrigger.Ship);

Console.WriteLine($"Order completed in state: {order.State}");
```

### Scenario 2: Validation Failure

```csharp
var order = new OrderFlow("ORD-002", -50m); // Invalid amount
order.Fire(OrderTrigger.Submit);

// Guard will prevent payment
if (!order.CanFire(OrderTrigger.Pay))
{
    Console.WriteLine("Cannot process payment - validation failed");
    order.Fire(OrderTrigger.Cancel);
}
```

### Scenario 3: Cancellation

```csharp
var order = new OrderFlow("ORD-003", 100m);
order.Fire(OrderTrigger.Submit);

// Customer changes mind before payment
order.Fire(OrderTrigger.Cancel);

Console.WriteLine($"Order cancelled in state: {order.State}");
```

### Scenario 4: Async with Cancellation

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var order = new OrderFlow("ORD-004", 500m);

try
{
    order.Fire(OrderTrigger.Submit);
    await order.FireAsync(OrderTrigger.Pay, cts.Token);
    Console.WriteLine("Payment processed before timeout");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Payment processing timed out");
    order.Fire(OrderTrigger.Cancel);
}
```

## Testing Your State Machines

### Unit Test Example

```csharp
using Xunit;

public class OrderFlowTests
{
    [Fact]
    public void Submit_TransitionsFromDraftToSubmitted()
    {
        // Arrange
        var order = new OrderFlow("TEST-001", 100m);
        Assert.Equal(OrderState.Draft, order.State);

        // Act
        order.Fire(OrderTrigger.Submit);

        // Assert
        Assert.Equal(OrderState.Submitted, order.State);
    }

    [Fact]
    public void Pay_WithZeroAmount_BlockedByGuard()
    {
        // Arrange
        var order = new OrderFlow("TEST-002", 0m);
        order.Fire(OrderTrigger.Submit);

        // Assert
        Assert.False(order.CanFire(OrderTrigger.Pay));
    }

    [Fact]
    public async Task PayAsync_ProcessesPaymentAndTransitionsToPaid()
    {
        // Arrange
        var order = new OrderFlow("TEST-003", 250m);
        order.Fire(OrderTrigger.Submit);

        // Act
        await order.FireAsync(OrderTrigger.Pay, CancellationToken.None);

        // Assert
        Assert.Equal(OrderState.Paid, order.State);
    }

    [Fact]
    public void Ship_FromDraft_ThrowsInvalidOperationException()
    {
        // Arrange
        var order = new OrderFlow("TEST-004", 100m);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => 
            order.Fire(OrderTrigger.Ship));
    }
}
```

### Integration Test Example

```csharp
public class OrderFlowIntegrationTests
{
    [Fact]
    public async Task CompleteOrderWorkflow_ProcessesSuccessfully()
    {
        // Arrange
        var order = new OrderFlow("INT-001", 199.99m);
        var states = new List<OrderState>();

        // Act
        states.Add(order.State); // Draft
        
        order.Fire(OrderTrigger.Submit);
        states.Add(order.State); // Submitted
        
        await order.FireAsync(OrderTrigger.Pay, CancellationToken.None);
        states.Add(order.State); // Paid
        
        order.Fire(OrderTrigger.Ship);
        states.Add(order.State); // Shipped

        // Assert
        Assert.Equal(new[]
        {
            OrderState.Draft,
            OrderState.Submitted,
            OrderState.Paid,
            OrderState.Shipped
        }, states);
    }
}
```

## Best Practices

### 1. Initialize State in Constructor

Always set the initial state explicitly:

```csharp
public OrderFlow(string id, decimal amount)
{
    Id = id;
    Amount = amount;
    State = OrderState.Draft; // Explicit initial state
}
```

### 2. Use Meaningful Names

Choose clear, business-oriented names:

```csharp
// Good
public enum OrderState { Draft, Submitted, Paid, Shipped }
public enum OrderTrigger { Submit, Pay, Ship }

// Avoid
public enum State { S1, S2, S3, S4 }
public enum Action { A1, A2, A3 }
```

### 3. Keep Transition Methods Focused

Each method should have a single responsibility:

```csharp
// Good - focused on payment
[StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
private async ValueTask OnPayAsync(CancellationToken ct)
{
    await ProcessPaymentAsync(ct);
}

// Bad - mixing concerns
[StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
private async ValueTask OnPayAsync(CancellationToken ct)
{
    await ProcessPaymentAsync(ct);
    await SendEmailAsync(ct); // Should be in entry hook
    await UpdateInventoryAsync(ct); // Should be separate
}
```

### 4. Use Entry/Exit Hooks for Side Effects

Separate concerns using hooks:

```csharp
[StateExit(OrderState.Paid)]
private void OnExitPaid()
{
    // Cleanup, finalization
    FinalizePaymentRecords();
}

[StateEntry(OrderState.Shipped)]
private async ValueTask OnEnterShippedAsync(CancellationToken ct)
{
    // Side effects when entering state
    await SendShippingNotificationAsync(ct);
    await UpdateInventoryAsync(ct);
}
```

### 5. Document Complex Workflows

Add comments explaining business logic:

```csharp
/// <summary>
/// Processes payment and transitions to Paid state.
/// Business Rules:
/// - Amount must be greater than 0
/// - Amount must be less than $10,000 (daily limit)
/// - Payment method must be valid
/// </summary>
[StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
private bool CanPay()
{
    return Amount > 0 && Amount < 10000 && IsPaymentMethodValid();
}
```

## Troubleshooting

### Issue: Guard always returns false in CanFire

**Problem:** Async guard evaluated synchronously

**Solution:** Async guards use `GetAwaiter().GetResult()` in `CanFire`. Use `FireAsync` for proper async evaluation.

### Issue: State doesn't change after Fire

**Possible causes:**
1. Guard returned false
2. Invalid trigger for current state
3. Check error handling policy

**Debug:**
```csharp
if (order.CanFire(trigger))
{
    try
    {
        order.Fire(trigger);
    }
    catch (InvalidOperationException ex)
    {
        Console.WriteLine($"Transition failed: {ex.Message}");
    }
}
```

### Issue: Compilation error about missing partial keyword

**Solution:** Ensure your class is marked as `partial`:

```csharp
[StateMachine(typeof(State), typeof(Trigger))]
public partial class MyStateMachine // Add 'partial'
{
}
```

## Further Reading

- [State Machine Generator Documentation](../../docs/generators/state-machine.md)
- [Generator Diagnostics](../../docs/generators/troubleshooting.md)
- [Pattern Overview](../../docs/patterns/behavioral/state/index.md)

## Contributing

Have an interesting state machine example? Submit a PR with:
1. Clear business scenario description
2. State and trigger definitions
3. Complete, runnable example
4. Expected output
5. Key concepts demonstrated
