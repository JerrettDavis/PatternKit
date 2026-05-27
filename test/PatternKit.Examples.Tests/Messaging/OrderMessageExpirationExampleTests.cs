using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.Messaging;
using PatternKit.Messaging;
using TinyBDD;

namespace PatternKit.Examples.Tests.Messaging;

public sealed class OrderMessageExpirationExampleTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 27, 12, 0, 0, TimeSpan.Zero);

    [Scenario("Fluent Expiration Stamps Fresh Order Command")]
    [Fact]
    public void Fluent_Expiration_Stamps_Fresh_Order_Command()
    {
        var summary = OrderMessageExpirationExampleRunner.RunFluent(new ExpiringOrderCommand("o-1", "c-1"), Now);

        ScenarioExpect.False(summary.Expired);
        ScenarioExpect.Equal(Now.AddMinutes(20), summary.ExpiresAt);
        ScenarioExpect.Null(summary.Reason);
    }

    [Scenario("Generated Expiration Stamps Fresh Order Command")]
    [Fact]
    public void Generated_Expiration_Stamps_Fresh_Order_Command()
    {
        var expiration = GeneratedOrderMessageExpiration.Create();
        var message = expiration.Stamp(Message<ExpiringOrderCommand>.Create(new ExpiringOrderCommand("o-1", "c-1")));

        var result = expiration.Evaluate(message);

        ScenarioExpect.False(result.Expired);
        ScenarioExpect.Equal("x-order-expires-at", expiration.HeaderName);
        ScenarioExpect.NotNull(result.ExpiresAt);
    }

    [Scenario("Generated Expiration Rejects Stale Order Command")]
    [Fact]
    public void Generated_Expiration_Rejects_Stale_Order_Command()
    {
        var expiration = GeneratedOrderMessageExpiration.Create();
        var message = Message<ExpiringOrderCommand>
            .Create(new ExpiringOrderCommand("o-1", "c-1"))
            .WithHeader("x-order-expires-at", DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = expiration.Evaluate(message);

        ScenarioExpect.True(result.Expired);
        ScenarioExpect.Equal("Order command expired before fulfillment accepted it.", result.Reason);
    }

    [Scenario("Expiration Example Registers With ServiceCollection")]
    [Fact]
    public void Expiration_Example_Registers_With_ServiceCollection()
    {
        using var provider = new ServiceCollection()
            .AddOrderMessageExpirationDemo()
            .BuildServiceProvider(validateScopes: true);
        var runner = provider.GetRequiredService<OrderMessageExpirationExampleRunner>();

        var summary = runner.RunGenerated(new ExpiringOrderCommand("o-1", "c-1"));

        ScenarioExpect.False(summary.Expired);
        ScenarioExpect.NotNull(summary.ExpiresAt);
    }
}
