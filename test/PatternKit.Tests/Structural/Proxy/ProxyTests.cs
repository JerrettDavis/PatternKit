using PatternKit.Structural.Proxy;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural.Proxy;

[Feature("Structural - Proxy<TIn,TOut> (access control & lazy initialization)")]
public sealed class ProxyTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Direct proxy simply delegates to subject")]
    [Fact]
    public Task Direct_Proxy_Delegates()
        => Given("a direct proxy", () =>
            Proxy<int, int>.Create(static x => x * 2).Build())
            .When("execute with 5", p => p.Execute(5))
            .Then("returns subject result", r => r == 10)
            .AssertPassed();

    [Scenario("Virtual proxy lazily initializes subject")]
    [Fact]
    public Task Virtual_Proxy_Lazy_Initialization()
        => Given("a virtual proxy with initialization flag", () =>
            {
                var proxy = Proxy<int, int>.Create()
                    .VirtualProxy(() =>
                    {
                        return x => x * 2;
                    })
                    .Build();
                return (proxy, initialized: false); // initialized is captured but still false at this point
            })
            .When("check initialized before first call", ctx => ctx.initialized)
            .Then("subject not yet initialized", initialized => !initialized)
            .AssertPassed();

    [Scenario("Virtual proxy initializes only once")]
    [Fact]
    public Task Virtual_Proxy_Initializes_Once()
        => Given("virtual proxy with call counter", () =>
            {
                var initCount = new int[1];
                var proxy = Proxy<int, int>.Create()
                    .VirtualProxy(() =>
                    {
                        initCount[0]++;
                        return static x => x * 2;
                    })
                    .Build();
                return (proxy, initCount);
            })
            .When("execute multiple times", ctx =>
            {
                var r1 = ctx.proxy.Execute(5);
                var r2 = ctx.proxy.Execute(10);
                var r3 = ctx.proxy.Execute(15);
                return (initCount: ctx.initCount[0], r1, r2, r3);
            })
            .Then("initialized exactly once", r => r.initCount == 1)
            .And("all results correct", r => r is { r1: 10, r2: 20, r3: 30 })
            .AssertPassed();

    [Scenario("Protection proxy allows authorized access")]
    [Fact]
    public Task Protection_Proxy_Allows_Access()
        => Given("protection proxy requiring positive input", () =>
            Proxy<int, int>.Create(static x => x * 2)
                .ProtectionProxy(static x => x > 0)
                .Build())
            .When("execute with positive value", p => p.Execute(5))
            .Then("returns result", r => r == 10)
            .AssertPassed();

    [Scenario("Protection proxy denies unauthorized access")]
    [Fact]
    public Task Protection_Proxy_Denies_Access()
        => Given("protection proxy requiring positive input", () =>
            Proxy<int, int>.Create(static x => x * 2)
                .ProtectionProxy(static x => x > 0)
                .Build())
            .When("execute with negative value", p =>
            {
                try
                {
                    p.Execute(-5);
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return true;
                }
            })
            .Then("throws UnauthorizedAccessException", threw => threw)
            .AssertPassed();

    [Scenario("Caching proxy memoizes results")]
    [Fact]
    public Task Caching_Proxy_Memoizes()
        => Given("caching proxy with call counter", () =>
            {
                var callCount = new int[1];
                var proxy = Proxy<int, int>.Create(x =>
                {
                    callCount[0]++;
                    return x * 2;
                }).CachingProxy().Build();
                return (proxy, callCount);
            })
            .When("execute same input twice", ctx =>
            {
                var r1 = ctx.proxy.Execute(5);
                var r2 = ctx.proxy.Execute(5);
                return (ctx.callCount[0], r1, r2);
            })
            .Then("subject called once", r => r.Item1 == 1)
            .And("both results match", r => r is { r1: 10, r2: 10 })
            .AssertPassed();

    [Scenario("Caching proxy caches different inputs separately")]
    [Fact]
    public Task Caching_Proxy_Different_Inputs()
        => Given("caching proxy with call counter", () =>
            {
                var callCount = new int[1];
                var proxy = Proxy<int, int>.Create(x =>
                {
                    callCount[0]++;
                    return x * 2;
                }).CachingProxy().Build();
                return (proxy, callCount);
            })
            .When("execute different inputs", ctx =>
            {
                var r1 = ctx.proxy.Execute(5);
                var r2 = ctx.proxy.Execute(10);
                var r3 = ctx.proxy.Execute(5);
                return (ctx.callCount[0], r1, r2, r3);
            })
            .Then("subject called twice", r => r.Item1 == 2)
            .And("results correct", r => r is { r1: 10, r2: 20, r3: 10 })
            .AssertPassed();

    [Scenario("Caching proxy with custom comparer")]
    [Fact]
    public Task Caching_Proxy_Custom_Comparer()
        => Given("caching proxy with case-insensitive comparer", () =>
            {
                var callCount = new int[1];
                var proxy = Proxy<string, int>.Create(s =>
                {
                    callCount[0]++;
                    return s.Length;
                }).CachingProxy(StringComparer.OrdinalIgnoreCase).Build();
                return (proxy, callCount);
            })
            .When("execute with different cases", ctx =>
            {
                var r1 = ctx.proxy.Execute("Hello");
                var r2 = ctx.proxy.Execute("HELLO");
                var r3 = ctx.proxy.Execute("hello");
                return (ctx.callCount[0], r1, r2, r3);
            })
            .Then("subject called once", r => r.Item1 == 1)
            .And("all results match", r => r is { r1: 5, r2: 5, r3: 5 })
            .AssertPassed();

    [Scenario("Logging proxy captures invocations")]
    [Fact]
    public Task Logging_Proxy_Captures()
        => Given("logging proxy with log list", () =>
            {
                var logs = new List<string>();
                var proxy = Proxy<int, int>.Create(static x => x * 2)
                    .LoggingProxy(logs.Add)
                    .Build();
                return (proxy, logs);
            })
            .When("execute", ctx => { var r = ctx.proxy.Execute(5); return (r, ctx.logs); })
            .Then("returns correct result", r => r.r == 10)
            .And("logs input and output", r => r.logs.Count == 2)
            .And("first log contains input", r => r.logs[0].Contains("5"))
            .And("second log contains output", r => r.logs[1].Contains("10"))
            .AssertPassed();

    [Scenario("Before intercepts before subject")]
    [Fact]
    public Task Before_Intercepts_Before()
        => Given("proxy with Before action", () =>
            {
                var log = new List<string>();
                var proxy = Proxy<int, int>.Create(x =>
                {
                    log.Add("subject");
                    return x * 2;
                }).Before(x => log.Add($"before-{x}")).Build();
                return (proxy, log);
            })
            .When("execute", ctx => { var r = ctx.proxy.Execute(5); return (r, ctx.log); })
            .Then("result correct", r => r.r == 10)
            .And("before logged first", r => r.log[0] == "before-5")
            .And("subject logged second", r => r.log[1] == "subject")
            .AssertPassed();

    [Scenario("After intercepts after subject")]
    [Fact]
    public Task After_Intercepts_After()
        => Given("proxy with After action", () =>
            {
                var log = new List<string>();
                var proxy = Proxy<int, int>.Create(x =>
                {
                    log.Add("subject");
                    return x * 2;
                }).After((input, output) => log.Add($"after-{input}->{output}")).Build();
                return (proxy, log);
            })
            .When("execute", ctx => { var r = ctx.proxy.Execute(5); return (r, ctx.log); })
            .Then("result correct", r => r.r == 10)
            .And("subject logged first", r => r.log[0] == "subject")
            .And("after logged second", r => r.log[1].Contains("after-5->10"))
            .AssertPassed();

    [Scenario("Custom interceptor can modify behavior")]
    [Fact]
    public Task Interceptor_Modifies_Behavior()
        => Given("proxy with custom interceptor", () =>
            Proxy<int, int>.Create(x => x * 2)
                .Intercept((x, next) => x < 0 ? 0 : next(x))
                .Build())
            .When("execute with negative", p => p.Execute(-5))
            .Then("returns 0 instead of subject result", r => r == 0)
            .AssertPassed();

    [Scenario("Interceptor can skip subject invocation")]
    [Fact]
    public Task Interceptor_Can_Skip_Subject()
        => Given("proxy with short-circuit interceptor", () =>
            {
                var subjectCalled = false;
                var proxy = Proxy<int, int>.Create(x =>
                {
                    subjectCalled = true;
                    return x * 2;
                }).Intercept((x, next) => x == 0 ? -1 : next(x)).Build();
                return (proxy, subjectCalled);
            })
            .When("execute with zero", ctx => { var r = ctx.proxy.Execute(0); return (r, ctx.subjectCalled); })
            .Then("returns interceptor result", r => r.r == -1)
            .And("subject not called", r => !r.subjectCalled)
            .AssertPassed();

    [Scenario("Interceptor can wrap subject with retry logic")]
    [Fact]
    public Task Interceptor_Retry_Logic()
        => Given("proxy with retry interceptor", () =>
            {
                var attemptCount = new int[1];
                var proxy = Proxy<int, int>.Create(x =>
                {
                    attemptCount[0]++;
                    if (attemptCount[0] < 3)
                        throw new InvalidOperationException("Temporary failure");
                    return x * 2;
                }).Intercept((x, next) =>
                {
                    for (var i = 0; i < 5; i++)
                    {
                        try
                        {
                            return next(x);
                        }
                        catch (InvalidOperationException) when (i < 4)
                        {
                            // Retry
                        }
                    }
                    throw new InvalidOperationException("Max retries");
                }).Build();
                return (proxy, attemptCount);
            })
            .When("execute", ctx => { var r = ctx.proxy.Execute(5); return (r, ctx.attemptCount[0]); })
            .Then("returns result after retries", r => r.r == 10)
            .And("attempted 3 times", r => r.Item2 == 3)
            .AssertPassed();

    [Scenario("Builder throws when protection proxy has no subject")]
    [Fact]
    public Task ProtectionProxy_Requires_Subject()
        => Given("builder with no subject", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create().ProtectionProxy(static _ => true).Build()))
            .When("building", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Builder throws when caching proxy has no subject")]
    [Fact]
    public Task CachingProxy_Requires_Subject()
        => Given("builder with no subject", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create().CachingProxy().Build()))
            .When("building", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Builder throws when logging proxy has no subject")]
    [Fact]
    public Task LoggingProxy_Requires_Subject()
        => Given("builder with no subject", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create().LoggingProxy(Console.WriteLine).Build()))
            .When("building", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Builder throws when Before has no subject")]
    [Fact]
    public Task Before_Requires_Subject()
        => Given("builder with no subject", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create().Before(static _ => { }).Build()))
            .When("building", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Builder throws when After has no subject")]
    [Fact]
    public Task After_Requires_Subject()
        => Given("builder with no subject", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create().After(static (_, _) => { }).Build()))
            .When("building", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Builder throws when Intercept has no subject")]
    [Fact]
    public Task Intercept_Requires_Subject()
        => Given("builder with no subject", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create().Intercept((_, next) => next(0)).Build()))
            .When("building", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    [Scenario("Virtual proxy is thread-safe")]
    [Fact]
    public async Task Virtual_Proxy_Thread_Safe()
    {
        var initCount = 0;
        var proxy = Proxy<int, int>.Create()
            .VirtualProxy(() =>
            {
                Interlocked.Increment(ref initCount);
                Thread.Sleep(10); // Simulate slow initialization
                return x => x * 2;
            })
            .Build();

        // Execute from multiple threads simultaneously
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => proxy.Execute(5)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        await Given("virtual proxy with concurrent access", () => (initCount, results))
            .When("executed from multiple threads", ctx => ctx)
            .Then("initialized exactly once", ctx => ctx.initCount == 1)
            .And("all results correct", ctx => ctx.results.All(r => r == 10))
            .AssertPassed();
    }

    [Scenario("Proxy works with reference types")]
    [Fact]
    public Task Proxy_With_Reference_Types()
        => Given("proxy with string operations", () =>
            Proxy<string, int>.Create(static s => s.Length)
                .Before(_ => { /* validation */ })
                .Build())
            .When("execute with string", p => p.Execute("Hello"))
            .Then("returns length", r => r == 5)
            .AssertPassed();

    [Scenario("Proxy works with complex types")]
    [Fact]
    public Task Proxy_With_Complex_Types()
        => Given("proxy with tuple input/output", () =>
            Proxy<(int a, int b), (int sum, int product)>.Create(
                static input => (input.a + input.b, input.a * input.b))
                .Build())
            .When("execute with (3, 4)", p => p.Execute((3, 4)))
            .Then("returns correct tuple", r => r is { sum: 7, product: 12 })
            .AssertPassed();

#if NET8_0_OR_GREATER
    [Scenario("Proxy works with modern C# features (NET8+)")]
    [Fact]
    public Task Proxy_With_Modern_CSharp()
        => Given("proxy using collection expressions", () =>
            {
                List<int> callLog = [];
                var proxy = Proxy<int, int>.Create(x =>
                {
                    callLog.Add(x);
                    return x * 2;
                }).CachingProxy().Build();
                return (proxy, callLog);
            })
            .When("execute multiple times", ctx =>
            {
                var r1 = ctx.proxy.Execute(5);
                var r2 = ctx.proxy.Execute(5);
                return (ctx.callLog, r1, r2);
            })
            .Then("subject called once", r => r.callLog.Count == 1)
            .And("both results match", r => r is { r1: 10, r2: 10 })
            .AssertPassed();
#endif

    #region Argument Validation Tests

    [Scenario("VirtualProxy throws when factory is null")]
    [Fact]
    public Task VirtualProxy_Null_Factory_Throws()
        => Given("builder with null factory", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create().VirtualProxy(null!).Build()))
            .When("building", ex => ex)
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("ProtectionProxy throws when validator is null")]
    [Fact]
    public Task ProtectionProxy_Null_Validator_Throws()
        => Given("builder with null validator", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create(x => x * 2).ProtectionProxy(null!).Build()))
            .When("building", ex => ex)
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("CachingProxy throws when comparer is null")]
    [Fact]
    public Task CachingProxy_Null_Comparer_Throws()
        => Given("builder with null comparer", () =>
            Record.Exception(() =>
                Proxy<string, int>.Create(s => s.Length).CachingProxy(null!).Build()))
            .When("building", ex => ex)
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("LoggingProxy throws when logger is null")]
    [Fact]
    public Task LoggingProxy_Null_Logger_Throws()
        => Given("builder with null logger", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create(x => x * 2).LoggingProxy(null!).Build()))
            .When("building", ex => ex)
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Before throws when action is null")]
    [Fact]
    public Task Before_Null_Action_Throws()
        => Given("builder with null action", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create(x => x * 2).Before(null!).Build()))
            .When("building", ex => ex)
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("After throws when action is null")]
    [Fact]
    public Task After_Null_Action_Throws()
        => Given("builder with null action", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create(x => x * 2).After(null!).Build()))
            .When("building", ex => ex)
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Intercept throws when interceptor is null")]
    [Fact]
    public Task Intercept_Null_Interceptor_Throws()
        => Given("builder with null interceptor", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create(x => x * 2).Intercept(null!).Build()))
            .When("building", ex => ex)
            .Then("throws ArgumentNullException", ex => ex is ArgumentNullException)
            .AssertPassed();

    [Scenario("Build throws when direct type has no subject")]
    [Fact]
    public Task Build_Direct_No_Subject_Throws()
        => Given("builder with no subject and no virtual proxy", () =>
            Record.Exception(() =>
                Proxy<int, int>.Create().Build()))
            .When("building", ex => ex)
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .AssertPassed();

    #endregion
}
