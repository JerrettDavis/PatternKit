using PatternKit.Structural.Proxy;

namespace PatternKit.Examples.ProxyDemo;

/// <summary>
/// Demonstrates various proxy patterns including virtual, protection, caching, logging, and a complete mocking framework.
/// </summary>
public static class ProxyDemo
{
    #region Virtual Proxy - Lazy Initialization

    /// <summary>
    /// Simulates an expensive resource that should be lazily initialized.
    /// </summary>
    /// <remarks>
    /// This class represents a heavyweight object (like a database connection) that
    /// has expensive initialization costs and should only be created when actually needed.
    /// </remarks>
    public sealed class ExpensiveDatabase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExpensiveDatabase"/> class.
        /// </summary>
        /// <param name="connectionString">The database connection string.</param>
        public ExpensiveDatabase(string connectionString)
        {
            Console.WriteLine($"[EXPENSIVE] Initializing database connection: {connectionString}");
            Thread.Sleep(100); // Simulate slow initialization
        }

        /// <summary>
        /// Executes a SQL query against the database.
        /// </summary>
        /// <param name="sql">The SQL query to execute.</param>
        /// <returns>The query result as a string.</returns>
        public string Query(string sql)
        {
            Console.WriteLine($"[DB] Executing: {sql}");
            return $"Result for: {sql}";
        }
    }

    /// <summary>
    /// Demonstrates the virtual proxy pattern for lazy initialization of expensive resources.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The virtual proxy delays creating the expensive database connection until the first
    /// query is executed. Subsequent queries reuse the initialized connection.
    /// </para>
    /// <para>
    /// This is particularly useful for:
    /// <list type="bullet">
    /// <item><description>Database connections</description></item>
    /// <item><description>File handles</description></item>
    /// <item><description>Network connections</description></item>
    /// <item><description>Heavy computational resources</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static void DemonstrateVirtualProxy()
    {
        DemonstrateVirtualProxy(Console.Out);
    }

    public static void DemonstrateVirtualProxy(TextWriter writer)
    {
        writer.WriteLine("\n=== Virtual Proxy - Lazy Initialization ===");

        // The database is NOT created here
        var dbProxy = Proxy<string, string>.Create()
            .VirtualProxy(() =>
            {
                var db = new ExpensiveDatabase("Server=localhost;Database=MyDb");
                return sql => db.Query(sql);
            })
            .Build();

        writer.WriteLine("Proxy created (database not yet initialized)");
        Thread.Sleep(50);

        writer.WriteLine("\nFirst query - database will initialize now:");
        var result1 = dbProxy.Execute("SELECT * FROM Users");
        writer.WriteLine($"Result: {result1}");

        writer.WriteLine("\nSecond query - database already initialized:");
        var result2 = dbProxy.Execute("SELECT * FROM Orders");
        writer.WriteLine($"Result: {result2}");
    }

    #endregion

    #region Protection Proxy - Access Control

    /// <summary>
    /// Represents a user with a name and role for access control demonstrations.
    /// </summary>
    /// <param name="Name">The user's name.</param>
    /// <param name="Role">The user's role (e.g., "Admin", "User").</param>
    public sealed record User(string Name, string Role);

    /// <summary>
    /// Represents a document with title, content, and access level restrictions.
    /// </summary>
    /// <param name="Title">The document title.</param>
    /// <param name="Content">The document content.</param>
    /// <param name="AccessLevel">The required access level (e.g., "Public", "Admin").</param>
    public sealed record Document(string Title, string Content, string AccessLevel);

    /// <summary>
    /// Service for performing operations on documents.
    /// </summary>
    /// <remarks>
    /// This service contains the actual business logic for document operations.
    /// In production, it would be protected by a proxy that enforces access control.
    /// </remarks>
    public sealed class DocumentService
    {
        /// <summary>
        /// Reads a document's content.
        /// </summary>
        /// <param name="doc">The document to read.</param>
        /// <returns>A formatted string containing the document content.</returns>
        public string Read(Document doc) => $"Reading '{doc.Title}': {doc.Content}";

        /// <summary>
        /// Deletes a document from the system.
        /// </summary>
        /// <param name="doc">The document to delete.</param>
        /// <returns><see langword="true"/> if deletion was successful.</returns>
        public bool Delete(Document doc)
        {
            Console.WriteLine($"[SERVICE] Deleted document: {doc.Title}");
            return true;
        }
    }

    /// <summary>
    /// Demonstrates the protection proxy pattern for access control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The protection proxy validates user permissions before allowing access to documents.
    /// This implements role-based access control (RBAC) at the proxy level.
    /// </para>
    /// <para>
    /// Use cases:
    /// <list type="bullet">
    /// <item><description>Role-based access control</description></item>
    /// <item><description>Authentication and authorization</description></item>
    /// <item><description>API rate limiting</description></item>
    /// <item><description>Feature flags and permissions</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static void DemonstrateProtectionProxy()
    {
        DemonstrateProtectionProxy(Console.Out);
    }

    public static void DemonstrateProtectionProxy(TextWriter writer)
    {
        writer.WriteLine("\n=== Protection Proxy - Access Control ===");

        var service = new DocumentService();
        var adminDoc = new Document("Admin Guide", "Confidential content", "Admin");
        var publicDoc = new Document("User Manual", "Public content", "Public");

        var currentUser = new User("Alice", "User");

        // Create a protection proxy that checks access levels
        var protectedRead = Proxy<(User user, Document doc), string>.Create(
                input => service.Read(input.doc))
            .ProtectionProxy(input =>
            {
                var hasAccess = input.doc.AccessLevel == "Public" || input.user.Role == "Admin";
                writer.WriteLine($"Access check: {input.user.Name} ({input.user.Role}) " +
                                  $"accessing {input.doc.AccessLevel} document - {(hasAccess ? "ALLOWED" : "DENIED")}");
                return hasAccess;
            })
            .Build();

        try
        {
            writer.WriteLine("\nAttempting to read public document:");
            var result1 = protectedRead.Execute((currentUser, publicDoc));
            writer.WriteLine($"Success: {result1}");
        }
        catch (UnauthorizedAccessException ex)
        {
            writer.WriteLine($"Failed: {ex.Message}");
        }

        try
        {
            writer.WriteLine("\nAttempting to read admin document:");
            var result2 = protectedRead.Execute((currentUser, adminDoc));
            writer.WriteLine($"Success: {result2}");
        }
        catch (UnauthorizedAccessException ex)
        {
            writer.WriteLine($"Failed: {ex.Message}");
        }

        // Now try as admin
        var adminUser = new User("Bob", "Admin");
        try
        {
            writer.WriteLine($"\nAttempting to read admin document as admin user:");
            var result3 = protectedRead.Execute((adminUser, adminDoc));
            writer.WriteLine($"Success: {result3}");
        }
        catch (UnauthorizedAccessException ex)
        {
            writer.WriteLine($"Failed: {ex.Message}");
        }
    }

    #endregion

    #region Caching Proxy

    /// <summary>
    /// Demonstrates the caching proxy pattern for result memoization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The caching proxy stores results of expensive calculations and returns cached values
    /// for repeated inputs, avoiding redundant computation.
    /// </para>
    /// <para>
    /// Ideal for:
    /// <list type="bullet">
    /// <item><description>Expensive computations (e.g., Fibonacci, cryptography)</description></item>
    /// <item><description>Database queries with stable data</description></item>
    /// <item><description>API calls with rate limits</description></item>
    /// <item><description>Image/video processing</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static void DemonstrateCachingProxy()
    {
        DemonstrateCachingProxy(Console.Out);
    }

    public static void DemonstrateCachingProxy(TextWriter writer)
    {
        writer.WriteLine("\n=== Caching Proxy - Result Memoization ===");

        var callCount = 0;

        Proxy<int, int>.Subject expensiveCalculation = x =>
        {
            callCount++;
            writer.WriteLine($"[EXPENSIVE] Computing fibonacci({x}) - Call #{callCount}");
            Thread.Sleep(50); // Simulate expensive operation
            return Fibonacci(x);
        };

        var cachedProxy = Proxy<int, int>.Create(expensiveCalculation)
            .CachingProxy()
            .Build();

        writer.WriteLine("\nFirst call - fib(10):");
        var r1 = cachedProxy.Execute(10);
        writer.WriteLine($"Result: {r1}\n");

        writer.WriteLine("Second call - fib(10) (should be cached):");
        var r2 = cachedProxy.Execute(10);
        writer.WriteLine($"Result: {r2}\n");

        writer.WriteLine("Third call - fib(15) (new value):");
        var r3 = cachedProxy.Execute(15);
        writer.WriteLine($"Result: {r3}\n");

        writer.WriteLine("Fourth call - fib(10) (still cached):");
        var r4 = cachedProxy.Execute(10);
        writer.WriteLine($"Result: {r4}\n");

        writer.WriteLine($"Total expensive calculations performed: {callCount}");
    }

    private static int Fibonacci(int n)
    {
        if (n <= 1) return n;
        int a = 0, b = 1;
        for (var i = 2; i <= n; i++)
        {
            var temp = a + b;
            a = b;
            b = temp;
        }
        return b;
    }

    #endregion

    #region Logging Proxy

    /// <summary>
    /// Demonstrates the logging proxy pattern for invocation tracking and debugging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The logging proxy transparently logs all method invocations and their results,
    /// useful for debugging, auditing, and monitoring.
    /// </para>
    /// <para>
    /// Common applications:
    /// <list type="bullet">
    /// <item><description>Audit trails for compliance</description></item>
    /// <item><description>Performance monitoring</description></item>
    /// <item><description>Debugging production issues</description></item>
    /// <item><description>Usage analytics</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static void DemonstrateLoggingProxy()
    {
        DemonstrateLoggingProxy(Console.Out);
    }

    public static void DemonstrateLoggingProxy(TextWriter writer)
    {
        writer.WriteLine("\n=== Logging Proxy - Invocation Tracking ===");

        var logMessages = new List<string>();

        var calculatorProxy = Proxy<(int a, int b), int>.Create(
                input => input.a + input.b)
            .LoggingProxy(msg => logMessages.Add(msg))
            .Build();

        writer.WriteLine("Executing: 5 + 3");
        var result = calculatorProxy.Execute((5, 3));
        writer.WriteLine($"Result: {result}\n");

        writer.WriteLine("Log messages:");
        foreach (var msg in logMessages)
            writer.WriteLine($"  {msg}");
    }

    #endregion

    #region Custom Interception

    /// <summary>
    /// Demonstrates custom interception for implementing retry logic with exponential backoff.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This example shows how the proxy pattern can add resilience to unreliable services
    /// by automatically retrying failed operations.
    /// </para>
    /// <para>
    /// Retry logic is essential for:
    /// <list type="bullet">
    /// <item><description>Network calls that may fail transiently</description></item>
    /// <item><description>Distributed systems with eventual consistency</description></item>
    /// <item><description>Cloud services with throttling</description></item>
    /// <item><description>Microservices communication</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static void DemonstrateCustomInterception()
    {
        DemonstrateCustomInterception(Console.Out);
    }

    public static void DemonstrateCustomInterception(TextWriter writer)
    {
        writer.WriteLine("\n=== Custom Interception - Retry Logic ===");

        var attemptCount = 0;

        Proxy<string, string>.Subject unreliableService = request =>
        {
            attemptCount++;
            writer.WriteLine($"  Attempt #{attemptCount}: Processing '{request}'");

            if (attemptCount < 3)
            {
                writer.WriteLine("  Failed!");
                throw new InvalidOperationException("Service temporarily unavailable");
            }

            writer.WriteLine("  Success!");
            return $"Processed: {request}";
        };

        var retryProxy = Proxy<string, string>.Create(unreliableService)
            .Intercept((input, next) =>
            {
                const int maxRetries = 5;
                for (var i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        return next(input);
                    }
                    catch (InvalidOperationException) when (i < maxRetries - 1)
                    {
                        writer.WriteLine($"  Retrying... ({i + 1}/{maxRetries - 1})");
                        Thread.Sleep(10);
                    }
                }
                throw new InvalidOperationException("Max retries exceeded");
            })
            .Build();

        writer.WriteLine("Calling unreliable service with retry proxy:");
        var result = retryProxy.Execute("important-data");
        writer.WriteLine($"\nFinal result: {result}");
    }

    #endregion

    #region Mock Framework Example

    /// <summary>
    /// A simple fluent mocking framework built with the Proxy pattern.
    /// </summary>
    /// <remarks>
    /// This demonstrates how proxy patterns power testing frameworks like Moq and NSubstitute.
    /// The mock intercepts calls, records invocations, and returns configured values.
    /// </remarks>
    public static class MockFramework
    {
        /// <summary>
        /// Creates a mock object for testing.
        /// </summary>
        /// <typeparam name="TIn">The input type for the mocked operation.</typeparam>
        /// <typeparam name="TOut">The output type for the mocked operation.</typeparam>
        /// <remarks>
        /// The mock records all invocations and allows verification of interactions,
        /// similar to popular mocking frameworks.
        /// </remarks>
        public sealed class Mock<TIn, TOut> where TIn : notnull
        {
            private readonly List<(Func<TIn, bool> predicate, TOut result)> _setups = new();
            private readonly List<TIn> _invocations = new();
            private TOut _defaultResult = default!;
            private bool _hasDefault;

            /// <summary>
            /// Configure the mock to return a specific value when the predicate matches.
            /// </summary>
            public Mock<TIn, TOut> Setup(Func<TIn, bool> predicate, TOut result)
            {
                _setups.Add((predicate, result));
                return this;
            }

            /// <summary>
            /// Configure the mock to return a specific value for any input.
            /// </summary>
            public Mock<TIn, TOut> Returns(TOut result)
            {
                _defaultResult = result;
                _hasDefault = true;
                return this;
            }

            /// <summary>
            /// Configure the mock to throw an exception when invoked.
            /// </summary>
            public Mock<TIn, TOut> Throws<TException>() where TException : Exception, new()
            {
                return Setup(_ => true, default!); // This is a simplification
            }

            /// <summary>
            /// Build the mock proxy.
            /// </summary>
            public Proxy<TIn, TOut> Build()
            {
                return Proxy<TIn, TOut>.Create(RealSubject)
                    .Intercept((input, next) =>
                    {
                        _invocations.Add(input);
                        return next(input);
                    })
                    .Build();
            }

            private TOut RealSubject(TIn input)
            {
                foreach (var (predicate, result) in _setups)
                {
                    if (predicate(input))
                        return result;
                }

                if (_hasDefault)
                    return _defaultResult;

                throw new InvalidOperationException($"No setup found for input: {input}");
            }

            /// <summary>
            /// Verify that the mock was called with the specified input.
            /// </summary>
            public void Verify(Func<TIn, bool> predicate, int times = 1)
            {
                var count = _invocations.Count(predicate);
                if (count != times)
                    throw new InvalidOperationException(
                        $"Expected {times} invocation(s) but found {count}");
            }

            /// <summary>
            /// Verify that the mock was called at least once.
            /// </summary>
            public void VerifyAny(Func<TIn, bool> predicate)
            {
                if (!_invocations.Any(predicate))
                    throw new InvalidOperationException("No matching invocations found");
            }

            /// <summary>
            /// Get all recorded invocations.
            /// </summary>
            public IReadOnlyList<TIn> Invocations => _invocations.AsReadOnly();
        }

        /// <summary>
        /// Creates a new mock instance.
        /// </summary>
        /// <typeparam name="TIn">The input type.</typeparam>
        /// <typeparam name="TOut">The output type.</typeparam>
        /// <returns>A new mock builder.</returns>
        public static Mock<TIn, TOut> CreateMock<TIn, TOut>() where TIn : notnull
            => new();
    }

    /// <summary>
    /// Interface for email sending operations.
    /// </summary>
    public interface IEmailService
    {
        /// <summary>
        /// Sends an email message.
        /// </summary>
        /// <param name="to">The recipient email address.</param>
        /// <param name="subject">The email subject.</param>
        /// <param name="body">The email body content.</param>
        /// <returns><see langword="true"/> if the email was sent successfully; otherwise, <see langword="false"/>.</returns>
        bool SendEmail(string to, string subject, string body);
    }

    /// <summary>
    /// Adapter that wraps a proxy to implement the <see cref="IEmailService"/> interface.
    /// </summary>
    /// <remarks>
    /// This demonstrates how proxies can be adapted to standard interfaces,
    /// enabling dependency injection and testability.
    /// </remarks>
    public sealed class EmailServiceAdapter : IEmailService
    {
        private readonly Proxy<(string to, string subject, string body), bool> _proxy;

        /// <summary>
        /// Initializes a new instance of the <see cref="EmailServiceAdapter"/> class.
        /// </summary>
        /// <param name="proxy">The proxy to wrap.</param>
        public EmailServiceAdapter(Proxy<(string to, string subject, string body), bool> proxy)
        {
            _proxy = proxy;
        }

        /// <inheritdoc/>
        public bool SendEmail(string to, string subject, string body)
            => _proxy.Execute((to, subject, body));
    }

    /// <summary>
    /// Demonstrates a complete mocking framework built with the proxy pattern.
    /// </summary>
    /// <remarks>
    /// Shows how to create test doubles, configure behavior, and verify interactions
    /// using the proxy pattern - the foundation of all .NET mocking frameworks.
    /// </remarks>
    public static void DemonstrateMockFramework()
    {
        DemonstrateMockFramework(Console.Out);
    }

    public static void DemonstrateMockFramework(TextWriter writer)
    {
        writer.WriteLine("\n=== Mock Framework - Test Doubles ===");

        // Create a mock email service
        var emailMock = MockFramework.CreateMock<(string to, string subject, string body), bool>();

        emailMock
            .Setup(input => input.to.Contains("@example.com"), true)
            .Setup(input => input.to.Contains("@spam.com"), false)
            .Returns(true); // Default

        var mockProxy = emailMock.Build();
        var emailService = new EmailServiceAdapter(mockProxy);

        // Test the service
        writer.WriteLine("Testing email service with mock:");

        var result1 = emailService.SendEmail("user@example.com", "Hello", "Welcome!");
        writer.WriteLine($"  Send to user@example.com: {result1}");

        var result2 = emailService.SendEmail("bad@spam.com", "Spam", "...");
        writer.WriteLine($"  Send to bad@spam.com: {result2}");

        var result3 = emailService.SendEmail("other@domain.com", "Test", "Content");
        writer.WriteLine($"  Send to other@domain.com: {result3}");

        // Verify interactions
        writer.WriteLine("\nVerifying mock interactions:");
        try
        {
            emailMock.VerifyAny(input => input.to == "user@example.com");
            writer.WriteLine("  ✓ Email sent to user@example.com");
        }
        catch (InvalidOperationException ex)
        {
            writer.WriteLine($"  ✗ {ex.Message}");
        }

        try
        {
            emailMock.Verify(input => input.to.Contains("@spam.com"), times: 1);
            writer.WriteLine("  ✓ Exactly 1 email sent to spam domain");
        }
        catch (InvalidOperationException ex)
        {
            writer.WriteLine($"  ✗ {ex.Message}");
        }

        writer.WriteLine($"\nTotal invocations: {emailMock.Invocations.Count}");
    }

    #endregion

    #region Remote Proxy Simulation

    /// <summary>
    /// Simulates a remote data service with network latency.
    /// </summary>
    /// <remarks>
    /// In real applications, this would represent a web service, REST API, or remote database.
    /// The proxy can add caching, retry logic, and circuit breakers to improve resilience.
    /// </remarks>
    public sealed class RemoteDataService
    {
        /// <summary>
        /// Fetches data from a remote server (simulated).
        /// </summary>
        /// <param name="id">The ID of the data to fetch.</param>
        /// <returns>The fetched data as a string.</returns>
        public string FetchData(int id)
        {
            Console.WriteLine($"[NETWORK] Fetching data from remote server for ID: {id}");
            Thread.Sleep(200); // Simulate network latency
            return $"Remote data for ID {id}";
        }
    }

    /// <summary>
    /// Demonstrates the remote proxy pattern with caching for network optimization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Combines multiple proxy concerns (logging + caching) to create an efficient
    /// remote proxy that minimizes network calls and provides visibility.
    /// </para>
    /// <para>
    /// Remote proxy use cases:
    /// <list type="bullet">
    /// <item><description>REST API clients</description></item>
    /// <item><description>Distributed object systems (RPC, gRPC)</description></item>
    /// <item><description>Database connection pools</description></item>
    /// <item><description>Message queue consumers</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static void DemonstrateRemoteProxy()
    {
        DemonstrateRemoteProxy(Console.Out);
    }

    #region RemoteProxy
    public static void DemonstrateRemoteProxy(TextWriter writer)
    {
        writer.WriteLine("\n=== Remote Proxy - Network Call Optimization ===");

        var remoteService = new RemoteDataService();
        var callCount = 0;

        // Combine caching with logging to create an efficient remote proxy
        var remoteProxy = Proxy<int, string>.Create(id =>
            {
                callCount++;
                return remoteService.FetchData(id);
            })
            .Intercept((id, next) =>
            {
                writer.WriteLine($"[PROXY] Request for ID: {id}");
                var result = next(id);
                writer.WriteLine($"[PROXY] Response received");
                return result;
            })
            .Build();

        // Now wrap with caching
        var cachedRemoteProxy = Proxy<int, string>.Create(
                id => remoteProxy.Execute(id))
            .CachingProxy()
            .Build();

        writer.WriteLine("First request for ID 42:");
        var r1 = cachedRemoteProxy.Execute(42);
        writer.WriteLine($"Result: {r1}\n");

        writer.WriteLine("Second request for ID 42 (cached):");
        var r2 = cachedRemoteProxy.Execute(42);
        writer.WriteLine($"Result: {r2}\n");

        writer.WriteLine("Request for ID 99:");
        var r3 = cachedRemoteProxy.Execute(99);
        writer.WriteLine($"Result: {r3}\n");

        writer.WriteLine($"Total network calls made: {callCount}");
    }
    #endregion

    #endregion

    /// <summary>
    /// Runs all proxy pattern demonstrations.
    /// </summary>
    public static void RunAllDemos()
    {
        RunAllDemos(Console.Out);
    }

    public static void RunAllDemos(TextWriter writer)
    {
        DemonstrateVirtualProxy(writer);
        DemonstrateProtectionProxy(writer);
        DemonstrateCachingProxy(writer);
        DemonstrateLoggingProxy(writer);
        DemonstrateCustomInterception(writer);
        DemonstrateMockFramework(writer);
        DemonstrateRemoteProxy(writer);
    }
}
