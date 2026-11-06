using PatternKit.Examples.AdapterGeneratorDemo;
using TinyBDD;

namespace PatternKit.Examples.Tests.AdapterGeneratorDemo;

public class AdapterGeneratorDemoTests
{
    // =========================================================================
    // Clock Adapter Tests
    // =========================================================================

    [Scenario("ClockAdapter ImplementsIClock")]
    [Fact]
    public void ClockAdapter_ImplementsIClock()
    {
        // Arrange
        var legacyClock = new LegacySystemClock();

        // Act
        IClock clock = new SystemClockAdapter(legacyClock);

        // Then - the adapter implements the interface
        ScenarioExpect.NotNull(clock);
    }

    [Scenario("ClockAdapter UtcNow DelegatesToLegacyClock")]
    [Fact]
    public void ClockAdapter_UtcNow_DelegatesToLegacyClock()
    {
        // Arrange
        var legacyClock = new LegacySystemClock();
        IClock clock = new SystemClockAdapter(legacyClock);

        // Act
        var before = DateTimeOffset.UtcNow;
        var result = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        // Then
        ScenarioExpect.InRange(result, before, after);
    }

    [Scenario("ClockAdapter LocalNow DelegatesToLegacyClock")]
    [Fact]
    public void ClockAdapter_LocalNow_DelegatesToLegacyClock()
    {
        // Arrange
        var legacyClock = new LegacySystemClock();
        IClock clock = new SystemClockAdapter(legacyClock);

        // Act
        var before = DateTimeOffset.Now;
        var result = clock.LocalNow;
        var after = DateTimeOffset.Now;

        // Then
        ScenarioExpect.InRange(result, before, after);
    }

    [Scenario("ClockAdapter UnixTimestamp ReturnsValidTimestamp")]
    [Fact]
    public void ClockAdapter_UnixTimestamp_ReturnsValidTimestamp()
    {
        // Arrange
        var legacyClock = new LegacySystemClock();
        IClock clock = new SystemClockAdapter(legacyClock);

        // Act
        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = clock.UnixTimestamp;
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Then
        ScenarioExpect.InRange(result, before, after);
    }

    [Scenario("ClockAdapter DelayAsync Delays")]
    [Fact]
    public async Task ClockAdapter_DelayAsync_Delays()
    {
        // Arrange
        var legacyClock = new LegacySystemClock();
        IClock clock = new SystemClockAdapter(legacyClock);

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await clock.DelayAsync(TimeSpan.FromMilliseconds(100));
        sw.Stop();

        // Then - should have delayed at least 50ms (allowing for scheduler jitter on loaded CI)
        ScenarioExpect.True(sw.ElapsedMilliseconds >= 50);
    }

    [Scenario("ClockAdapter ThrowsOnNullAdaptee")]
    [Fact]
    public void ClockAdapter_ThrowsOnNullAdaptee()
    {
        // Act and verify
        ScenarioExpect.Throws<ArgumentNullException>(() => new SystemClockAdapter(null!));
    }

    // =========================================================================
    // Payment Adapter Tests
    // =========================================================================

    [Scenario("StripeAdapter ImplementsIPaymentGateway")]
    [Fact]
    public void StripeAdapter_ImplementsIPaymentGateway()
    {
        // Arrange
        var stripeClient = new StripePaymentClient();

        // Act
        IPaymentGateway gateway = new StripePaymentAdapter(stripeClient);

        // Then
        ScenarioExpect.NotNull(gateway);
    }

    [Scenario("StripeAdapter GatewayName ReturnsStripe")]
    [Fact]
    public void StripeAdapter_GatewayName_ReturnsStripe()
    {
        // Arrange
        var stripeClient = new StripePaymentClient();
        IPaymentGateway gateway = new StripePaymentAdapter(stripeClient);

        // Act
        var name = gateway.GatewayName;

        // Then
        ScenarioExpect.Equal("Stripe", name);
    }

    [Scenario("StripeAdapter ChargeAsync ReturnsSuccessfulResult")]
    [Fact]
    public async Task StripeAdapter_ChargeAsync_ReturnsSuccessfulResult()
    {
        // Arrange
        var stripeClient = new StripePaymentClient();
        IPaymentGateway gateway = new StripePaymentAdapter(stripeClient);

        // Act
        var result = await gateway.ChargeAsync("tok_visa", 99.99m, "USD");

        // Then
        ScenarioExpect.True(result.Success);
        ScenarioExpect.StartsWith("ch_", result.TransactionId);
        ScenarioExpect.Null(result.ErrorMessage);
    }

    [Scenario("StripeAdapter RefundAsync ReturnsSuccessfulResult")]
    [Fact]
    public async Task StripeAdapter_RefundAsync_ReturnsSuccessfulResult()
    {
        // Arrange
        var stripeClient = new StripePaymentClient();
        IPaymentGateway gateway = new StripePaymentAdapter(stripeClient);

        // Act
        var result = await gateway.RefundAsync("ch_123", 50.00m);

        // Then
        ScenarioExpect.True(result.Success);
        ScenarioExpect.StartsWith("re_", result.RefundId);
        ScenarioExpect.Null(result.ErrorMessage);
    }

    [Scenario("PayPalAdapter ImplementsIPaymentGateway")]
    [Fact]
    public void PayPalAdapter_ImplementsIPaymentGateway()
    {
        // Arrange
        var paypalService = new PayPalPaymentService();

        // Act
        IPaymentGateway gateway = new PayPalPaymentAdapter(paypalService);

        // Then
        ScenarioExpect.NotNull(gateway);
    }

    [Scenario("PayPalAdapter GatewayName ReturnsPayPal")]
    [Fact]
    public void PayPalAdapter_GatewayName_ReturnsPayPal()
    {
        // Arrange
        var paypalService = new PayPalPaymentService();
        IPaymentGateway gateway = new PayPalPaymentAdapter(paypalService);

        // Act
        var name = gateway.GatewayName;

        // Then
        ScenarioExpect.Equal("PayPal", name);
    }

    [Scenario("PayPalAdapter ChargeAsync ReturnsSuccessfulResult")]
    [Fact]
    public async Task PayPalAdapter_ChargeAsync_ReturnsSuccessfulResult()
    {
        // Arrange
        var paypalService = new PayPalPaymentService();
        IPaymentGateway gateway = new PayPalPaymentAdapter(paypalService);

        // Act
        var result = await gateway.ChargeAsync("token_123", 149.99m, "USD");

        // Then
        ScenarioExpect.True(result.Success);
        ScenarioExpect.StartsWith("PAY-", result.TransactionId);
        ScenarioExpect.Null(result.ErrorMessage);
    }

    [Scenario("PayPalAdapter RefundAsync ReturnsSuccessfulResult")]
    [Fact]
    public async Task PayPalAdapter_RefundAsync_ReturnsSuccessfulResult()
    {
        // Arrange
        var paypalService = new PayPalPaymentService();
        IPaymentGateway gateway = new PayPalPaymentAdapter(paypalService);

        // Act
        var result = await gateway.RefundAsync("PAY-123", 75.00m);

        // Then
        ScenarioExpect.True(result.Success);
        ScenarioExpect.StartsWith("REF-", result.RefundId);
        ScenarioExpect.Null(result.ErrorMessage);
    }

    [Scenario("MultiplePaymentAdapters AreInterchangeable")]
    [Fact]
    public async Task MultiplePaymentAdapters_AreInterchangeable()
    {
        // Arrange - different implementations behind the same interface
        IPaymentGateway stripeGateway = new StripePaymentAdapter(new StripePaymentClient());
        IPaymentGateway paypalGateway = new PayPalPaymentAdapter(new PayPalPaymentService());

        // Act - use them interchangeably
        var stripeResult = await stripeGateway.ChargeAsync("tok_1", 100m, "USD");
        var paypalResult = await paypalGateway.ChargeAsync("tok_2", 100m, "USD");

        // Then - both work through the unified interface
        ScenarioExpect.True(stripeResult.Success);
        ScenarioExpect.True(paypalResult.Success);
        ScenarioExpect.NotEqual(stripeResult.TransactionId, paypalResult.TransactionId);
    }

    // =========================================================================
    // Logger Adapter Tests
    // =========================================================================

    [Scenario("LoggerAdapter ImplementsIStructuredLogger")]
    [Fact]
    public void LoggerAdapter_ImplementsIStructuredLogger()
    {
        // Arrange
        var legacyLogger = new LegacyConsoleLogger("TEST");

        // Act
        IStructuredLogger logger = new ConsoleLoggerAdapter(legacyLogger);

        // Then
        ScenarioExpect.NotNull(logger);
    }

    [Scenario("LoggerAdapter IsEnabled RespectsMinimumLevel")]
    [Fact]
    public void LoggerAdapter_IsEnabled_RespectsMinimumLevel()
    {
        // Arrange - logger with Warning minimum level
        var legacyLogger = new LegacyConsoleLogger("TEST", minimumLevel: 2);
        IStructuredLogger logger = new ConsoleLoggerAdapter(legacyLogger);

        // Act and verify
        ScenarioExpect.False(logger.IsEnabled(LogLevel.Debug));
        ScenarioExpect.False(logger.IsEnabled(LogLevel.Info));
        ScenarioExpect.True(logger.IsEnabled(LogLevel.Warning));
        ScenarioExpect.True(logger.IsEnabled(LogLevel.Error));
    }

    [Scenario("LoggerAdapter IsEnabled AllLevelsWhenMinimumIsDebug")]
    [Fact]
    public void LoggerAdapter_IsEnabled_AllLevelsWhenMinimumIsDebug()
    {
        // Arrange - logger with Debug minimum level (default)
        var legacyLogger = new LegacyConsoleLogger("TEST", minimumLevel: 0);
        IStructuredLogger logger = new ConsoleLoggerAdapter(legacyLogger);

        // Act and verify
        ScenarioExpect.True(logger.IsEnabled(LogLevel.Debug));
        ScenarioExpect.True(logger.IsEnabled(LogLevel.Info));
        ScenarioExpect.True(logger.IsEnabled(LogLevel.Warning));
        ScenarioExpect.True(logger.IsEnabled(LogLevel.Error));
    }

    [Scenario("LoggerAdapter LogMethods DoNotThrow")]
    [Fact]
    public void LoggerAdapter_LogMethods_DoNotThrow()
    {
        // Arrange
        var legacyLogger = new LegacyConsoleLogger("TEST");
        IStructuredLogger logger = new ConsoleLoggerAdapter(legacyLogger);

        // Act and verify - none should throw
        var ex = Record.Exception(() =>
        {
            logger.LogDebug("Debug message");
            logger.LogInfo("Info message");
            logger.LogWarning("Warning message");
            logger.LogError("Error message");
            logger.LogError("Error with exception", new InvalidOperationException("Test"));
        });

        ScenarioExpect.Null(ex);
    }

    [Scenario("LoggerAdapter ThrowsOnNullAdaptee")]
    [Fact]
    public void LoggerAdapter_ThrowsOnNullAdaptee()
    {
        // Act and verify
        ScenarioExpect.Throws<ArgumentNullException>(() => new ConsoleLoggerAdapter(null!));
    }
}
