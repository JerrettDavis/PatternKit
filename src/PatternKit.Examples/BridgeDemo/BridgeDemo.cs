using PatternKit.Structural.Bridge;

namespace PatternKit.Examples.BridgeDemo;

/// <summary>
/// Demonstrates the Bridge pattern for decoupling message sending abstraction from delivery implementations.
/// This example shows a notification system that can send through multiple channels.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real-world scenario:</b> A notification service that needs to send messages through
/// multiple channels (Email, SMS, Push, Slack) with different priorities and formatting.
/// </para>
/// <para>
/// <b>Key GoF concepts demonstrated:</b>
/// <list type="bullet">
/// <item>Abstraction (Message preparation, validation, logging)</item>
/// <item>Implementation (Actual delivery mechanism - Email, SMS, Push)</item>
/// <item>Decoupled evolution - abstractions and implementations can vary independently</item>
/// </list>
/// </para>
/// </remarks>
public static class BridgeDemo
{
    // ─────────────────────────────────────────────────────────────────────────
    // Implementation Interface - The "bridge" to different delivery mechanisms
    // ─────────────────────────────────────────────────────────────────────────

    public interface IMessageChannel
    {
        string Name { get; }
        bool IsAvailable { get; }
        int MaxMessageLength { get; }
        bool Send(string recipient, string subject, string body);
        void Connect();
        void Disconnect();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Concrete Implementations - Different delivery channels
    // ─────────────────────────────────────────────────────────────────────────

    public sealed class EmailChannel : IMessageChannel
    {
        private bool _connected;
        public string Name => "Email (SMTP)";
        public bool IsAvailable => _connected;
        public int MaxMessageLength => 50000;

        public void Connect()
        {
            Console.WriteLine("  📧 Connecting to SMTP server...");
            _connected = true;
        }

        public void Disconnect()
        {
            Console.WriteLine("  📧 Disconnecting from SMTP server");
            _connected = false;
        }

        public bool Send(string recipient, string subject, string body)
        {
            Console.WriteLine($"  📧 Sending email to: {recipient}");
            Console.WriteLine($"      Subject: {subject}");
            Console.WriteLine($"      Body length: {body.Length} chars");
            return true;
        }
    }

    public sealed class SmsChannel : IMessageChannel
    {
        private bool _connected;
        public string Name => "SMS (Twilio)";
        public bool IsAvailable => _connected;
        public int MaxMessageLength => 160;

        public void Connect()
        {
            Console.WriteLine("  📱 Initializing Twilio SMS gateway...");
            _connected = true;
        }

        public void Disconnect()
        {
            Console.WriteLine("  📱 Closing Twilio connection");
            _connected = false;
        }

        public bool Send(string recipient, string subject, string body)
        {
            var message = body.Length > 160 ? body[..157] + "..." : body;
            Console.WriteLine($"  📱 Sending SMS to: {recipient}");
            Console.WriteLine($"      Message: {message}");
            return true;
        }
    }

    public sealed class PushNotificationChannel : IMessageChannel
    {
        private bool _connected;
        public string Name => "Push (Firebase)";
        public bool IsAvailable => _connected;
        public int MaxMessageLength => 4096;

        public void Connect()
        {
            Console.WriteLine("  🔔 Connecting to Firebase Cloud Messaging...");
            _connected = true;
        }

        public void Disconnect()
        {
            Console.WriteLine("  🔔 Disconnecting from FCM");
            _connected = false;
        }

        public bool Send(string recipient, string subject, string body)
        {
            Console.WriteLine($"  🔔 Sending push notification to device: {recipient}");
            Console.WriteLine($"      Title: {subject}");
            Console.WriteLine($"      Body: {body[..Math.Min(50, body.Length)]}...");
            return true;
        }
    }

    public sealed class SlackChannel : IMessageChannel
    {
        private bool _connected;
        public string Name => "Slack (Webhook)";
        public bool IsAvailable => _connected;
        public int MaxMessageLength => 40000;

        public void Connect()
        {
            Console.WriteLine("  💬 Initializing Slack webhook...");
            _connected = true;
        }

        public void Disconnect()
        {
            Console.WriteLine("  💬 Closing Slack webhook");
            _connected = false;
        }

        public bool Send(string recipient, string subject, string body)
        {
            Console.WriteLine($"  💬 Posting to Slack channel: {recipient}");
            Console.WriteLine($"      *{subject}*");
            Console.WriteLine($"      {body[..Math.Min(80, body.Length)]}...");
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Message Types - The abstraction layer
    // ─────────────────────────────────────────────────────────────────────────

    public sealed record NotificationMessage(
        string Recipient,
        string Subject,
        string Body,
        NotificationPriority Priority = NotificationPriority.Normal);

    public enum NotificationPriority { Low, Normal, High, Critical }

    // ─────────────────────────────────────────────────────────────────────────
    // Bridge Configuration using PatternKit
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a notification bridge with the specified channel implementation.
    /// The bridge handles validation, logging, and error handling while delegating
    /// actual delivery to the channel.
    /// </summary>
    public static Bridge<NotificationMessage, bool, IMessageChannel> CreateNotificationBridge(
        IMessageChannel channel)
    {
        // Create a provider delegate that returns the channel
        Bridge<NotificationMessage, bool, IMessageChannel>.Provider provider = () => channel;

        return Bridge<NotificationMessage, bool, IMessageChannel>
            .Create(provider)

            // Validate before sending
            .Require((in NotificationMessage msg, IMessageChannel ch) =>
            {
                if (string.IsNullOrWhiteSpace(msg.Recipient))
                    return "Recipient is required";
                if (string.IsNullOrWhiteSpace(msg.Body))
                    return "Message body is required";
                if (msg.Body.Length > ch.MaxMessageLength)
                    return $"Message too long. Max: {ch.MaxMessageLength}, Got: {msg.Body.Length}";
                return null;
            })

            // Connect before sending
            .Before((in NotificationMessage msg, IMessageChannel ch) =>
            {
                Console.WriteLine($"  [Pre] Preparing to send via {ch.Name}");
                if (!ch.IsAvailable)
                    ch.Connect();
            })

            // Core operation - delegate to the channel
            .Operation((in NotificationMessage msg, IMessageChannel ch) =>
                ch.Send(msg.Recipient, msg.Subject, msg.Body))

            // Log after sending
            .After((in NotificationMessage msg, IMessageChannel ch, bool result) =>
            {
                var status = result ? "✓ Sent" : "✗ Failed";
                Console.WriteLine($"  [Post] {status} via {ch.Name} to {msg.Recipient}");
                return result;
            })

            // Validate result
            .RequireResult((in NotificationMessage msg, IMessageChannel _, in bool result) =>
                result ? null : "Delivery failed")

            .Build();
    }

    /// <summary>
    /// Creates an async notification bridge for high-priority messages.
    /// </summary>
    public static AsyncBridge<NotificationMessage, bool, IMessageChannel> CreateAsyncNotificationBridge(
        IMessageChannel channel)
    {
        AsyncBridge<NotificationMessage, bool, IMessageChannel>.Provider provider = _ => ValueTask.FromResult(channel);

        return AsyncBridge<NotificationMessage, bool, IMessageChannel>
            .Create(provider)

            .Require((msg, ch) =>
                string.IsNullOrWhiteSpace(msg.Recipient) ? "Recipient required" : null)

            .Before(async (msg, ch, ct) =>
            {
                Console.WriteLine($"  [Async Pre] Initializing {ch.Name}...");
                await Task.Delay(10, ct); // Simulate async init
                if (!ch.IsAvailable) ch.Connect();
            })

            .Operation(async (msg, ch, ct) =>
            {
                await Task.Delay(50, ct); // Simulate network latency
                return ch.Send(msg.Recipient, msg.Subject, msg.Body);
            })

            .After((msg, ch, result) =>
            {
                Console.WriteLine($"  [Async Post] Delivery complete: {(result ? "Success" : "Failed")}");
                return result;
            })

            .Build();
    }

    /// <summary>
    /// Runs the complete Bridge pattern demonstration.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║             BRIDGE PATTERN DEMONSTRATION                      ║");
        Console.WriteLine("║   Decoupled Notification System with Multiple Channels       ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

        // Create channels (implementations)
        var emailChannel = new EmailChannel();
        var smsChannel = new SmsChannel();
        var pushChannel = new PushNotificationChannel();
        var slackChannel = new SlackChannel();

        // Create bridges for each channel (abstraction + implementation)
        var emailBridge = CreateNotificationBridge(emailChannel);
        var smsBridge = CreateNotificationBridge(smsChannel);
        var pushBridge = CreateNotificationBridge(pushChannel);
        var asyncSlackBridge = CreateAsyncNotificationBridge(slackChannel);

        // Sample messages
        var orderConfirmation = new NotificationMessage(
            Recipient: "customer@example.com",
            Subject: "Order Confirmed",
            Body: "Your order #12345 has been confirmed and will ship within 2 business days.",
            Priority: NotificationPriority.Normal);

        var criticalAlert = new NotificationMessage(
            Recipient: "+1-555-0123",
            Subject: "CRITICAL",
            Body: "Server CPU at 95%! Immediate attention required.",
            Priority: NotificationPriority.Critical);

        var teamUpdate = new NotificationMessage(
            Recipient: "#engineering",
            Subject: "Deploy Complete",
            Body: "Version 2.5.0 has been successfully deployed to production.",
            Priority: NotificationPriority.Normal);

        // ── Scenario 1: Email notification ──
        Console.WriteLine("▶ Scenario 1: Email Notification");
        Console.WriteLine(new string('─', 50));
        var emailResult = emailBridge.Execute(orderConfirmation);
        Console.WriteLine($"  Result: {(emailResult ? "Success" : "Failed")}\n");

        // ── Scenario 2: SMS Alert ──
        Console.WriteLine("▶ Scenario 2: SMS Critical Alert");
        Console.WriteLine(new string('─', 50));
        var smsResult = smsBridge.Execute(criticalAlert);
        Console.WriteLine($"  Result: {(smsResult ? "Success" : "Failed")}\n");

        // ── Scenario 3: Push Notification ──
        Console.WriteLine("▶ Scenario 3: Push Notification");
        Console.WriteLine(new string('─', 50));
        var pushResult = pushBridge.Execute(orderConfirmation);
        Console.WriteLine($"  Result: {(pushResult ? "Success" : "Failed")}\n");

        // ── Scenario 4: Async Slack Message ──
        Console.WriteLine("▶ Scenario 4: Async Slack Channel Post");
        Console.WriteLine(new string('─', 50));
        var slackResult = await asyncSlackBridge.ExecuteAsync(teamUpdate);
        Console.WriteLine($"  Result: {(slackResult ? "Success" : "Failed")}\n");

        // ── Scenario 5: Validation Error ──
        Console.WriteLine("▶ Scenario 5: Validation Error (Message Too Long)");
        Console.WriteLine(new string('─', 50));
        var longMessage = new NotificationMessage(
            Recipient: "+1-555-0123",
            Subject: "Test",
            Body: new string('x', 200), // SMS max is 160
            Priority: NotificationPriority.Low);

        if (!smsBridge.TryExecute(longMessage, out var tryResult, out var error))
        {
            Console.WriteLine($"  ✗ Validation failed: {error}\n");
        }

        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("Pattern Benefits Demonstrated:");
        Console.WriteLine("  • Abstraction (validation, logging) is separate from implementation");
        Console.WriteLine("  • Same abstraction works with Email, SMS, Push, or Slack");
        Console.WriteLine("  • New channels can be added without changing abstraction");
        Console.WriteLine("  • Implementation can be swapped at runtime");
        Console.WriteLine("  • Sync and async variants share the same design");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
    }

    public static void Run() => RunAsync().GetAwaiter().GetResult();
}
