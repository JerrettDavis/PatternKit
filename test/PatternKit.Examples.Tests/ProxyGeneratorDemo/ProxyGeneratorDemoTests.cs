using PatternKit.Examples.ProxyGeneratorDemo;
using PatternKit.Examples.ProxyGeneratorDemo.Interceptors;

namespace PatternKit.Examples.Tests.ProxyGeneratorDemo;

public sealed class ProxyGeneratorDemoTests
{
    [Fact]
    public void RealPaymentService_StoresSuccessfulTransactions()
    {
        var service = new RealPaymentService();

        var result = service.ProcessPayment(CreateRequest("cust-1", 42m));
        var history = service.GetTransactionHistory("cust-1");

        Assert.True(result.Success);
        Assert.StartsWith("TXN-", result.TransactionId);
        var transaction = Assert.Single(history);
        Assert.Equal("cust-1", transaction.CustomerId);
        Assert.Equal(42m, transaction.Amount);
        Assert.Equal("USD", transaction.Currency);
    }

    [Fact]
    public async Task RealPaymentService_AsyncPayment_StoresTransaction()
    {
        var service = new RealPaymentService();

        var result = await service.ProcessPaymentAsync(CreateRequest("cust-async", 75m));

        Assert.True(result.Success);
        Assert.Contains("async", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(service.GetTransactionHistory("cust-async"));
    }

    [Fact]
    public void GeneratedProxy_WithInterceptors_InvokesInnerService()
    {
        var timing = new TimingInterceptor();
        var proxy = new PaymentServiceProxy(
            new RealPaymentService(),
            [timing, new LoggingInterceptor("Test")]);

        var result = proxy.ProcessPayment(CreateRequest("cust-proxy", 100m));
        var history = proxy.GetTransactionHistory("cust-proxy");

        Assert.True(result.Success);
        Assert.Single(history);
        Assert.Contains(nameof(IPaymentService.ProcessPayment), timing.GetTimings().Keys);
        Assert.Contains(nameof(IPaymentService.GetTransactionHistory), timing.GetTimings().Keys);
    }

    [Fact]
    public void GeneratedProxy_WithAuthenticationInterceptor_RejectsInvalidToken()
    {
        var proxy = new PaymentServiceProxy(
            new RealPaymentService(),
            [new AuthenticationInterceptor(), new RetryInterceptor()]);

        var request = new PaymentRequest
        {
            CustomerId = "cust-invalid",
            Amount = 100m,
            Currency = "USD",
            Description = "invalid token",
            AuthToken = "bad-token"
        };

        Assert.Throws<UnauthorizedAccessException>(() => proxy.ProcessPayment(request));
    }

    [Fact]
    public async Task GeneratedProxy_AsyncInterceptors_InvokeInnerService()
    {
        var timing = new TimingInterceptor();
        var proxy = new PaymentServiceProxy(
            new RealPaymentService(),
            [new AuthenticationInterceptor(), timing]);

        var result = await proxy.ProcessPaymentAsync(CreateRequest("cust-proxy-async", 55m));

        Assert.True(result.Success);
        Assert.Contains(nameof(IPaymentService.ProcessPaymentAsync), timing.GetTimings().Keys);
    }

    [Fact]
    public void CachingInterceptor_RecordsTransactionHistoryResults()
    {
        var cache = new CachingInterceptor();
        var proxy = new PaymentServiceProxy(new RealPaymentService(), [cache]);

        proxy.ProcessPayment(CreateRequest("cust-cache", 10m));
        _ = proxy.GetTransactionHistory("cust-cache");

        var stats = cache.GetStats();
        Assert.Equal(1, stats.Count);
        Assert.Equal(0, stats.Expired);

        cache.ClearCache();
        Assert.Equal(0, cache.GetStats().Count);
    }

    private static PaymentRequest CreateRequest(string customerId, decimal amount) =>
        new()
        {
            CustomerId = customerId,
            Amount = amount,
            Currency = "USD",
            Description = "test payment",
            AuthToken = "valid-token-123"
        };
}
