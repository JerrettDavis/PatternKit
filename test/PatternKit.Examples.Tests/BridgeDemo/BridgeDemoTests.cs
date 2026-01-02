using PatternKit.Examples.BridgeDemo;
using static PatternKit.Examples.BridgeDemo.BridgeDemo;

namespace PatternKit.Examples.Tests.BridgeDemoTests;

public sealed class BridgeDemoTests
{
    [Fact]
    public void EmailChannel_Connect_And_Send()
    {
        var channel = new EmailChannel();

        Assert.Equal("Email (SMTP)", channel.Name);
        Assert.False(channel.IsAvailable);
        Assert.Equal(50000, channel.MaxMessageLength);

        channel.Connect();
        Assert.True(channel.IsAvailable);

        var result = channel.Send("test@example.com", "Subject", "Body");
        Assert.True(result);

        channel.Disconnect();
        Assert.False(channel.IsAvailable);
    }

    [Fact]
    public void SmsChannel_Connect_And_Send()
    {
        var channel = new SmsChannel();

        Assert.Equal("SMS (Twilio)", channel.Name);
        Assert.Equal(160, channel.MaxMessageLength);

        channel.Connect();
        Assert.True(channel.IsAvailable);

        var result = channel.Send("+1555123456", "Alert", "Short message");
        Assert.True(result);
    }

    [Fact]
    public void SmsChannel_Truncates_Long_Messages()
    {
        var channel = new SmsChannel();
        channel.Connect();

        var longMessage = new string('x', 200);
        var result = channel.Send("+1555123456", "Test", longMessage);
        Assert.True(result);
    }

    [Fact]
    public void PushNotificationChannel_Connect_And_Send()
    {
        var channel = new PushNotificationChannel();

        Assert.Equal("Push (Firebase)", channel.Name);
        Assert.Equal(4096, channel.MaxMessageLength);

        channel.Connect();
        Assert.True(channel.IsAvailable);

        var result = channel.Send("device123", "Push Title", "Push body message here");
        Assert.True(result);

        channel.Disconnect();
        Assert.False(channel.IsAvailable);
    }

    [Fact]
    public void SlackChannel_Connect_And_Send()
    {
        var channel = new SlackChannel();

        Assert.Equal("Slack (Webhook)", channel.Name);
        Assert.Equal(40000, channel.MaxMessageLength);

        channel.Connect();
        Assert.True(channel.IsAvailable);

        var result = channel.Send("#general", "Slack Title", "Slack message content that is longer than typical");
        Assert.True(result);
    }

    [Fact]
    public void NotificationMessage_Record_Works()
    {
        var msg = new NotificationMessage(
            Recipient: "user@example.com",
            Subject: "Test Subject",
            Body: "Test Body",
            Priority: NotificationPriority.High);

        Assert.Equal("user@example.com", msg.Recipient);
        Assert.Equal("Test Subject", msg.Subject);
        Assert.Equal("Test Body", msg.Body);
        Assert.Equal(NotificationPriority.High, msg.Priority);
    }

    [Fact]
    public void CreateNotificationBridge_Email_Success()
    {
        var channel = new EmailChannel();
        var bridge = CreateNotificationBridge(channel);

        var msg = new NotificationMessage(
            Recipient: "user@example.com",
            Subject: "Hello",
            Body: "World");

        var result = bridge.Execute(msg);

        Assert.True(result);
    }

    [Fact]
    public void CreateNotificationBridge_Validation_Fails_Empty_Recipient()
    {
        var channel = new EmailChannel();
        var bridge = CreateNotificationBridge(channel);

        var msg = new NotificationMessage(
            Recipient: "",
            Subject: "Hello",
            Body: "World");

        var success = bridge.TryExecute(msg, out var result, out var error);

        Assert.False(success);
        Assert.Contains("Recipient", error);
    }

    [Fact]
    public void CreateNotificationBridge_Validation_Fails_Empty_Body()
    {
        var channel = new EmailChannel();
        var bridge = CreateNotificationBridge(channel);

        var msg = new NotificationMessage(
            Recipient: "user@example.com",
            Subject: "Hello",
            Body: "");

        var success = bridge.TryExecute(msg, out var result, out var error);

        Assert.False(success);
        Assert.Contains("body", error);
    }

    [Fact]
    public void CreateNotificationBridge_Validation_Fails_Message_Too_Long()
    {
        var channel = new SmsChannel();
        var bridge = CreateNotificationBridge(channel);

        var msg = new NotificationMessage(
            Recipient: "+1555123456",
            Subject: "Test",
            Body: new string('x', 200)); // SMS max is 160

        var success = bridge.TryExecute(msg, out var result, out var error);

        Assert.False(success);
        Assert.Contains("too long", error);
    }

    [Fact]
    public async Task CreateAsyncNotificationBridge_Success()
    {
        var channel = new SlackChannel();
        var bridge = CreateAsyncNotificationBridge(channel);

        var msg = new NotificationMessage(
            Recipient: "#engineering",
            Subject: "Deploy Complete",
            Body: "Version 2.0 deployed.");

        var result = await bridge.ExecuteAsync(msg);

        Assert.True(result);
    }

    [Fact]
    public async Task CreateAsyncNotificationBridge_Validation_Fails()
    {
        var channel = new SlackChannel();
        var bridge = CreateAsyncNotificationBridge(channel);

        var msg = new NotificationMessage(
            Recipient: "",
            Subject: "Test",
            Body: "Body");

        var (success, _, error) = await bridge.TryExecuteAsync(msg);

        Assert.False(success);
        Assert.Contains("Recipient", error);
    }

    [Fact]
    public async Task RunAsync_Executes_Without_Errors()
    {
        await PatternKit.Examples.BridgeDemo.BridgeDemo.RunAsync();
    }

    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.BridgeDemo.BridgeDemo.Run();
    }
}
