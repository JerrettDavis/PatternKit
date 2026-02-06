using PatternKit.Examples.ProxyGeneratorDemo.Interceptors;

namespace PatternKit.Examples.ProxyGeneratorDemo;

/// <summary>
/// Comprehensive demonstration of the Proxy Pattern generator with various interceptor scenarios.
/// Shows how the generated proxy enables cross-cutting concerns without modifying business logic.
/// </summary>
public static class ProxyGeneratorDemo
{
    /// <summary>
    /// Runs all proxy pattern demonstrations.
    /// </summary>
    public static void Run()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║          Proxy Pattern Generator - Comprehensive Demo       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

        RunBasicProxyDemo();
        RunSingleInterceptorDemo();
        RunPipelineDemo();
        RunInterceptorOrderingDemo();
        RunAsyncSupportDemo();
        RunExceptionHandlingDemo();
        RunCachingDemo();

        Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    Demo Complete!                           ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    }

    /// <summary>
    /// Demonstrates basic proxy usage without interceptors.
    /// </summary>
    private static void RunBasicProxyDemo()
    {
        PrintSection("1. Basic Proxy (No Interceptors)");

        var service = new RealPaymentService();
        // For basic proxy without interceptors, we would use ProxyInterceptorMode.None
        // But since our IPaymentService uses Pipeline mode, we'll use an empty interceptor list
        var proxy = new PaymentServiceProxy(service, Array.Empty<IPaymentServiceInterceptor>());

        var request = new PaymentRequest
        {
            CustomerId = "CUST001",
            Amount = 99.99m,
            Currency = "USD",
            Description = "Basic payment test",
            AuthToken = "valid-token-123"
        };

        var result = proxy.ProcessPayment(request);
        Console.WriteLine($"Result: {result.Success}, Transaction ID: {result.TransactionId}");
        Console.WriteLine("Note: Proxy delegates directly to the real service with no interception.\n");
    }

    /// <summary>
    /// Demonstrates single interceptor usage for logging.
    /// </summary>
    private static void RunSingleInterceptorDemo()
    {
        PrintSection("2. Single Interceptor (Logging)");

        var service = new RealPaymentService();
        var loggingInterceptor = new LoggingInterceptor();
        var proxy = new PaymentServiceProxy(service, new[] { loggingInterceptor });

        var request = new PaymentRequest
        {
            CustomerId = "CUST002",
            Amount = 149.99m,
            Currency = "USD",
            Description = "Single interceptor test",
            AuthToken = "valid-token-123"
        };

        var result = proxy.ProcessPayment(request);
        Console.WriteLine($"Result: {result.Success}, Transaction ID: {result.TransactionId}\n");
    }

    /// <summary>
    /// Demonstrates pipeline with multiple interceptors working together.
    /// </summary>
    private static void RunPipelineDemo()
    {
        PrintSection("3. Pipeline Mode (Multiple Interceptors)");

        var service = new RealPaymentService();
        var interceptors = new IPaymentServiceInterceptor[]
        {
            new LoggingInterceptor("Log"),
            new TimingInterceptor(),
            new AuthenticationInterceptor()
        };
        var proxy = new PaymentServiceProxy(service, interceptors);

        var request = new PaymentRequest
        {
            CustomerId = "CUST003",
            Amount = 249.99m,
            Currency = "EUR",
            Description = "Pipeline test",
            AuthToken = "valid-token-123"
        };

        Console.WriteLine("Executing with pipeline: Logging → Timing → Authentication");
        var result = proxy.ProcessPayment(request);
        Console.WriteLine($"Result: {result.Success}, Transaction ID: {result.TransactionId}\n");
    }

    /// <summary>
    /// Demonstrates how interceptor ordering affects execution.
    /// </summary>
    private static void RunInterceptorOrderingDemo()
    {
        PrintSection("4. Interceptor Ordering");

        var service = new RealPaymentService();

        Console.WriteLine("Order: Auth → Timing → Logging");
        var interceptors1 = new IPaymentServiceInterceptor[]
        {
            new AuthenticationInterceptor(),
            new TimingInterceptor(),
            new LoggingInterceptor("Log")
        };
        var proxy1 = new PaymentServiceProxy(service, interceptors1);

        var request = new PaymentRequest
        {
            CustomerId = "CUST004",
            Amount = 99.99m,
            Currency = "USD",
            Description = "Order test 1",
            AuthToken = "admin-token-456"
        };

        proxy1.ProcessPayment(request);

        Console.WriteLine("\nOrder: Logging → Timing → Auth");
        var interceptors2 = new IPaymentServiceInterceptor[]
        {
            new LoggingInterceptor("Log"),
            new TimingInterceptor(),
            new AuthenticationInterceptor()
        };
        var proxy2 = new PaymentServiceProxy(service, interceptors2);

        proxy2.ProcessPayment(request);
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates async method support with interceptors.
    /// </summary>
    private static void RunAsyncSupportDemo()
    {
        PrintSection("5. Async Support");

        var service = new RealPaymentService();
        var interceptors = new IPaymentServiceInterceptor[]
        {
            new LoggingInterceptor("AsyncLog"),
            new TimingInterceptor()
        };
        var proxy = new PaymentServiceProxy(service, interceptors);

        var request = new PaymentRequest
        {
            CustomerId = "CUST005",
            Amount = 399.99m,
            Currency = "GBP",
            Description = "Async payment test",
            AuthToken = "valid-token-123"
        };

        Console.WriteLine("Executing async method with interceptors...");
        var result = proxy.ProcessPaymentAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        Console.WriteLine($"Result: {result.Success}, Transaction ID: {result.TransactionId}\n");
    }

    /// <summary>
    /// Demonstrates exception handling and authentication failure.
    /// </summary>
    private static void RunExceptionHandlingDemo()
    {
        PrintSection("6. Exception Handling");

        var service = new RealPaymentService();
        var interceptors = new IPaymentServiceInterceptor[]
        {
            new LoggingInterceptor("ErrorLog"),
            new AuthenticationInterceptor()
        };
        var proxy = new PaymentServiceProxy(service, interceptors);

        var invalidRequest = new PaymentRequest
        {
            CustomerId = "CUST006",
            Amount = 99.99m,
            Currency = "USD",
            Description = "Invalid auth test",
            AuthToken = "invalid-token-999" // Invalid token
        };

        Console.WriteLine("Attempting payment with invalid authentication token...");
        try
        {
            proxy.ProcessPayment(invalidRequest);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"✓ Exception caught: {ex.Message}");
        }

        var missingTokenRequest = new PaymentRequest
        {
            CustomerId = "CUST006",
            Amount = 99.99m,
            Currency = "USD",
            Description = "Missing auth test",
            AuthToken = null // No token
        };

        Console.WriteLine("\nAttempting payment without authentication token...");
        try
        {
            proxy.ProcessPayment(missingTokenRequest);
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"✓ Exception caught: {ex.Message}\n");
        }
    }

    /// <summary>
    /// Demonstrates caching interceptor with transaction history lookups.
    /// </summary>
    private static void RunCachingDemo()
    {
        PrintSection("7. Caching Interceptor");

        var service = new RealPaymentService();
        var cachingInterceptor = new CachingInterceptor();
        var timingInterceptor = new TimingInterceptor();
        var interceptors = new IPaymentServiceInterceptor[]
        {
            cachingInterceptor,
            timingInterceptor,
            new LoggingInterceptor("CacheDemo")
        };
        var proxy = new PaymentServiceProxy(service, interceptors);

        // Create some transaction history
        var request1 = new PaymentRequest
        {
            CustomerId = "CUST007",
            Amount = 50.00m,
            Currency = "USD",
            Description = "First transaction",
            AuthToken = "valid-token-123"
        };
        proxy.ProcessPayment(request1);

        var request2 = new PaymentRequest
        {
            CustomerId = "CUST007",
            Amount = 75.00m,
            Currency = "USD",
            Description = "Second transaction",
            AuthToken = "valid-token-123"
        };
        proxy.ProcessPayment(request2);

        Console.WriteLine("\n--- First history lookup (cache miss) ---");
        var history1 = proxy.GetTransactionHistory("CUST007");
        Console.WriteLine($"Retrieved {history1.Count} transactions");

        Console.WriteLine("\n--- Second history lookup (cache hit) ---");
        var history2 = proxy.GetTransactionHistory("CUST007");
        Console.WriteLine($"Retrieved {history2.Count} transactions");

        Console.WriteLine("\n--- Third history lookup (cache hit) ---");
        var history3 = proxy.GetTransactionHistory("CUST007");
        Console.WriteLine($"Retrieved {history3.Count} transactions");

        var stats = cachingInterceptor.GetStats();
        Console.WriteLine($"\nCache stats: {stats.Count} entries, {stats.Expired} expired");
        timingInterceptor.PrintSummary();
        Console.WriteLine("Note: Cache hits avoid the 50ms database lookup delay.\n");
    }

    private static void PrintSection(string title)
    {
        Console.WriteLine($"╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine($"║ {title,-60} ║");
        Console.WriteLine($"╚══════════════════════════════════════════════════════════════╝\n");
    }
}
