using PatternKit.Examples.BridgeDemo;
using TinyBDD;
using static PatternKit.Examples.BridgeDemo.BridgeDemo;

namespace PatternKit.Examples.Tests.BridgeDemoTests;

public sealed class BridgeDemoTests
{
    [Scenario("EmailChannel Connect And Send")]
    [Fact]
    public void EmailChannel_Connect_And_Send()
    {
        var channel = new EmailChannel();

        ScenarioExpect.Equal("Email (SMTP)", channel.Name);
        ScenarioExpect.False(channel.IsAvailable);
        ScenarioExpect.Equal(50000, channel.MaxMessageLength);

        channel.Connect();
        ScenarioExpect.True(channel.IsAvailable);

        var result = channel.Send("test@example.com", "Subject", "Body");
        ScenarioExpect.True(result);

        channel.Disconnect();
        ScenarioExpect.False(channel.IsAvailable);
    }

    [Scenario("SmsChannel Connect And Send")]
    [Fact]
    public void SmsChannel_Connect_And_Send()
    {
        var channel = new SmsChannel();

        ScenarioExpect.Equal("SMS (Twilio)", channel.Name);
        ScenarioExpect.Equal(160, channel.MaxMessageLength);

        channel.Connect();
        ScenarioExpect.True(channel.IsAvailable);

        var result = channel.Send("+1555123456", "Alert", "Short message");
        ScenarioExpect.True(result);
    }

    [Scenario("SmsChannel Truncates Long Messages")]
    [Fact]
    public void SmsChannel_Truncates_Long_Messages()
    {
        var channel = new SmsChannel();
        channel.Connect();

        var longMessage = new string('x', 200);
        var result = channel.Send("+1555123456", "Test", longMessage);
        ScenarioExpect.True(result);
    }

    [Scenario("PushNotificationChannel Connect And Send")]
    [Fact]
    public void PushNotificationChannel_Connect_And_Send()
    {
        var channel = new PushNotificationChannel();

        ScenarioExpect.Equal("Push (Firebase)", channel.Name);
        ScenarioExpect.Equal(4096, channel.MaxMessageLength);

        channel.Connect();
        ScenarioExpect.True(channel.IsAvailable);

        var result = channel.Send("device123", "Push Title", "Push body message here");
        ScenarioExpect.True(result);

        channel.Disconnect();
        ScenarioExpect.False(channel.IsAvailable);
    }

    [Scenario("SlackChannel Connect And Send")]
    [Fact]
    public void SlackChannel_Connect_And_Send()
    {
        var channel = new SlackChannel();

        ScenarioExpect.Equal("Slack (Webhook)", channel.Name);
        ScenarioExpect.Equal(40000, channel.MaxMessageLength);

        channel.Connect();
        ScenarioExpect.True(channel.IsAvailable);

        var result = channel.Send("#general", "Slack Title", "Slack message content that is longer than typical");
        ScenarioExpect.True(result);
    }

    [Scenario("NotificationMessage Record Works")]
    [Fact]
    public void NotificationMessage_Record_Works()
    {
        var msg = new NotificationMessage(
            Recipient: "user@example.com",
            Subject: "Test Subject",
            Body: "Test Body",
            Priority: NotificationPriority.High);

        ScenarioExpect.Equal("user@example.com", msg.Recipient);
        ScenarioExpect.Equal("Test Subject", msg.Subject);
        ScenarioExpect.Equal("Test Body", msg.Body);
        ScenarioExpect.Equal(NotificationPriority.High, msg.Priority);
    }

    [Scenario("CreateNotificationBridge Email Success")]
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

        ScenarioExpect.True(result);
    }

    [Scenario("CreateNotificationBridge Validation Fails Empty Recipient")]
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

        ScenarioExpect.False(success);
        ScenarioExpect.Contains("Recipient", error);
    }

    [Scenario("CreateNotificationBridge Validation Fails Empty Body")]
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

        ScenarioExpect.False(success);
        ScenarioExpect.Contains("body", error);
    }

    [Scenario("CreateNotificationBridge Validation Fails Message Too Long")]
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

        ScenarioExpect.False(success);
        ScenarioExpect.Contains("too long", error);
    }

    [Scenario("CreateAsyncNotificationBridge Success")]
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

        ScenarioExpect.True(result);
    }

    [Scenario("CreateAsyncNotificationBridge Validation Fails")]
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

        ScenarioExpect.False(success);
        ScenarioExpect.Contains("Recipient", error);
    }

    [Scenario("RunAsync Executes Without Errors")]
    [Fact]
    public async Task RunAsync_Executes_Without_Errors()
    {
        await PatternKit.Examples.BridgeDemo.BridgeDemo.RunAsync();
    }

    [Scenario("Run Executes Without Errors")]
    [Fact]
    public void Run_Executes_Without_Errors()
    {
        PatternKit.Examples.BridgeDemo.BridgeDemo.Run();
    }
}
