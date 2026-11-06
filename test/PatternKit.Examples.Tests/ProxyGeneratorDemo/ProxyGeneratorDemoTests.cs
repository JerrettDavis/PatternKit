using PatternKit.Examples.ProxyGeneratorDemo;
using PatternKit.Examples.ProxyGeneratorDemo.Interceptors;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ProxyGeneratorDemo;

[Feature("Proxy generator demo")]
[Collection(PatternKit.Examples.Tests.ConsoleTestCollection.Name)]
public sealed class ProxyGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("RealPaymentService StoresSuccessfulTransactions")]
    [Fact]
    public void RealPaymentService_StoresSuccessfulTransactions()
    {
        var service = new RealPaymentService();

        var result = service.ProcessPayment(CreateRequest("cust-1", 42m));
        var history = service.GetTransactionHistory("cust-1");

        ScenarioExpect.True(result.Success);
        ScenarioExpect.StartsWith("TXN-", result.TransactionId);
        var transaction = ScenarioExpect.Single(history);
        ScenarioExpect.Equal("cust-1", transaction.CustomerId);
        ScenarioExpect.Equal(42m, transaction.Amount);
        ScenarioExpect.Equal("USD", transaction.Currency);
    }

    [Scenario("RealPaymentService AsyncPayment StoresTransaction")]
    [Fact]
    public async Task RealPaymentService_AsyncPayment_StoresTransaction()
    {
        var service = new RealPaymentService();

        var result = await service.ProcessPaymentAsync(CreateRequest("cust-async", 75m));

        ScenarioExpect.True(result.Success);
        ScenarioExpect.Contains("async", result.Message, StringComparison.OrdinalIgnoreCase);
        ScenarioExpect.Single(service.GetTransactionHistory("cust-async"));
    }

    [Scenario("GeneratedProxy WithInterceptors InvokesInnerService")]
    [Fact]
    public void GeneratedProxy_WithInterceptors_InvokesInnerService()
    {
        var timing = new TimingInterceptor();
        var proxy = new PaymentServiceProxy(
            new RealPaymentService(),
            [timing, new LoggingInterceptor("Test")]);

        var result = proxy.ProcessPayment(CreateRequest("cust-proxy", 100m));
        var history = proxy.GetTransactionHistory("cust-proxy");

        ScenarioExpect.True(result.Success);
        ScenarioExpect.Single(history);
        ScenarioExpect.Contains(nameof(IPaymentService.ProcessPayment), timing.GetTimings().Keys);
        ScenarioExpect.Contains(nameof(IPaymentService.GetTransactionHistory), timing.GetTimings().Keys);
    }

    [Scenario("GeneratedProxy WithAuthenticationInterceptor RejectsInvalidToken")]
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

        ScenarioExpect.Throws<UnauthorizedAccessException>(() => proxy.ProcessPayment(request));
    }

    [Scenario("GeneratedProxy AsyncInterceptors InvokeInnerService")]
    [Fact]
    public async Task GeneratedProxy_AsyncInterceptors_InvokeInnerService()
    {
        var timing = new TimingInterceptor();
        var proxy = new PaymentServiceProxy(
            new RealPaymentService(),
            [new AuthenticationInterceptor(), timing]);

        var result = await proxy.ProcessPaymentAsync(CreateRequest("cust-proxy-async", 55m));

        ScenarioExpect.True(result.Success);
        ScenarioExpect.Contains(nameof(IPaymentService.ProcessPaymentAsync), timing.GetTimings().Keys);
    }

    [Scenario("CachingInterceptor RecordsTransactionHistoryResults")]
    [Fact]
    public void CachingInterceptor_RecordsTransactionHistoryResults()
    {
        var cache = new CachingInterceptor();
        var proxy = new PaymentServiceProxy(new RealPaymentService(), [cache]);

        proxy.ProcessPayment(CreateRequest("cust-cache", 10m));
        _ = proxy.GetTransactionHistory("cust-cache");

        var stats = cache.GetStats();
        ScenarioExpect.Equal(1, stats.Count);
        ScenarioExpect.Equal(0, stats.Expired);

        cache.ClearCache();
        ScenarioExpect.Equal(0, cache.GetStats().Count);
    }

    [Scenario("Comprehensive proxy demo runs the generated proxy pipeline")]
    [Fact]
    public async Task ProxyGeneratorDemo_Run_ExercisesAllDemoScenarios()
    {
        await Given("a redirected console", CaptureConsole)
            .When("running the complete proxy demo", string (capture) =>
            {
                try
                {
                    PatternKit.Examples.ProxyGeneratorDemo.ProxyGeneratorDemo.Run();
                    return capture.Output();
                }
                finally
                {
                    capture.Dispose();
                }
            })
            .Then("the basic proxy scenario completed", output => output.Contains("Basic Proxy", StringComparison.Ordinal))
            .And("the interceptor pipeline scenario completed", output => output.Contains("Pipeline Mode", StringComparison.Ordinal))
            .And("the invalid authentication scenario was caught", output => output.Contains("Invalid authentication token", StringComparison.Ordinal))
            .And("the caching scenario reported cache statistics", output => output.Contains("Cache stats:", StringComparison.Ordinal))
            .AssertPassed();
    }

    [Scenario("Proxy interceptors cover happy and sad paths")]
    [Fact]
    public async Task ProxyInterceptors_RecordSuccessAndFailurePaths()
    {
        await Given("authenticated and cache-enabled proxies", () =>
            {
                var timing = new TimingInterceptor();
                var cache = new CachingInterceptor();
                var authProxy = new PaymentServiceProxy(
                    new RealPaymentService(),
                    [new AuthenticationInterceptor(), new RetryInterceptor(maxRetries: 2), new LoggingInterceptor("Spec"), timing]);
                var cacheProxy = new PaymentServiceProxy(
                    new RealPaymentService(),
                    [cache, timing, new LoggingInterceptor("History")]);

                return new ProxyHarness(authProxy, cacheProxy, timing, cache);
            })
            .When("processing valid and invalid requests", ProxyResult (harness) =>
            {
                var accepted = harness.AuthenticatedProxy.ProcessPayment(CreateRequest("cust-bdd", 125m));
                harness.HistoryProxy.ProcessPayment(CreateRequest("cust-bdd", 75m));
                var history = harness.HistoryProxy.GetTransactionHistory("cust-bdd");
                UnauthorizedAccessException? rejected = null;

                try
                {
                    harness.AuthenticatedProxy.ProcessPayment(new PaymentRequest
                    {
                        CustomerId = "cust-bdd",
                        Amount = 10m,
                        Currency = "USD",
                        Description = "missing token",
                        AuthToken = null
                    });
                }
                catch (UnauthorizedAccessException ex)
                {
                    rejected = ex;
                }

                return new ProxyResult(harness, accepted, history, rejected);
            })
            .Then("the valid request reached the real service", result => result.Accepted.Success && result.History.Count == 1)
            .And("the cache captured the history result", result => result.Harness.Cache.GetStats().Count == 1)
            .And("timing recorded successful methods", result =>
                result.Harness.Timing.GetTimings().ContainsKey(nameof(IPaymentService.ProcessPayment))
                && result.Harness.Timing.GetTimings().ContainsKey(nameof(IPaymentService.GetTransactionHistory)))
            .And("the missing token request was rejected", result =>
                result.Rejected is not null
                && result.Rejected.Message.Contains("required", StringComparison.OrdinalIgnoreCase))
            .AssertPassed();
    }

    [Scenario("Proxy interceptors observe retriable sync and async inner failures")]
    [Fact]
    public async Task ProxyInterceptors_RecordRetriableInnerFailures()
    {
        await Given("throwing sync and async payment proxies", () =>
            {
                var syncTiming = new TimingInterceptor();
                var asyncTiming = new TimingInterceptor();
                var syncProxy = new PaymentServiceProxy(
                    new ThrowingPaymentService(syncException: new IOException("gateway down")),
                    [new RetryInterceptor(maxRetries: 4), syncTiming, new LoggingInterceptor("SyncFailure")]);
                var asyncProxy = new PaymentServiceProxy(
                    new ThrowingPaymentService(asyncException: new IOException("async gateway down")),
                    [new CachingInterceptor(), new RetryInterceptor(maxRetries: 5), asyncTiming, new LoggingInterceptor("AsyncFailure")]);

                return (syncProxy, asyncProxy, syncTiming, asyncTiming);
            })
            .When("processing payments that fail inside the service", async Task<(IOException? syncFailure, IOException? asyncFailure, TimingInterceptor syncTiming, TimingInterceptor asyncTiming)> (proxies) =>
            {
                IOException? syncFailure = null;
                IOException? asyncFailure = null;

                try
                {
                    proxies.syncProxy.ProcessPayment(CreateRequest("sync-fail", 10m));
                }
                catch (IOException ex)
                {
                    syncFailure = ex;
                }

                try
                {
                    await proxies.asyncProxy.ProcessPaymentAsync(CreateRequest("async-fail", 10m));
                }
                catch (IOException ex)
                {
                    asyncFailure = ex;
                }

                return (syncFailure, asyncFailure, proxies.syncTiming, proxies.asyncTiming);
            })
            .Then("the sync retriable failure propagates after interceptors observe it", result => result.syncFailure is not null)
            .And("the async retriable failure propagates after interceptors observe it", result => result.asyncFailure is not null)
            .And("timing removes failed operations from active timers", result =>
                result.syncTiming.GetTimings().Count == 0
                && result.asyncTiming.GetTimings().Count == 0)
            .AssertPassed();
    }

    [Scenario("Proxy interceptors expose async no-op, invalid auth, and non-retriable exception branches")]
    [Fact]
    public async Task ProxyInterceptors_CoverAsyncNoOpAndNonRetriableBranches()
    {
        await Given("standalone interceptors and method contexts", () => new
        {
            Retry = new RetryInterceptor(maxRetries: 1),
            Cache = new CachingInterceptor(),
            Auth = new AuthenticationInterceptor(),
            Context = new GetTransactionHistoryMethodContext("cust-standalone"),
            MissingToken = new ProcessPaymentAsyncMethodContext(
                    new PaymentRequest
                    {
                        CustomerId = "cust-standalone",
                        Amount = 1m,
                        Currency = "USD",
                        Description = "missing token",
                        AuthToken = null
                    },
                    CancellationToken.None)
        })
            .When("invoking direct async interceptor paths", async Task<UnauthorizedAccessException?> (harness) =>
            {
                await harness.Retry.BeforeAsync(harness.Context);
                await harness.Retry.AfterAsync(harness.Context);
                await harness.Retry.OnExceptionAsync(harness.Context, new ArgumentException("bad input"));
                await harness.Cache.BeforeAsync(harness.Context);
                await harness.Cache.AfterAsync(harness.Context);
                await harness.Cache.OnExceptionAsync(harness.Context, new IOException("ignored"));

                try
                {
                    await harness.Auth.BeforeAsync(harness.MissingToken);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return ex;
                }

                return null;
            })
            .Then("authentication rejects missing tokens on async contexts", ex =>
                ex is not null && ex.Message.Contains("required", StringComparison.OrdinalIgnoreCase))
            .AssertPassed();
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

    private static ConsoleCapture CaptureConsole() => new();

    private sealed class ConsoleCapture : IDisposable
    {
        private readonly TextWriter _original = Console.Out;
        private readonly StringWriter _writer = new();

        public ConsoleCapture()
        {
            Console.SetOut(_writer);
        }

        public string Output() => _writer.ToString();

        public void Dispose()
        {
            Console.SetOut(_original);
            _writer.Dispose();
        }
    }

    private sealed record ProxyHarness(
        PaymentServiceProxy AuthenticatedProxy,
        PaymentServiceProxy HistoryProxy,
        TimingInterceptor Timing,
        CachingInterceptor Cache);

    private sealed record ProxyResult(
        ProxyHarness Harness,
        PaymentResult Accepted,
        IReadOnlyList<Transaction> History,
        UnauthorizedAccessException? Rejected);

    private sealed class ThrowingPaymentService(
        Exception? syncException = null,
        Exception? asyncException = null) : IPaymentService
    {
        public PaymentResult ProcessPayment(PaymentRequest request)
            => throw syncException ?? new IOException("sync failure");

        public Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
            => Task.FromException<PaymentResult>(asyncException ?? new IOException("async failure"));

        public IReadOnlyList<Transaction> GetTransactionHistory(string customerId)
            => throw syncException ?? new IOException("history failure");
    }
}
