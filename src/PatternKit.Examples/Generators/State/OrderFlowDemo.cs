using System;
using System.Threading;
using System.Threading.Tasks;
using PatternKit.Generators.State;

namespace PatternKit.Examples.Generators.State;

/// <summary>
/// Real-world example of a State Machine generator demonstrating an order processing workflow.
/// This example shows:
/// - Enum-based states and triggers
/// - Synchronous and asynchronous transitions
/// - Guards to control transitions
/// - Entry and exit hooks for state changes
/// - Proper cancellation token handling
/// </summary>
public static class OrderFlowDemo
{
    public static void Run()
    {
        Console.WriteLine("=== Order Flow State Machine Demo ===\n");

        // Create an order flow instance
        var order = new OrderFlow("ORD-001", 299.99m);
        
        Console.WriteLine($"Order: {order.Id}, Amount: ${order.Amount:F2}");
        Console.WriteLine($"Initial State: {order.State}\n");

        // Submit the order
        Console.WriteLine("1. Submitting order...");
        order.Fire(OrderTrigger.Submit);
        Console.WriteLine($"   State: {order.State}\n");

        // Try to pay for the order (will check guard)
        Console.WriteLine("2. Attempting to pay...");
        if (order.CanFire(OrderTrigger.Pay))
        {
            // This is async, so we'll use RunAsync
            RunAsync(order).GetAwaiter().GetResult();
        }
        else
        {
            Console.WriteLine("   Cannot pay for order (guard failed)\n");
        }

        // Ship the order
        Console.WriteLine("4. Shipping order...");
        order.Fire(OrderTrigger.Ship);
        Console.WriteLine($"   State: {order.State}\n");

        Console.WriteLine("=== Order processing complete ===\n");
    }

    private static async Task RunAsync(OrderFlow order)
    {
        Console.WriteLine("3. Processing payment...");
        await order.FireAsync(OrderTrigger.Pay, CancellationToken.None);
        Console.WriteLine($"   State: {order.State}\n");
    }

    public static void CancellationDemo()
    {
        Console.WriteLine("=== Cancellation Example ===\n");

        var order = new OrderFlow("ORD-002", 599.99m);
        
        Console.WriteLine($"Order: {order.Id}, Amount: ${order.Amount:F2}");
        Console.WriteLine($"Initial State: {order.State}\n");

        // Submit the order
        Console.WriteLine("1. Submitting order...");
        order.Fire(OrderTrigger.Submit);

        // Cancel before processing
        Console.WriteLine("2. Cancelling order before payment...");
        order.Fire(OrderTrigger.Cancel);
        Console.WriteLine($"   State: {order.State}\n");

        Console.WriteLine("=== Order was cancelled ===\n");
    }

    public static void GuardFailureDemo()
    {
        Console.WriteLine("=== Guard Failure Example ===\n");

        var order = new OrderFlow("ORD-003", -50m); // Invalid amount
        
        Console.WriteLine($"Order: {order.Id}, Amount: ${order.Amount:F2}");
        Console.WriteLine($"Initial State: {order.State}\n");

        // Submit the order
        Console.WriteLine("1. Submitting order...");
        order.Fire(OrderTrigger.Submit);

        // Try to pay - guard will fail due to invalid amount
        Console.WriteLine("2. Attempting to pay with invalid amount...");
        if (order.CanFire(OrderTrigger.Pay))
        {
            Console.WriteLine("   Payment allowed");
        }
        else
        {
            Console.WriteLine("   Payment blocked by guard (invalid amount)\n");
        }

        Console.WriteLine("=== Guard prevented invalid payment ===\n");
    }
}

/// <summary>
/// Enum defining the possible states of an order.
/// </summary>
public enum OrderState
{
    Draft,
    Submitted,
    Paid,
    Shipped,
    Cancelled
}

/// <summary>
/// Enum defining the triggers that can transition an order between states.
/// </summary>
public enum OrderTrigger
{
    Submit,
    Pay,
    Ship,
    Cancel
}

/// <summary>
/// State machine for managing order lifecycle.
/// Uses the StateMachine generator to create deterministic state transitions.
/// </summary>
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

    // Transition: Draft -> Submitted
    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
    private void OnSubmit()
    {
        Console.WriteLine($"   >> Transition: Submitting order {Id}");
    }

    // Guard: Can only pay if amount is valid
    [StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
    private bool CanPay()
    {
        return Amount > 0;
    }

    // Transition: Submitted -> Paid (async)
    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
    private async ValueTask OnPayAsync(CancellationToken ct)
    {
        Console.WriteLine($"   >> Transition: Processing payment for {Id}...");
        // Simulate payment processing
        await Task.Delay(500, ct);
        Console.WriteLine($"   >> Payment of ${Amount:F2} processed");
    }

    // Exit hook: Called when leaving Paid state
    [StateExit(OrderState.Paid)]
    private void OnExitPaid()
    {
        Console.WriteLine($"   >> Exit Hook: Finalizing payment for {Id}");
    }

    // Transition: Paid -> Shipped
    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Ship, To = OrderState.Shipped)]
    private void OnShip()
    {
        Console.WriteLine($"   >> Transition: Shipping order {Id}");
    }

    // Entry hook: Called when entering Shipped state
    [StateEntry(OrderState.Shipped)]
    private void OnEnterShipped()
    {
        Console.WriteLine($"   >> Entry Hook: Order {Id} is now shipped, sending notification");
    }

    // Transitions for cancellation from multiple states
    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    private void OnCancel()
    {
        Console.WriteLine($"   >> Transition: Cancelling order {Id}");
    }

    // Entry hook: Called when entering Cancelled state
    [StateEntry(OrderState.Cancelled)]
    private void OnEnterCancelled()
    {
        Console.WriteLine($"   >> Entry Hook: Order {Id} is cancelled, processing refund if needed");
    }
}
