using PatternKit.Examples.AdapterGeneratorDemo;

namespace PatternKit.Examples.Tests.AdapterGeneratorDemo;

public class AdapterGeneratorDemoTests
{
    // =========================================================================
    // Clock Adapter Tests
    // =========================================================================

    [Fact]
    public void ClockAdapter_ImplementsIClock()
    {
        // Arrange
        var legacyClock = new LegacySystemClock();

        // Act
        IClock clock = new SystemClockAdapter(legacyClock);

        // Assert - the adapter implements the interface
        Assert.NotNull(clock);
    }

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

        // Assert
        Assert.InRange(result, before, after);
    }

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

        // Assert
        Assert.InRange(result, before, after);
    }

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

        // Assert
        Assert.InRange(result, before, after);
    }

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

        // Assert - should have delayed at least 50ms (allowing for scheduler jitter on loaded CI)
        Assert.True(sw.ElapsedMilliseconds >= 50);
    }

    [Fact]
    public void ClockAdapter_ThrowsOnNullAdaptee()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SystemClockAdapter(null!));
    }

    // =========================================================================
    // Payment Adapter Tests
    // =========================================================================

    [Fact]
    public void StripeAdapter_ImplementsIPaymentGateway()
    {
        // Arrange
        var stripeClient = new StripePaymentClient();

        // Act
        IPaymentGateway gateway = new StripePaymentAdapter(stripeClient);

        // Assert
        Assert.NotNull(gateway);
    }

    [Fact]
    public void StripeAdapter_GatewayName_ReturnsStripe()
    {
        // Arrange
        var stripeClient = new StripePaymentClient();
        IPaymentGateway gateway = new StripePaymentAdapter(stripeClient);

        // Act
        var name = gateway.GatewayName;

        // Assert
        Assert.Equal("Stripe", name);
    }

    [Fact]
    public async Task StripeAdapter_ChargeAsync_ReturnsSuccessfulResult()
    {
        // Arrange
        var stripeClient = new StripePaymentClient();
        IPaymentGateway gateway = new StripePaymentAdapter(stripeClient);

        // Act
        var result = await gateway.ChargeAsync("tok_visa", 99.99m, "USD");

        // Assert
        Assert.True(result.Success);
        Assert.StartsWith("ch_", result.TransactionId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task StripeAdapter_RefundAsync_ReturnsSuccessfulResult()
    {
        // Arrange
        var stripeClient = new StripePaymentClient();
        IPaymentGateway gateway = new StripePaymentAdapter(stripeClient);

        // Act
        var result = await gateway.RefundAsync("ch_123", 50.00m);

        // Assert
        Assert.True(result.Success);
        Assert.StartsWith("re_", result.RefundId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void PayPalAdapter_ImplementsIPaymentGateway()
    {
        // Arrange
        var paypalService = new PayPalPaymentService();

        // Act
        IPaymentGateway gateway = new PayPalPaymentAdapter(paypalService);

        // Assert
        Assert.NotNull(gateway);
    }

    [Fact]
    public void PayPalAdapter_GatewayName_ReturnsPayPal()
    {
        // Arrange
        var paypalService = new PayPalPaymentService();
        IPaymentGateway gateway = new PayPalPaymentAdapter(paypalService);

        // Act
        var name = gateway.GatewayName;

        // Assert
        Assert.Equal("PayPal", name);
    }

    [Fact]
    public async Task PayPalAdapter_ChargeAsync_ReturnsSuccessfulResult()
    {
        // Arrange
        var paypalService = new PayPalPaymentService();
        IPaymentGateway gateway = new PayPalPaymentAdapter(paypalService);

        // Act
        var result = await gateway.ChargeAsync("token_123", 149.99m, "USD");

        // Assert
        Assert.True(result.Success);
        Assert.StartsWith("PAY-", result.TransactionId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task PayPalAdapter_RefundAsync_ReturnsSuccessfulResult()
    {
        // Arrange
        var paypalService = new PayPalPaymentService();
        IPaymentGateway gateway = new PayPalPaymentAdapter(paypalService);

        // Act
        var result = await gateway.RefundAsync("PAY-123", 75.00m);

        // Assert
        Assert.True(result.Success);
        Assert.StartsWith("REF-", result.RefundId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task MultiplePaymentAdapters_AreInterchangeable()
    {
        // Arrange - different implementations behind the same interface
        IPaymentGateway stripeGateway = new StripePaymentAdapter(new StripePaymentClient());
        IPaymentGateway paypalGateway = new PayPalPaymentAdapter(new PayPalPaymentService());

        // Act - use them interchangeably
        var stripeResult = await stripeGateway.ChargeAsync("tok_1", 100m, "USD");
        var paypalResult = await paypalGateway.ChargeAsync("tok_2", 100m, "USD");

        // Assert - both work through the unified interface
        Assert.True(stripeResult.Success);
        Assert.True(paypalResult.Success);
        Assert.NotEqual(stripeResult.TransactionId, paypalResult.TransactionId);
    }

    // =========================================================================
    // Logger Adapter Tests
    // =========================================================================

    [Fact]
    public void LoggerAdapter_ImplementsIStructuredLogger()
    {
        // Arrange
        var legacyLogger = new LegacyConsoleLogger("TEST");

        // Act
        IStructuredLogger logger = new ConsoleLoggerAdapter(legacyLogger);

        // Assert
        Assert.NotNull(logger);
    }

    [Fact]
    public void LoggerAdapter_IsEnabled_RespectsMinimumLevel()
    {
        // Arrange - logger with Warning minimum level
        var legacyLogger = new LegacyConsoleLogger("TEST", minimumLevel: 2);
        IStructuredLogger logger = new ConsoleLoggerAdapter(legacyLogger);

        // Act & Assert
        Assert.False(logger.IsEnabled(LogLevel.Debug));
        Assert.False(logger.IsEnabled(LogLevel.Info));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void LoggerAdapter_IsEnabled_AllLevelsWhenMinimumIsDebug()
    {
        // Arrange - logger with Debug minimum level (default)
        var legacyLogger = new LegacyConsoleLogger("TEST", minimumLevel: 0);
        IStructuredLogger logger = new ConsoleLoggerAdapter(legacyLogger);

        // Act & Assert
        Assert.True(logger.IsEnabled(LogLevel.Debug));
        Assert.True(logger.IsEnabled(LogLevel.Info));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
    }

    [Fact]
    public void LoggerAdapter_LogMethods_DoNotThrow()
    {
        // Arrange
        var legacyLogger = new LegacyConsoleLogger("TEST");
        IStructuredLogger logger = new ConsoleLoggerAdapter(legacyLogger);

        // Act & Assert - none should throw
        var ex = Record.Exception(() =>
        {
            logger.LogDebug("Debug message");
            logger.LogInfo("Info message");
            logger.LogWarning("Warning message");
            logger.LogError("Error message");
            logger.LogError("Error with exception", new InvalidOperationException("Test"));
        });

        Assert.Null(ex);
    }

    [Fact]
    public void LoggerAdapter_ThrowsOnNullAdaptee()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConsoleLoggerAdapter(null!));
    }
}
