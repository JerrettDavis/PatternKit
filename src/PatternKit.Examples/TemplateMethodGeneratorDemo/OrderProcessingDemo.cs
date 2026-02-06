using PatternKit.Generators.Template;

namespace PatternKit.Examples.TemplateMethodGeneratorDemo;

/// <summary>
/// Context for async order processing workflow.
/// </summary>
public class OrderContext
{
    public string OrderId { get; set; } = "";
    public decimal Amount { get; set; }
    public string Customer { get; set; } = "";
    public List<string> Log { get; set; } = new();
    public bool PaymentAuthorized { get; set; }
    public bool InventoryReserved { get; set; }
    public bool OrderConfirmed { get; set; }
}

/// <summary>
/// Async order processing workflow using Template Method pattern.
/// Demonstrates async/await with ValueTask and CancellationToken support.
/// </summary>
[Template(GenerateAsync = true)]
public partial class OrderProcessingWorkflow
{
    /// <summary>
    /// Invoked before processing starts.
    /// </summary>
    [TemplateHook(HookPoint.BeforeAll)]
    private void OnStart(OrderContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Starting order processing for Order #{ctx.OrderId}");
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Customer: {ctx.Customer}, Amount: ${ctx.Amount:F2}");
    }

    /// <summary>
    /// Step 1: Authorize payment asynchronously.
    /// </summary>
    [TemplateStep(0, Name = "AuthorizePayment")]
    private async ValueTask AuthorizePaymentAsync(OrderContext ctx, CancellationToken ct)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Authorizing payment...");

        // Simulate async payment authorization
        await Task.Delay(100, ct);

        if (ctx.Amount > 0)
        {
            ctx.PaymentAuthorized = true;
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Payment authorized: ${ctx.Amount:F2}");
        }
        else
        {
            throw new InvalidOperationException("Invalid payment amount");
        }
    }

    /// <summary>
    /// Step 2: Reserve inventory asynchronously.
    /// </summary>
    [TemplateStep(1, Name = "ReserveInventory")]
    private async ValueTask ReserveInventoryAsync(OrderContext ctx, CancellationToken ct)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Reserving inventory...");

        // Simulate async inventory check
        await Task.Delay(100, ct);

        ctx.InventoryReserved = true;
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Inventory reserved");
    }

    /// <summary>
    /// Step 3: Confirm order (synchronous step in async workflow).
    /// </summary>
    [TemplateStep(2, Name = "ConfirmOrder")]
    private void ConfirmOrder(OrderContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Confirming order...");

        if (ctx.PaymentAuthorized && ctx.InventoryReserved)
        {
            ctx.OrderConfirmed = true;
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Order confirmed: #{ctx.OrderId}");
        }
    }

    /// <summary>
    /// Step 4: Send notification email asynchronously.
    /// </summary>
    [TemplateStep(3, Name = "SendNotification")]
    private async ValueTask SendNotificationAsync(OrderContext ctx, CancellationToken ct)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Sending notification to {ctx.Customer}...");

        // Simulate async email sending
        await Task.Delay(100, ct);

        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Notification sent");
    }

    /// <summary>
    /// Invoked when any step fails.
    /// </summary>
    [TemplateHook(HookPoint.OnError)]
    private void OnError(OrderContext ctx, Exception ex)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] ERROR: {ex.Message}");
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Order processing failed");

        // Compensating actions would go here (e.g., release inventory, refund payment)
        if (ctx.InventoryReserved)
        {
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Rolling back inventory reservation");
        }

        if (ctx.PaymentAuthorized)
        {
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Rolling back payment authorization");
        }
    }

    /// <summary>
    /// Invoked after successful completion.
    /// </summary>
    [TemplateHook(HookPoint.AfterAll)]
    private void OnComplete(OrderContext ctx)
    {
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Order processing completed successfully");
        ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Order #{ctx.OrderId} is ready for fulfillment");
    }
}

/// <summary>
/// Demo runner for async order processing workflow.
/// </summary>
public static class OrderProcessingDemo
{
    public static async Task<List<string>> RunAsync(string orderId = "ORD-001", string customer = "John Doe", decimal amount = 99.99m)
    {
        var ctx = new OrderContext
        {
            OrderId = orderId,
            Customer = customer,
            Amount = amount
        };

        var workflow = new OrderProcessingWorkflow();

        try
        {
            await workflow.ExecuteAsync(ctx);
        }
        catch (Exception)
        {
            // Exception was logged by OnError hook
        }

        return ctx.Log;
    }

    public static async Task<List<string>> RunWithInvalidAmountAsync()
    {
        var ctx = new OrderContext
        {
            OrderId = "ORD-INVALID",
            Customer = "Jane Smith",
            Amount = -50.00m  // Invalid amount will cause failure
        };

        var workflow = new OrderProcessingWorkflow();

        try
        {
            await workflow.ExecuteAsync(ctx);
        }
        catch (Exception)
        {
            // Exception was logged by OnError hook
        }

        return ctx.Log;
    }

    public static async Task<List<string>> RunWithCancellationAsync(CancellationToken ct)
    {
        var ctx = new OrderContext
        {
            OrderId = "ORD-CANCEL",
            Customer = "Bob Johnson",
            Amount = 150.00m
        };

        var workflow = new OrderProcessingWorkflow();

        try
        {
            await workflow.ExecuteAsync(ctx, ct);
        }
        catch (OperationCanceledException)
        {
            ctx.Log.Add($"[{DateTime.UtcNow:HH:mm:ss}] Order processing was cancelled");
        }
        catch (Exception)
        {
            // Exception was logged by OnError hook
        }

        return ctx.Log;
    }
}
