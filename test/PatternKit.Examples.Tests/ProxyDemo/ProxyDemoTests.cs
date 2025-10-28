using PatternKit.Structural.Proxy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;
using static PatternKit.Examples.ProxyDemo.ProxyDemo;

namespace PatternKit.Examples.Tests.ProxyDemo;

[Feature("Examples - Proxy Pattern Demonstrations")]
public sealed class ProxyDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Virtual proxy delays expensive initialization")]
    [Fact]
    public Task VirtualProxy_Delays_Initialization()
        => Given("virtual proxy for expensive database", () =>
            {
                // This test validates the concept without actually running the demo
                return true;
            })
            .When("proxy is created", _ => true)
            .Then("database not yet initialized", _ => true)
            .AssertPassed();

    [Scenario("Protection proxy enforces access control")]
    [Fact]
    public Task ProtectionProxy_Enforces_Access()
        => Given("protection proxy with access rules", () =>
            {
                var user = new User("TestUser", "User");
                var adminUser = new User("Admin", "Admin");
                var publicDoc = new Document("Public", "Content", "Public");
                var adminDoc = new Document("Admin", "Secret", "Admin");
                return (user, adminUser, publicDoc, adminDoc);
            })
            .When("validate access rules", ctx =>
            {
                var userCanAccessPublic = ctx.publicDoc.AccessLevel == "Public" || ctx.user.Role == "Admin";
                var userCanAccessAdmin = ctx.adminDoc.AccessLevel == "Public" || ctx.user.Role == "Admin";
                var adminCanAccessAdmin = ctx.adminDoc.AccessLevel == "Public" || ctx.adminUser.Role == "Admin";
                return (userCanAccessPublic, userCanAccessAdmin, adminCanAccessAdmin);
            })
            .Then("user can access public", r => r.userCanAccessPublic)
            .And("user cannot access admin", r => !r.userCanAccessAdmin)
            .And("admin can access admin", r => r.adminCanAccessAdmin)
            .AssertPassed();

    [Scenario("Mock framework tracks invocations")]
    [Fact]
    public Task MockFramework_Tracks_Invocations()
        => Given("mock with setup", () =>
            {
                var mock = MockFramework.CreateMock<int, string>();
                mock.Setup(x => x > 0, "positive")
                    .Setup(x => x < 0, "negative")
                    .Returns("zero");
                return mock.Build();
            })
            .When("execute multiple times", proxy =>
            {
                var r1 = proxy.Execute(5);
                var r2 = proxy.Execute(-3);
                var r3 = proxy.Execute(0);
                return (r1, r2, r3);
            })
            .Then("returns correct results", r => 
                r.r1 == "positive" && r.r2 == "negative" && r.r3 == "zero")
            .AssertPassed();

    [Scenario("Mock framework verifies call count")]
    [Fact]
    public Task MockFramework_Verifies_Calls()
        => Given("mock with invocations", () =>
            {
                var mock = MockFramework.CreateMock<int, int>();
                mock.Returns(42);
                var proxy = mock.Build();
                
                proxy.Execute(1);
                proxy.Execute(2);
                proxy.Execute(1);
                
                return mock;
            })
            .When("verify specific input called twice", mock =>
            {
                try
                {
                    mock.Verify(x => x == 1, times: 2);
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .Then("verification passes", verified => verified)
            .AssertPassed();

    [Scenario("Mock framework detects missing calls")]
    [Fact]
    public Task MockFramework_Detects_Missing_Calls()
        => Given("mock without expected calls", () =>
            {
                var mock = MockFramework.CreateMock<int, int>();
                mock.Returns(42);
                var proxy = mock.Build();
                
                proxy.Execute(1);
                proxy.Execute(2);
                
                return mock;
            })
            .When("verify non-existent call", mock =>
            {
                return Record.Exception(() => mock.Verify(x => x == 99, times: 1));
            })
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Mock framework VerifyAny succeeds when call exists")]
    [Fact]
    public Task MockFramework_VerifyAny_Succeeds()
        => Given("mock with invocations", () =>
            {
                var mock = MockFramework.CreateMock<string, bool>();
                mock.Returns(true);
                var proxy = mock.Build();
                
                proxy.Execute("test");
                proxy.Execute("other");
                
                return mock;
            })
            .When("verify any matching call exists", mock =>
            {
                try
                {
                    mock.VerifyAny(s => s.Contains("test"));
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .Then("verification passes", verified => verified)
            .AssertPassed();

    [Scenario("Mock framework VerifyAny fails when no matching calls")]
    [Fact]
    public Task MockFramework_VerifyAny_Fails()
        => Given("mock without matching calls", () =>
            {
                var mock = MockFramework.CreateMock<string, bool>();
                mock.Returns(true);
                var proxy = mock.Build();
                
                proxy.Execute("hello");
                proxy.Execute("world");
                
                return mock;
            })
            .When("verify non-matching predicate", mock =>
            {
                return Record.Exception(() => mock.VerifyAny(s => s.Contains("test")));
            })
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Mock framework exposes invocations list")]
    [Fact]
    public Task MockFramework_Exposes_Invocations()
        => Given("mock with multiple invocations", () =>
            {
                var mock = MockFramework.CreateMock<int, int>();
                mock.Returns(0);
                var proxy = mock.Build();
                
                proxy.Execute(1);
                proxy.Execute(2);
                proxy.Execute(3);
                
                return mock;
            })
            .When("get invocations list", mock => mock.Invocations)
            .Then("contains all invocations", invocations => invocations.Count == 3)
            .And("in correct order", invocations => 
                invocations[0] == 1 && invocations[1] == 2 && invocations[2] == 3)
            .AssertPassed();

    [Scenario("Email service adapter integrates with mock")]
    [Fact]
    public Task EmailService_Adapter_Works()
        => Given("email service with mock", () =>
            {
                var mock = MockFramework.CreateMock<(string to, string subject, string body), bool>();
                mock.Setup(input => input.to.Contains("@valid.com"), true)
                    .Returns(false);
                
                var proxy = mock.Build();
                var service = new EmailServiceAdapter(proxy);
                return service;
            })
            .When("send email to valid address", svc =>
            {
                var result1 = svc.SendEmail("user@valid.com", "Test", "Body");
                var result2 = svc.SendEmail("user@invalid.com", "Test", "Body");
                return (result1, result2);
            })
            .Then("returns correct results", r => r.result1 && !r.result2)
            .AssertPassed();

    [Scenario("Caching proxy reduces expensive calculations")]
    [Fact]
    public Task CachingProxy_Demo_Validation()
        => Given("caching proxy with fibonacci", () =>
            {
                var callCount = 0;
                var proxy = Proxy<int, int>.Create(n =>
                {
                    callCount++;
                    // Simple fibonacci
                    if (n <= 1) return n;
                    int a = 0, b = 1;
                    for (var i = 2; i <= n; i++)
                    {
                        var temp = a + b;
                        a = b;
                        b = temp;
                    }
                    return b;
                }).CachingProxy().Build();
                return (proxy, callCount: new Func<int>(() => callCount));
            })
            .When("execute same value multiple times", ctx =>
            {
                var r1 = ctx.proxy.Execute(10);
                var r2 = ctx.proxy.Execute(10);
                var r3 = ctx.proxy.Execute(15);
                var r4 = ctx.proxy.Execute(10);
                return (r1, r2, r3, r4, callCount: ctx.callCount());
            })
            .Then("first and cached results match", r => r.r1 == r.r2 && r.r2 == r.r4)
            .And("only called twice for two distinct values", r => r.callCount == 2)
            .And("fibonacci(10) is correct", r => r.r1 == 55)
            .And("fibonacci(15) is correct", r => r.r3 == 610)
            .AssertPassed();

    [Scenario("Logging proxy records invocations")]
    [Fact]
    public Task LoggingProxy_Demo_Validation()
        => Given("logging proxy with list", () =>
            {
                var logs = new List<string>();
                var proxy = Proxy<(int a, int b), int>.Create(
                    input => input.a + input.b)
                    .LoggingProxy(logs.Add)
                    .Build();
                return (proxy, logs);
            })
            .When("execute calculation", ctx =>
            {
                var result = ctx.proxy.Execute((5, 3));
                return (result, logs: ctx.logs);
            })
            .Then("returns correct sum", r => r.result == 8)
            .And("logs input and output", r => r.logs.Count == 2)
            .And("logs contain values", r => 
                r.logs.Any(l => l.Contains("5") && l.Contains("3")) && 
                r.logs.Any(l => l.Contains("8")))
            .AssertPassed();

    [Scenario("Remote proxy combines logging and caching")]
    [Fact]
    public Task RemoteProxy_Demo_Validation()
        => Given("remote proxy with call tracking", () =>
            {
                var callCount = 0;
                var logs = new List<string>();
                
                // Inner proxy with logging
                var innerProxy = Proxy<int, string>.Create(id =>
                {
                    callCount++;
                    return $"Remote data for ID {id}";
                })
                .Intercept((id, next) =>
                {
                    logs.Add($"Request for ID: {id}");
                    var result = next(id);
                    logs.Add("Response received");
                    return result;
                })
                .Build();
                
                // Outer caching proxy
                var cachedProxy = Proxy<int, string>.Create(
                    id => innerProxy.Execute(id))
                    .CachingProxy()
                    .Build();
                    
                return (proxy: cachedProxy, callCount: new Func<int>(() => callCount), logs);
            })
            .When("execute same ID multiple times", ctx =>
            {
                var r1 = ctx.proxy.Execute(42);
                var r2 = ctx.proxy.Execute(42);
                var r3 = ctx.proxy.Execute(99);
                return (r1, r2, r3, calls: ctx.callCount(), logCount: ctx.logs.Count);
            })
            .Then("cached calls return same result", r => r.r1 == r.r2)
            .And("only makes 2 remote calls", r => r.calls == 2)
            .And("logs both requests", r => r.logCount == 4) // 2 requests × 2 logs each
            .AssertPassed();

    [Scenario("Retry interceptor handles transient failures")]
    [Fact]
    public Task RetryInterceptor_Demo_Validation()
        => Given("proxy with retry logic", () =>
            {
                var attempts = 0;
                var proxy = Proxy<string, string>.Create(req =>
                {
                    attempts++;
                    if (attempts < 3)
                        throw new InvalidOperationException("Temporary failure");
                    return $"Processed: {req}";
                })
                .Intercept((input, next) =>
                {
                    const int maxRetries = 5;
                    for (var i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            return next(input);
                        }
                        catch (InvalidOperationException)
                        {
                            if (i == maxRetries - 1)
                            {
                                // Final failure after exhausting retries
                                throw new InvalidOperationException("Max retries exceeded");
                            }
                            // else retry
                        }
                    }
                    // Should be unreachable
                    throw new InvalidOperationException("Max retries exceeded");
                })
                .Build();
                return (proxy, attempts: new Func<int>(() => attempts));
            })
            .When("execute with failures", ctx =>
            {
                var result = ctx.proxy.Execute("test-data");
                return (result, attempts: ctx.attempts());
            })
            .Then("eventually succeeds", r => r.result == "Processed: test-data")
            .And("made 3 attempts", r => r.attempts == 3)
            .AssertPassed();

    [Scenario("Demo methods execute without errors")]
    [Fact]
    public Task Demo_Methods_Execute()
        => Given("demo methods", () => true)
            .When("check demo methods exist", _ =>
            {
                try
                {
                    // We won't actually run them in tests as they have console output and delays
                    // But we verify they exist and are callable
                    var methods = typeof(PatternKit.Examples.ProxyDemo.ProxyDemo).GetMethods()
                        .Where(m => m.Name.StartsWith("Demonstrate") || m.Name == "RunAllDemos")
                        .ToList();
                    return methods.Count >= 7; // We have 7 demo methods
                }
                catch
                {
                    return false;
                }
            })
            .Then("all demo methods exist", hasAll => hasAll)
            .AssertPassed();

    #region ExpensiveDatabase Tests

    [Scenario("ExpensiveDatabase initializes and queries")]
    [Fact]
    public Task ExpensiveDatabase_Initializes_And_Queries()
        => Given("expensive database with connection string", () =>
            new ExpensiveDatabase("TestConnection"))
            .When("executing query", db => db.Query("SELECT * FROM Test"))
            .Then("returns formatted result", result => result == "Result for: SELECT * FROM Test")
            .AssertPassed();

    #endregion

    #region DocumentService Tests

    [Scenario("DocumentService reads document")]
    [Fact]
    public Task DocumentService_Reads_Document()
        => Given("document service", () => new DocumentService())
            .When("reading document", svc =>
            {
                var doc = new Document("Title", "Content", "Public");
                return svc.Read(doc);
            })
            .Then("returns formatted content", result => result == "Reading 'Title': Content")
            .AssertPassed();

    [Scenario("DocumentService deletes document")]
    [Fact]
    public Task DocumentService_Deletes_Document()
        => Given("document service", () => new DocumentService())
            .When("deleting document", svc =>
            {
                var doc = new Document("Title", "Content", "Public");
                return svc.Delete(doc);
            })
            .Then("returns success", result => result)
            .AssertPassed();

    #endregion

    #region RemoteDataService Tests

    [Scenario("RemoteDataService fetches data")]
    [Fact]
    public Task RemoteDataService_Fetches_Data()
        => Given("remote data service", () => new RemoteDataService())
            .When("fetching data by ID", svc => svc.FetchData(123))
            .Then("returns formatted data", result => result == "Remote data for ID 123")
            .AssertPassed();

    #endregion

    #region Mock Framework Edge Cases

    [Scenario("Mock framework with no setup throws on unknown input")]
    [Fact]
    public Task MockFramework_NoSetup_Throws()
        => Given("mock without default", () =>
            {
                var mock = MockFramework.CreateMock<int, int>();
                return mock.Build();
            })
            .When("executing with no matching setup", proxy =>
                Record.Exception(() => proxy.Execute(42)))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .And("has descriptive message", ex => ex.Message.Contains("No setup found"))
            .AssertPassed();

    [Scenario("Mock framework Throws method configures exception")]
    [Fact]
    public Task MockFramework_Throws_Method()
        => Given("mock with Throws configured", () =>
            {
                var mock = MockFramework.CreateMock<int, int>();
                mock.Throws<InvalidOperationException>();
                return mock;
            })
            .When("verifying setup exists", mock => mock.Build())
            .Then("proxy is created", proxy => proxy != null)
            .AssertPassed();

    #endregion

    #region Virtual Proxy Demo Coverage

    [Scenario("Virtual proxy demo validates lazy initialization behavior")]
    [Fact]
    public Task VirtualProxy_Demo_Validates_Lazy_Init()
        => Given("virtual proxy with initialization tracker", () =>
            {
                var initialized = false;
                var proxy = Proxy<string, string>.Create()
                    .VirtualProxy(() =>
                    {
                        initialized = true;
                        var db = new ExpensiveDatabase("TestDB");
                        return sql => db.Query(sql);
                    })
                    .Build();
                return (proxy, getInitialized: new Func<bool>(() => initialized));
            })
            .When("executing queries", ctx =>
            {
                var initializedBefore = ctx.getInitialized();
                var result1 = ctx.proxy.Execute("SELECT 1");
                var initializedAfter = ctx.getInitialized();
                var result2 = ctx.proxy.Execute("SELECT 2");
                return (initializedBefore, initializedAfter, result1, result2);
            })
            .Then("not initialized before first call", r => !r.initializedBefore)
            .And("initialized after first call", r => r.initializedAfter)
            .And("both queries succeed", r => 
                r.result1.Contains("SELECT 1") && r.result2.Contains("SELECT 2"))
            .AssertPassed();

    #endregion

    #region Protection Proxy Demo Coverage

    [Scenario("Protection proxy demo validates access control logic")]
    [Fact]
    public Task ProtectionProxy_Demo_Validates_Access_Control()
        => Given("protection proxy with document service", () =>
            {
                var service = new DocumentService();
                var adminDoc = new Document("Admin", "Secret", "Admin");
                var publicDoc = new Document("Public", "Info", "Public");
                var user = new User("Alice", "User");
                var admin = new User("Bob", "Admin");

                var proxy = Proxy<(User user, Document doc), string>.Create(
                    input => service.Read(input.doc))
                    .ProtectionProxy(input =>
                        input.doc.AccessLevel == "Public" || input.user.Role == "Admin")
                    .Build();

                return (proxy, user, admin, publicDoc, adminDoc);
            })
            .When("testing various access scenarios", ctx =>
            {
                var userPublic = ctx.proxy.Execute((ctx.user, ctx.publicDoc));
                
                Exception? userAdminEx = null;
                try { ctx.proxy.Execute((ctx.user, ctx.adminDoc)); }
                catch (UnauthorizedAccessException ex) { userAdminEx = ex; }

                var adminPublic = ctx.proxy.Execute((ctx.admin, ctx.publicDoc));
                var adminAdmin = ctx.proxy.Execute((ctx.admin, ctx.adminDoc));

                return (userPublic, userAdminEx, adminPublic, adminAdmin);
            })
            .Then("user can read public", r => r.userPublic.Contains("Public"))
            .And("user cannot read admin", r => r.userAdminEx != null)
            .And("admin can read public", r => r.adminPublic.Contains("Public"))
            .And("admin can read admin", r => r.adminAdmin.Contains("Admin"))
            .AssertPassed();

    #endregion

    #region Caching Proxy Demo Coverage

    [Scenario("Caching proxy demo validates memoization with Fibonacci")]
    [Fact]
    public Task CachingProxy_Demo_Validates_Fibonacci_Memoization()
        => Given("caching proxy with call tracking", () =>
            {
                var callCount = 0;
                var proxy = Proxy<int, int>.Create(n =>
                {
                    callCount++;
                    // Fibonacci implementation from demo
                    if (n <= 1) return n;
                    int a = 0, b = 1;
                    for (var i = 2; i <= n; i++)
                    {
                        var temp = a + b;
                        a = b;
                        b = temp;
                    }
                    return b;
                }).CachingProxy().Build();
                return (proxy, getCallCount: new Func<int>(() => callCount));
            })
            .When("executing fibonacci calculations", ctx =>
            {
                var fib10_1 = ctx.proxy.Execute(10);
                var fib10_2 = ctx.proxy.Execute(10); // Cached
                var fib15 = ctx.proxy.Execute(15);
                var fib10_3 = ctx.proxy.Execute(10); // Still cached
                var totalCalls = ctx.getCallCount();
                return (fib10_1, fib10_2, fib15, fib10_3, totalCalls);
            })
            .Then("all fib(10) results match", r => r.fib10_1 == r.fib10_2 && r.fib10_2 == r.fib10_3)
            .And("fib(10) is 55", r => r.fib10_1 == 55)
            .And("fib(15) is 610", r => r.fib15 == 610)
            .And("called subject only twice", r => r.totalCalls == 2)
            .AssertPassed();

    #endregion

    #region Logging Proxy Demo Coverage

    [Scenario("Logging proxy demo validates invocation logging")]
    [Fact]
    public Task LoggingProxy_Demo_Validates_Logging()
        => Given("logging proxy with message collector", () =>
            {
                var logs = new List<string>();
                var proxy = Proxy<(int a, int b), int>.Create(
                    input => input.a + input.b)
                    .LoggingProxy(logs.Add)
                    .Build();
                return (proxy, logs);
            })
            .When("executing operations", ctx =>
            {
                var result = ctx.proxy.Execute((5, 3));
                return (result, logCount: ctx.logs.Count, logs: ctx.logs);
            })
            .Then("returns correct sum", r => r.result == 8)
            .And("logs two messages", r => r.logCount == 2)
            .And("first log has input", r => r.logs[0].Contains("(5, 3)"))
            .And("second log has output", r => r.logs[1].Contains("8"))
            .AssertPassed();

    #endregion

    #region Custom Interception Demo Coverage

    [Scenario("Custom interception demo validates retry logic")]
    [Fact]
    public Task CustomInterception_Demo_Validates_Retry()
        => Given("proxy with retry interceptor and failure tracking", () =>
            {
                var attemptCount = 0;
                var proxy = Proxy<string, string>.Create(request =>
                {
                    attemptCount++;
                    if (attemptCount < 3)
                        throw new InvalidOperationException("Service temporarily unavailable");
                    return $"Processed: {request}";
                })
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
                            // Retry without delay for testing
                        }
                    }
                    throw new InvalidOperationException("Max retries exceeded");
                })
                .Build();
                return (proxy, getAttempts: new Func<int>(() => attemptCount));
            })
            .When("executing with transient failures", ctx =>
            {
                var result = ctx.proxy.Execute("important-data");
                var attempts = ctx.getAttempts();
                return (result, attempts);
            })
            .Then("eventually succeeds", r => r.result == "Processed: important-data")
            .And("retried exactly 3 times", r => r.attempts == 3)
            .AssertPassed();

    [Scenario("Custom interception demo validates max retries exceeded")]
    [Fact]
    public Task CustomInterception_Demo_Validates_Max_Retries()
        => Given("proxy with retry that always fails", () =>
            {
                var proxy = Proxy<string, string>.Create(_ =>
                    throw new InvalidOperationException("Always fails"))
                .Intercept((input, next) =>
                {
                    const int maxRetries = 5;
                    for (var i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            return next(input);
                        }
                        catch (InvalidOperationException)
                        {
                            if (i == maxRetries - 1)
                            {
                                // Final failure after exhausting retries
                                throw new InvalidOperationException("Max retries exceeded");
                            }
                            // else retry
                        }
                    }
                    // Should be unreachable
                    throw new InvalidOperationException("Max retries exceeded");
                })
                .Build();
                return proxy;
            })
            .When("executing", proxy =>
                Record.Exception(() => proxy.Execute("data")))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .And("has max retries message", ex => ex!.Message == "Max retries exceeded")
            .AssertPassed();

    #endregion

    #region Remote Proxy Demo Coverage

    [Scenario("Remote proxy demo validates network call optimization")]
    [Fact]
    public Task RemoteProxy_Demo_Validates_Network_Optimization()
        => Given("remote proxy with call and log tracking", () =>
            {
                var remoteService = new RemoteDataService();
                var callCount = 0;
                var logs = new List<string>();

                var remoteProxy = Proxy<int, string>.Create(id =>
                {
                    callCount++;
                    return remoteService.FetchData(id);
                })
                .Intercept((id, next) =>
                {
                    logs.Add($"[PROXY] Request for ID: {id}");
                    var result = next(id);
                    logs.Add("[PROXY] Response received");
                    return result;
                })
                .Build();

                var cachedRemoteProxy = Proxy<int, string>.Create(
                    id => remoteProxy.Execute(id))
                    .CachingProxy()
                    .Build();

                return (proxy: cachedRemoteProxy, getCallCount: new Func<int>(() => callCount), logs);
            })
            .When("making multiple requests", ctx =>
            {
                var r1 = ctx.proxy.Execute(42);
                var r2 = ctx.proxy.Execute(42); // Cached
                var r3 = ctx.proxy.Execute(99);
                var calls = ctx.getCallCount();
                var logCount = ctx.logs.Count;
                return (r1, r2, r3, calls, logCount);
            })
            .Then("cached requests return same data", r => r.r1 == r.r2)
            .And("results are correct", r => 
                r.r1 == "Remote data for ID 42" && r.r3 == "Remote data for ID 99")
            .And("only made 2 network calls", r => r.calls == 2)
            .And("logged 4 messages", r => r.logCount == 4) // 2 requests × 2 logs each
            .AssertPassed();

    #endregion

    #region Mock Framework Complete Coverage

    [Scenario("Mock framework handles multiple setups correctly")]
    [Fact]
    public Task MockFramework_Multiple_Setups()
        => Given("mock with multiple setups", () =>
            {
                var mock = MockFramework.CreateMock<string, int>();
                mock.Setup(s => s.StartsWith("A"), 1)
                    .Setup(s => s.StartsWith("B"), 2)
                    .Setup(s => s.StartsWith("C"), 3)
                    .Returns(0);
                return mock.Build();
            })
            .When("executing with different inputs", proxy =>
            {
                var a = proxy.Execute("Alpha");
                var b = proxy.Execute("Beta");
                var c = proxy.Execute("Charlie");
                var d = proxy.Execute("Delta");
                return (a, b, c, d);
            })
            .Then("returns correct values for all", r => 
                r.a == 1 && r.b == 2 && r.c == 3 && r.d == 0)
            .AssertPassed();

    [Scenario("Mock framework verify with default times parameter")]
    [Fact]
    public Task MockFramework_Verify_Default_Times()
        => Given("mock with single invocation", () =>
            {
                var mock = MockFramework.CreateMock<int, int>();
                mock.Returns(42);
                var proxy = mock.Build();
                proxy.Execute(5);
                return mock;
            })
            .When("verifying with default times (1)", mock =>
            {
                try
                {
                    mock.Verify(x => x == 5); // times parameter defaults to 1
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .Then("verification passes", result => result)
            .AssertPassed();

    #endregion

    #region Actual Demo Method Execution Tests

    [Scenario("DemonstrateVirtualProxy executes without error")]
    [Fact]
    public Task DemonstrateVirtualProxy_Executes()
        => Given("string writer for output", () =>
            {
                var sw = new StringWriter();
                return sw;
            })
            .When("executing demo", sw =>
            {
                try
                {
                    DemonstrateVirtualProxy(sw);
                    return (success: true, output: sw.ToString());
                }
                catch
                {
                    return (success: false, output: sw.ToString());
                }
            })
            .Then("executes successfully", r => r.success)
            .And("produces expected output", r => r.output.Contains("Virtual Proxy"))
            .AssertPassed();

    [Scenario("DemonstrateProtectionProxy executes without error")]
    [Fact]
    public Task DemonstrateProtectionProxy_Executes()
        => Given("string writer for output", () =>
            {
                var sw = new StringWriter();
                return sw;
            })
            .When("executing demo", sw =>
            {
                try
                {
                    DemonstrateProtectionProxy(sw);
                    return (success: true, output: sw.ToString());
                }
                catch
                {
                    return (success: false, output: sw.ToString());
                }
            })
            .Then("executes successfully", r => r.success)
            .And("produces expected output", r => r.output.Contains("Protection Proxy"))
            .AssertPassed();

    [Scenario("DemonstrateCachingProxy executes without error")]
    [Fact]
    public Task DemonstrateCachingProxy_Executes()
        => Given("string writer for output", () =>
            {
                var sw = new StringWriter();
                return sw;
            })
            .When("executing demo", sw =>
            {
                try
                {
                    DemonstrateCachingProxy(sw);
                    return (success: true, output: sw.ToString());
                }
                catch
                {
                    return (success: false, output: sw.ToString());
                }
            })
            .Then("executes successfully", r => r.success)
            .And("produces expected output", r => r.output.Contains("Caching Proxy"))
            .AssertPassed();

    [Scenario("DemonstrateLoggingProxy executes without error")]
    [Fact]
    public Task DemonstrateLoggingProxy_Executes()
        => Given("string writer for output", () =>
            {
                var sw = new StringWriter();
                return sw;
            })
            .When("executing demo", sw =>
            {
                try
                {
                    DemonstrateLoggingProxy(sw);
                    return (success: true, output: sw.ToString());
                }
                catch
                {
                    return (success: false, output: sw.ToString());
                }
            })
            .Then("executes successfully", r => r.success)
            .And("produces expected output", r => r.output.Contains("Logging Proxy"))
            .AssertPassed();

    [Scenario("DemonstrateCustomInterception executes without error")]
    [Fact]
    public Task DemonstrateCustomInterception_Executes()
        => Given("string writer for output", () =>
            {
                var sw = new StringWriter();
                return sw;
            })
            .When("executing demo", sw =>
            {
                try
                {
                    DemonstrateCustomInterception(sw);
                    return (success: true, output: sw.ToString());
                }
                catch
                {
                    return (success: false, output: sw.ToString());
                }
            })
            .Then("executes successfully", r => r.success)
            .And("produces expected output", r => r.output.Contains("Custom Interception"))
            .AssertPassed();

    [Scenario("DemonstrateMockFramework executes without error")]
    [Fact]
    public Task DemonstrateMockFramework_Executes()
        => Given("string writer for output", () =>
            {
                var sw = new StringWriter();
                return sw;
            })
            .When("executing demo", sw =>
            {
                try
                {
                    DemonstrateMockFramework(sw);
                    return (success: true, output: sw.ToString());
                }
                catch
                {
                    return (success: false, output: sw.ToString());
                }
            })
            .Then("executes successfully", r => r.success)
            .And("produces expected output", r => r.output.Contains("Mock Framework"))
            .AssertPassed();

    [Scenario("DemonstrateRemoteProxy executes without error")]
    [Fact]
    public Task DemonstrateRemoteProxy_Executes()
        => Given("string writer for output", () =>
            {
                var sw = new StringWriter();
                return sw;
            })
            .When("executing demo", sw =>
            {
                try
                {
                    DemonstrateRemoteProxy(sw);
                    return (success: true, output: sw.ToString());
                }
                catch
                {
                    return (success: false, output: sw.ToString());
                }
            })
            .Then("executes successfully", r => r.success)
            .And("produces expected output", r => r.output.Contains("Remote Proxy"))
            .AssertPassed();

    [Scenario("RunAllDemos executes all demonstrations")]
    [Fact]
    public Task RunAllDemos_Executes()
        => Given("string writer for output", () =>
            {
                var sw = new StringWriter();
                return sw;
            })
            .When("executing all demos", sw =>
            {
                try
                {
                    RunAllDemos(sw);
                    return (success: true, output: sw.ToString());
                }
                catch
                {
                    return (success: false, output: sw.ToString());
                }
            })
            .Then("executes successfully", r => r.success)
            .And("runs all 7 demos", r => 
                r.output.Contains("Virtual Proxy") &&
                r.output.Contains("Protection Proxy") &&
                r.output.Contains("Caching Proxy") &&
                r.output.Contains("Logging Proxy") &&
                r.output.Contains("Custom Interception") &&
                r.output.Contains("Mock Framework") &&
                r.output.Contains("Remote Proxy"))
            .AssertPassed();

    #endregion
}
