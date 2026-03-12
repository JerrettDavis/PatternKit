using PatternKit.Generators.Observer;

namespace PatternKit.Examples.ObserverGeneratorDemo;

/// <summary>
/// A notification message to be sent.
/// </summary>
/// <param name="RecipientId">ID of the recipient.</param>
/// <param name="Message">The notification message.</param>
/// <param name="Priority">Priority level (0=low, 1=normal, 2=high).</param>
public record Notification(string RecipientId, string Message, int Priority);

/// <summary>
/// Result of attempting to send a notification.
/// </summary>
/// <param name="Success">Whether the send was successful.</param>
/// <param name="Channel">Which channel was used (Email, SMS, Push).</param>
/// <param name="Error">Error message if failed.</param>
public record NotificationResult(bool Success, string Channel, string? Error = null);

/// <summary>
/// Observable event for notifications with async support.
/// Demonstrates async handlers and PublishAsync.
/// </summary>
[Observer(typeof(Notification), 
    Threading = ObserverThreadingPolicy.Locking,
    Exceptions = ObserverExceptionPolicy.Continue,
    GenerateAsync = true)]
public partial class NotificationPublished
{
    partial void OnSubscriberError(Exception ex)
    {
        Console.WriteLine($"❌ Notification handler error: {ex.Message}");
    }
}

/// <summary>
/// Observable event for notification results.
/// Uses Aggregate exception policy to collect all failures.
/// </summary>
[Observer(typeof(NotificationResult),
    Threading = ObserverThreadingPolicy.Locking,
    Exceptions = ObserverExceptionPolicy.Aggregate)]
public partial class NotificationSent
{
}

/// <summary>
/// Multi-channel notification system with async handlers.
/// </summary>
public class NotificationSystem
{
    private readonly NotificationPublished _notificationPublished = new();
    private readonly NotificationSent _notificationSent = new();
    private readonly Random _random = new();

    /// <summary>
    /// Subscribes to notifications with a synchronous handler.
    /// </summary>
    public IDisposable Subscribe(Action<Notification> handler) =>
        _notificationPublished.Subscribe(handler);

    /// <summary>
    /// Subscribes to notifications with an async handler.
    /// </summary>
    public IDisposable SubscribeAsync(Func<Notification, ValueTask> handler) =>
        _notificationPublished.Subscribe(handler);

    /// <summary>
    /// Subscribes to notification send results.
    /// </summary>
    public IDisposable OnNotificationSent(Action<NotificationResult> handler) =>
        _notificationSent.Subscribe(handler);

    /// <summary>
    /// Sends a notification through all registered channels asynchronously.
    /// </summary>
    public async Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n📤 Sending notification (Priority: {notification.Priority})...");
        await _notificationPublished.PublishAsync(notification, cancellationToken);
    }

    /// <summary>
    /// Reports that a notification was sent through a channel.
    /// </summary>
    public void ReportSent(NotificationResult result)
    {
        _notificationSent.Publish(result);
    }

    /// <summary>
    /// Simulates sending an email (async operation).
    /// </summary>
    public async Task<NotificationResult> SendEmailAsync(Notification notification)
    {
        await Task.Delay(100); // Simulate network delay
        
        // Simulate random failures (20% chance)
        if (_random.NextDouble() < 0.2)
        {
            return new NotificationResult(false, "Email", "SMTP server unavailable");
        }

        Console.WriteLine($"  ✉️  Email sent to {notification.RecipientId}");
        return new NotificationResult(true, "Email");
    }

    /// <summary>
    /// Simulates sending an SMS (async operation).
    /// </summary>
    public async Task<NotificationResult> SendSmsAsync(Notification notification)
    {
        await Task.Delay(80); // Simulate network delay
        
        // High priority only
        if (notification.Priority < 2)
        {
            return new NotificationResult(false, "SMS", "Priority too low for SMS");
        }

        Console.WriteLine($"  📱 SMS sent to {notification.RecipientId}");
        return new NotificationResult(true, "SMS");
    }

    /// <summary>
    /// Simulates sending a push notification (async operation).
    /// </summary>
    public async Task<NotificationResult> SendPushAsync(Notification notification)
    {
        await Task.Delay(50); // Simulate network delay
        Console.WriteLine($"  🔔 Push notification sent to {notification.RecipientId}");
        return new NotificationResult(true, "Push");
    }
}

/// <summary>
/// Demonstrates async handlers with PublishAsync.
/// </summary>
public static class AsyncNotificationDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Async Notification System ===\n");

        var system = new NotificationSystem();

        // Subscribe email channel (async handler)
        using var emailSub = system.SubscribeAsync(async notification =>
        {
            var result = await system.SendEmailAsync(notification);
            system.ReportSent(result);
        });

        // Subscribe SMS channel (async handler)
        using var smsSub = system.SubscribeAsync(async notification =>
        {
            var result = await system.SendSmsAsync(notification);
            system.ReportSent(result);
        });

        // Subscribe push channel (async handler)
        using var pushSub = system.SubscribeAsync(async notification =>
        {
            var result = await system.SendPushAsync(notification);
            system.ReportSent(result);
        });

        // Subscribe to results to track success/failure
        var successCount = 0;
        var failureCount = 0;
        using var resultSub = system.OnNotificationSent(result =>
        {
            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failureCount++;
                Console.WriteLine($"  ⚠️  {result.Channel} failed: {result.Error}");
            }
        });

        // Send notifications with different priorities
        var notifications = new[]
        {
            new Notification("user123", "Welcome to our service!", 1),
            new Notification("user456", "Your order has shipped", 1),
            new Notification("user789", "URGENT: Security alert", 2),
            new Notification("user999", "Daily digest available", 0)
        };

        foreach (var notification in notifications)
        {
            await system.SendAsync(notification);
            await Task.Delay(200); // Space out notifications
        }

        Console.WriteLine($"\n📊 Results: {successCount} successful, {failureCount} failed");
    }
}

/// <summary>
/// Demonstrates exception handling with different policies.
/// </summary>
public static class ExceptionHandlingDemo
{
    public static void Run()
    {
        Console.WriteLine("\n=== Exception Handling Demo ===\n");

        // Demo 1: Continue policy (default) - all handlers run despite errors
        Console.WriteLine("1. Continue Policy (fault-tolerant):");
        DemoContinuePolicy();

        // Demo 2: Aggregate policy - collect all errors
        Console.WriteLine("\n2. Aggregate Policy (collect all errors):");
        DemoAggregatePolicy();
    }

    private static void DemoContinuePolicy()
    {
        var notification = new NotificationPublished();
        
        // Handler 1: Works fine
        notification.Subscribe(n => 
            Console.WriteLine("  ✅ Handler 1: Success"));

        // Handler 2: Throws exception
        notification.Subscribe(n =>
        {
            Console.WriteLine("  ❌ Handler 2: Throwing exception...");
            throw new InvalidOperationException("Handler 2 failed");
        });

        // Handler 3: Also works fine
        notification.Subscribe(n =>
            Console.WriteLine("  ✅ Handler 3: Success (ran despite Handler 2 error)"));

        notification.Publish(new Notification("test", "Test message", 1));
        Console.WriteLine("  ℹ️  All handlers attempted, errors logged via OnSubscriberError");
    }

    private static void DemoAggregatePolicy()
    {
        var results = new NotificationSent();

        // Handler 1: Throws
        results.Subscribe(r =>
        {
            Console.WriteLine("  ❌ Validator 1: Failed");
            throw new InvalidOperationException("Validation 1 failed");
        });

        // Handler 2: Also throws
        results.Subscribe(r =>
        {
            Console.WriteLine("  ❌ Validator 2: Failed");
            throw new ArgumentException("Validation 2 failed");
        });

        // Handler 3: Would succeed
        results.Subscribe(r =>
            Console.WriteLine("  ✅ Validator 3: Success"));

        try
        {
            results.Publish(new NotificationResult(true, "Test"));
            Console.WriteLine("  ℹ️  No exception thrown (shouldn't reach here)");
        }
        catch (AggregateException ex)
        {
            Console.WriteLine($"  🔥 AggregateException caught with {ex.InnerExceptions.Count} errors:");
            foreach (var inner in ex.InnerExceptions)
            {
                Console.WriteLine($"     - {inner.GetType().Name}: {inner.Message}");
            }
        }
    }
}

/// <summary>
/// Demonstrates mixing sync and async handlers.
/// </summary>
public static class MixedHandlersDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n=== Mixed Sync/Async Handlers Demo ===\n");

        var notification = new NotificationPublished();

        // Sync handler
        notification.Subscribe(n =>
            Console.WriteLine($"  🔹 Sync handler: {n.Message}"));

        // Async handler
        notification.Subscribe(async n =>
        {
            await Task.Delay(50);
            Console.WriteLine($"  🔸 Async handler: {n.Message}");
        });

        // Another sync handler
        notification.Subscribe(n =>
            Console.WriteLine($"  🔹 Sync handler 2: Priority={n.Priority}"));

        Console.WriteLine("Publishing with Publish (sync):");
        notification.Publish(new Notification("user", "Hello World", 1));
        
        // Note: async handlers run fire-and-forget with Publish
        await Task.Delay(100); // Wait for async handlers
        
        Console.WriteLine("\nPublishing with PublishAsync (awaits async handlers):");
        await notification.PublishAsync(new Notification("user", "Goodbye World", 2));
        
        Console.WriteLine("\nNote: PublishAsync waits for all async handlers to complete.");
    }
}

/// <summary>
/// Demonstrates cancellation token support in async handlers.
/// </summary>
public static class CancellationDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("\n=== Cancellation Demo ===\n");

        var notification = new NotificationPublished();
        var processedCount = 0;

        // Long-running async handler  
        // Note: Cancellation is checked between handlers, not during handler execution
        notification.Subscribe(async n =>
        {
            Console.WriteLine("  ⏳ Starting long operation...");
            await Task.Delay(100); // Shorter delay for demo
            processedCount++;
            Console.WriteLine("  ✅ Long operation completed");
        });

        // Quick handler
        notification.Subscribe(async n =>
        {
            await Task.Delay(10);
            processedCount++;
            Console.WriteLine("  ✅ Quick operation completed");
        });

        using var cts = new CancellationTokenSource(50); // Cancel after 50ms - before first handler completes

        try
        {
            await notification.PublishAsync(
                new Notification("user", "Test", 1),
                cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n  ℹ️  PublishAsync was cancelled");
        }

        Console.WriteLine($"\n  Handlers completed: {processedCount}/2");
        Console.WriteLine("  Note: Cancellation is checked between handler invocations, not during execution");
    }
}
