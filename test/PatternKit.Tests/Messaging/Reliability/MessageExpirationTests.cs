using PatternKit.Messaging;
using PatternKit.Messaging.Reliability;
using TinyBDD;

namespace PatternKit.Tests.Messaging.Reliability;

public sealed class MessageExpirationTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Scenario("Stamp Applies Default Ttl")]
    [Fact]
    public void Stamp_Applies_Default_Ttl()
    {
        var expiration = MessageExpiration<Order>.Create()
            .Name("order-expiration")
            .DefaultTtl(TimeSpan.FromMinutes(15))
            .Clock(() => Now)
            .Build();

        var stamped = expiration.Stamp(Message<Order>.Create(new Order("o-1")));

        ScenarioExpect.Equal("expires-at", expiration.HeaderName);
        ScenarioExpect.Equal(Now.AddMinutes(15), expiration.Read(stamped));
    }

    [Scenario("Stamp Preserves Existing Deadline By Default")]
    [Fact]
    public void Stamp_Preserves_Existing_Deadline_By_Default()
    {
        var existing = Now.AddMinutes(5);
        var expiration = MessageExpiration<Order>.Create()
            .DefaultTtl(TimeSpan.FromMinutes(15))
            .Clock(() => Now)
            .Build();
        var message = Message<Order>.Create(new Order("o-1")).WithHeader("expires-at", existing);

        var stamped = expiration.Stamp(message);

        ScenarioExpect.Same(message, stamped);
        ScenarioExpect.Equal(existing, expiration.Read(stamped));
    }

    [Scenario("Stamp Can Replace Existing Deadline")]
    [Fact]
    public void Stamp_Can_Replace_Existing_Deadline()
    {
        var expiration = MessageExpiration<Order>.Create()
            .DefaultTtl(TimeSpan.FromMinutes(15))
            .Clock(() => Now)
            .PreserveExisting(false)
            .Build();
        var message = Message<Order>.Create(new Order("o-1")).WithHeader("expires-at", Now.AddMinutes(5));

        var stamped = expiration.Stamp(message);

        ScenarioExpect.NotSame(message, stamped);
        ScenarioExpect.Equal(Now.AddMinutes(15), expiration.Read(stamped));
    }

    [Scenario("Evaluate Accepts Fresh Messages")]
    [Fact]
    public void Evaluate_Accepts_Fresh_Messages()
    {
        var expiration = MessageExpiration<Order>.Create()
            .Name("order-expiration")
            .Clock(() => Now)
            .Build();
        var message = Message<Order>.Create(new Order("o-1")).WithHeader("expires-at", Now.AddSeconds(1));

        var result = expiration.Evaluate(message);

        ScenarioExpect.False(result.Expired);
        ScenarioExpect.Equal("order-expiration", result.PolicyName);
        ScenarioExpect.Equal(Now, result.CheckedAt);
        ScenarioExpect.Equal(Now.AddSeconds(1), result.ExpiresAt);
        ScenarioExpect.Null(result.Reason);
    }

    [Scenario("Evaluate Rejects Expired Messages")]
    [Fact]
    public void Evaluate_Rejects_Expired_Messages()
    {
        var expiration = MessageExpiration<Order>.Create()
            .ExpiredReason("Order command expired.")
            .Clock(() => Now)
            .Build();
        var message = Message<Order>.Create(new Order("o-1")).WithHeader("expires-at", Now);

        var result = expiration.Evaluate(message);

        ScenarioExpect.True(result.Expired);
        ScenarioExpect.Equal("Order command expired.", result.Reason);
    }

    [Scenario("Evaluate Uses Context Deadline When Message Has None")]
    [Fact]
    public void Evaluate_Uses_Context_Deadline_When_Message_Has_None()
    {
        var expiration = MessageExpiration<Order>.Create().Clock(() => Now).Build();
        var context = new MessageContext(MessageHeaders.Empty.With("expires-at", Now.AddMinutes(-1)));

        var result = expiration.Evaluate(Message<Order>.Create(new Order("o-1")), context);

        ScenarioExpect.True(result.Expired);
        ScenarioExpect.Equal(Now.AddMinutes(-1), result.ExpiresAt);
    }

    [Scenario("Builder Rejects Invalid Configuration")]
    [Fact]
    public void Builder_Rejects_Invalid_Configuration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => MessageExpiration<Order>.Create().Name(""));
        ScenarioExpect.Throws<ArgumentException>(() => MessageExpiration<Order>.Create().Header(""));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => MessageExpiration<Order>.Create().DefaultTtl(TimeSpan.Zero));
        ScenarioExpect.Throws<ArgumentNullException>(() => MessageExpiration<Order>.Create().Clock(null!));
        ScenarioExpect.Throws<ArgumentException>(() => MessageExpiration<Order>.Create().ExpiredReason(""));
        ScenarioExpect.Throws<InvalidOperationException>(() => MessageExpiration<Order>.Create().Build().Stamp(Message<Order>.Create(new Order("o-1"))));
    }

    [Scenario("Methods Reject Null Messages")]
    [Fact]
    public void Methods_Reject_Null_Messages()
    {
        var expiration = MessageExpiration<Order>.Create()
            .DefaultTtl(TimeSpan.FromMinutes(1))
            .Build();

        ScenarioExpect.Throws<ArgumentNullException>(() => expiration.Read(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => expiration.Stamp(null!));
        ScenarioExpect.Throws<ArgumentNullException>(() => expiration.WithDeadline(null!, Now));
        ScenarioExpect.Throws<ArgumentNullException>(() => expiration.Evaluate(null!));
    }

    private sealed record Order(string Id);
}
