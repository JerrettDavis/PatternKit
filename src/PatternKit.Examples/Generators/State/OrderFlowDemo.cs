using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PatternKit.Generators.State;

namespace PatternKit.Examples.Generators.State;

/// <summary>
/// Real-world example of a State Machine generator demonstrating an order processing workflow.
/// This comprehensive example shows:
/// - Enum-based states and triggers
/// - Synchronous and asynchronous transitions
/// - Guards to control transitions based on business rules
/// - Entry and exit hooks for state changes
/// - Proper cancellation token handling
/// - Multiple scenarios (happy path, cancellation, guard failures)
/// - Error handling with different policies
/// </summary>
public static class OrderFlowDemo
{
    /// <summary>
    /// Runs the main order flow demonstration showing a complete happy-path workflow.
    /// </summary>
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

    /// <summary>
    /// Demonstrates order cancellation from different states.
    /// </summary>
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

    /// <summary>
    /// Demonstrates guard failure when business rules prevent a transition.
    /// </summary>
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

    /// <summary>
    /// Demonstrates async cancellation token handling.
    /// </summary>
    public static async Task AsyncCancellationDemo()
    {
        Console.WriteLine("=== Async Cancellation Example ===\n");

        var order = new OrderFlow("ORD-004", 450m);
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        Console.WriteLine($"Order: {order.Id}, Amount: ${order.Amount:F2}");
        Console.WriteLine("Timeout set to 100ms for 500ms operation\n");

        order.Fire(OrderTrigger.Submit);

        try
        {
            Console.WriteLine("Processing payment with timeout...");
            await order.FireAsync(OrderTrigger.Pay, cts.Token);
            Console.WriteLine("Payment completed successfully");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Payment operation was cancelled due to timeout");
            order.Fire(OrderTrigger.Cancel);
            Console.WriteLine($"Order cancelled. Final state: {order.State}\n");
        }

        Console.WriteLine("=== Async cancellation handled ===\n");
    }

    /// <summary>
    /// Demonstrates state-based decision making.
    /// </summary>
    public static void StateBasedLogicDemo()
    {
        Console.WriteLine("=== State-Based Logic Example ===\n");

        var order = new OrderFlow("ORD-005", 199.99m);

        // Function to display available actions based on current state
        void ShowAvailableActions(OrderFlow o)
        {
            Console.WriteLine($"Current state: {o.State}");
            Console.WriteLine("Available actions:");
            
            foreach (OrderTrigger trigger in Enum.GetValues(typeof(OrderTrigger)))
            {
                if (o.CanFire(trigger))
                {
                    Console.WriteLine($"  - {trigger}");
                }
            }
            Console.WriteLine();
        }

        ShowAvailableActions(order);

        order.Fire(OrderTrigger.Submit);
        ShowAvailableActions(order);

        order.FireAsync(OrderTrigger.Pay, CancellationToken.None).GetAwaiter().GetResult();
        ShowAvailableActions(order);

        Console.WriteLine("=== State-based logic complete ===\n");
    }

    /// <summary>
    /// Runs all demonstration scenarios.
    /// </summary>
    public static async Task RunAllDemosAsync()
    {
        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║  State Machine Pattern - Complete Demo Suite  ║");
        Console.WriteLine("╔════════════════════════════════════════════════╗\n");

        Run();
        await Task.Delay(500);

        CancellationDemo();
        await Task.Delay(500);

        GuardFailureDemo();
        await Task.Delay(500);

        await AsyncCancellationDemo();
        await Task.Delay(500);

        StateBasedLogicDemo();

        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║           All Demonstrations Complete          ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝\n");
    }
}

/// <summary>
/// Enum defining the possible states of an order in the fulfillment workflow.
/// </summary>
public enum OrderState
{
    /// <summary>Initial state - order is being prepared</summary>
    Draft,
    
    /// <summary>Order has been submitted for processing</summary>
    Submitted,
    
    /// <summary>Payment has been successfully processed</summary>
    Paid,
    
    /// <summary>Order has been shipped to the customer</summary>
    Shipped,
    
    /// <summary>Order was cancelled and will not be processed</summary>
    Cancelled
}

/// <summary>
/// Enum defining the triggers that can cause state transitions.
/// </summary>
public enum OrderTrigger
{
    /// <summary>Submit the order for processing</summary>
    Submit,
    
    /// <summary>Process payment for the order</summary>
    Pay,
    
    /// <summary>Ship the order to the customer</summary>
    Ship,
    
    /// <summary>Cancel the order</summary>
    Cancel
}

/// <summary>
/// State machine for managing order lifecycle using the State Pattern Generator.
/// Demonstrates deterministic state transitions with guards, hooks, and async support.
/// 
/// State Flow:
///   Draft -> Submit -> Submitted -> Pay -> Paid -> Ship -> Shipped
///   Draft/Submitted/Paid can -> Cancel -> Cancelled
/// </summary>
[StateMachine(typeof(OrderState), typeof(OrderTrigger))]
public partial class OrderFlow
{
    /// <summary>
    /// Gets the unique identifier for this order.
    /// </summary>
    public string Id { get; }
    
    /// <summary>
    /// Gets the order amount.
    /// </summary>
    public decimal Amount { get; }
    
    /// <summary>
    /// Initializes a new instance of the OrderFlow state machine.
    /// </summary>
    /// <param name="id">Unique order identifier</param>
    /// <param name="amount">Order amount in dollars</param>
    public OrderFlow(string id, decimal amount)
    {
        Id = id;
        Amount = amount;
        State = OrderState.Draft; // Set initial state
    }

    #region Transitions

    /// <summary>
    /// Handles the submission of an order from Draft to Submitted state.
    /// </summary>
    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Submit, To = OrderState.Submitted)]
    private void OnSubmit()
    {
        Console.WriteLine($"   >> Transition: Submitting order {Id}");
        // In a real system: Validate order data, reserve inventory, etc.
    }

    /// <summary>
    /// Guard that validates whether payment can be processed.
    /// Checks that the amount is valid (greater than 0).
    /// </summary>
    [StateGuard(From = OrderState.Submitted, Trigger = OrderTrigger.Pay)]
    private bool CanPay()
    {
        // Business rule: Amount must be positive
        return Amount > 0;
    }

    /// <summary>
    /// Processes payment asynchronously and transitions from Submitted to Paid state.
    /// Simulates payment processing with a delay.
    /// </summary>
    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Pay, To = OrderState.Paid)]
    private async ValueTask OnPayAsync(CancellationToken ct)
    {
        Console.WriteLine($"   >> Transition: Processing payment for {Id}...");
        
        // Simulate payment processing
        await Task.Delay(500, ct);
        
        // In a real system: 
        // - Call payment gateway
        // - Update payment records
        // - Generate receipt
        
        Console.WriteLine($"   >> Payment of ${Amount:F2} processed");
    }

    /// <summary>
    /// Exit hook executed when leaving the Paid state.
    /// Performs cleanup and finalization tasks.
    /// </summary>
    [StateExit(OrderState.Paid)]
    private void OnExitPaid()
    {
        Console.WriteLine($"   >> Exit Hook: Finalizing payment for {Id}");
        // In a real system: 
        // - Send payment confirmation email
        // - Update accounting system
        // - Notify warehouse
    }

    /// <summary>
    /// Handles the shipping of an order from Paid to Shipped state.
    /// </summary>
    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Ship, To = OrderState.Shipped)]
    private void OnShip()
    {
        Console.WriteLine($"   >> Transition: Shipping order {Id}");
        // In a real system:
        // - Generate shipping label
        // - Notify shipping carrier
        // - Update inventory
    }

    /// <summary>
    /// Entry hook executed when entering the Shipped state.
    /// Sends notifications and updates tracking.
    /// </summary>
    [StateEntry(OrderState.Shipped)]
    private void OnEnterShipped()
    {
        Console.WriteLine($"   >> Entry Hook: Order {Id} is now shipped, sending notification");
        // In a real system:
        // - Send shipping notification email with tracking
        // - Update customer portal
        // - Start delivery monitoring
    }

    /// <summary>
    /// Handles order cancellation from multiple states.
    /// Can be triggered from Draft, Submitted, or Paid states.
    /// </summary>
    [StateTransition(From = OrderState.Draft, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    [StateTransition(From = OrderState.Submitted, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    [StateTransition(From = OrderState.Paid, Trigger = OrderTrigger.Cancel, To = OrderState.Cancelled)]
    private void OnCancel()
    {
        Console.WriteLine($"   >> Transition: Cancelling order {Id}");
        // In a real system:
        // - Release reserved inventory
        // - Cancel payment authorization
        // - Log cancellation reason
    }

    /// <summary>
    /// Entry hook executed when entering the Cancelled state.
    /// Processes refunds and cleanup.
    /// </summary>
    [StateEntry(OrderState.Cancelled)]
    private void OnEnterCancelled()
    {
        Console.WriteLine($"   >> Entry Hook: Order {Id} is cancelled, processing refund if needed");
        // In a real system:
        // - Issue refund if payment was processed
        // - Send cancellation confirmation
        // - Update analytics
    }

    #endregion

    /// <summary>
    /// Gets a human-readable description of the current state.
    /// </summary>
    public string GetStateDescription()
    {
        return State switch
        {
            OrderState.Draft => "Order is being prepared",
            OrderState.Submitted => "Order is waiting for payment",
            OrderState.Paid => "Payment received, preparing for shipment",
            OrderState.Shipped => "Order is on its way to you",
            OrderState.Cancelled => "Order has been cancelled",
            _ => "Unknown state"
        };
    }

    /// <summary>
    /// Gets all triggers that are valid for the current state.
    /// </summary>
    public IEnumerable<OrderTrigger> GetAvailableTriggers()
    {
        foreach (OrderTrigger trigger in Enum.GetValues(typeof(OrderTrigger)))
        {
            if (CanFire(trigger))
            {
                yield return trigger;
            }
        }
    }
}

/// <summary>
/// Example of a more complex state machine with additional business logic.
/// Demonstrates a document approval workflow.
/// </summary>
public enum DocumentState
{
    Draft,
    PendingReview,
    Approved,
    Rejected,
    Published,
    Archived
}

public enum DocumentAction
{
    SubmitForReview,
    Approve,
    Reject,
    Revise,
    Publish,
    Archive
}

/// <summary>
/// Document approval workflow state machine.
/// </summary>
[StateMachine(typeof(DocumentState), typeof(DocumentAction))]
public partial class DocumentWorkflow
{
    public string DocumentId { get; }
    public string Author { get; }
    public List<string> ReviewComments { get; } = new();
    
    public DocumentWorkflow(string documentId, string author)
    {
        DocumentId = documentId;
        Author = author;
        State = DocumentState.Draft;
    }

    [StateTransition(From = DocumentState.Draft, Trigger = DocumentAction.SubmitForReview, To = DocumentState.PendingReview)]
    private void OnSubmitForReview()
    {
        Console.WriteLine($"Document {DocumentId} submitted for review by {Author}");
    }

    [StateGuard(From = DocumentState.PendingReview, Trigger = DocumentAction.Approve)]
    private bool CanApprove()
    {
        // Business rule: Must have at least one review comment
        return ReviewComments.Count > 0;
    }

    [StateTransition(From = DocumentState.PendingReview, Trigger = DocumentAction.Approve, To = DocumentState.Approved)]
    private void OnApprove()
    {
        Console.WriteLine($"Document {DocumentId} approved");
    }

    [StateTransition(From = DocumentState.PendingReview, Trigger = DocumentAction.Reject, To = DocumentState.Rejected)]
    private void OnReject()
    {
        Console.WriteLine($"Document {DocumentId} rejected");
    }

    [StateTransition(From = DocumentState.Rejected, Trigger = DocumentAction.Revise, To = DocumentState.Draft)]
    private void OnRevise()
    {
        ReviewComments.Clear();
        Console.WriteLine($"Document {DocumentId} sent back to draft for revision");
    }

    [StateTransition(From = DocumentState.Approved, Trigger = DocumentAction.Publish, To = DocumentState.Published)]
    private async ValueTask OnPublishAsync(CancellationToken ct)
    {
        Console.WriteLine($"Publishing document {DocumentId}...");
        await Task.Delay(300, ct); // Simulate publishing
        Console.WriteLine($"Document {DocumentId} published");
    }

    [StateTransition(From = DocumentState.Published, Trigger = DocumentAction.Archive, To = DocumentState.Archived)]
    private void OnArchive()
    {
        Console.WriteLine($"Document {DocumentId} archived");
    }
}
