using PatternKit.Behavioral.Chain;
using PatternKit.Examples.Chain;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Chain;

[Feature("Auth & Logging demo (ActionChain<HttpRequest>)")]
public sealed class AuthLoggingDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    // --- Helpers ------------------------------------------------------------

    private static IReadOnlyDictionary<string, string> H(
        string? requestId = null,
        string? auth = null)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(requestId)) d["X-Request-Id"] = requestId;
        if (!string.IsNullOrWhiteSpace(auth)) d["Authorization"] = auth;
        return d;
    }

    /// <summary>
    /// Recreates the demo chain so we can drive it with custom inputs.
    /// </summary>
    private static (ActionChain<HttpRequest> Chain, List<string> Log) BuildChain()
    {
        var log = new List<string>();

        var chain = ActionChain<HttpRequest>.Create()
            // request id (continue)
            .When(static (in r) => r.Headers.ContainsKey("X-Request-Id"))
            .ThenContinue(r => log.Add($"reqid={r.Headers["X-Request-Id"]}"))
            // admin requires auth (stop)
            .When(static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal)
                                   && !r.Headers.ContainsKey("Authorization"))
            .ThenStop(_ => log.Add("deny: missing auth"))
            // finally always logs method/path
            .Finally((in r, next) =>
            {
                log.Add($"{r.Method} {r.Path}");
                next(r); // terminal 'next' is a no-op
            })
            .Build();

        return (chain, log);
    }

    // --- Scenarios ----------------------------------------------------------

    [Scenario("AuthLoggingDemo.Run returns the expected three log lines in order")]
    [Fact]
    public Task Demo_Run_Smoke()
        => Given("the demo Run() helper", () => (Func<List<string>>)AuthLoggingDemo.Run)
            .When("running it", run => run())
            .Then("logs method/path for /health first", log => log.ElementAtOrDefault(0) == "GET /health")
            .And("then logs a deny for missing auth on /admin", log => log.ElementAtOrDefault(1) == "deny: missing auth")
            .And("stops after deny (no /admin method/path)", log => log.Count == 2 && !log.Any(l => l.Contains("/admin")))
            .AssertPassed();

    [Scenario("X-Request-Id is logged before method/path")]
    [Fact]
    public Task RequestId_Is_Logged_Then_MethodPath()
        => Given("a fresh chain+log", BuildChain)
            .When("executing a GET /users with X-Request-Id", t =>
            {
                var (chain, log) = t;
                chain.Execute(new HttpRequest("GET", "/users", H(requestId: "abc123")));
                return log;
            })
            .Then("first line is reqid", log => log[0] == "reqid=abc123")
            .And("second line is method/path", log => log[1] == "GET /users")
            .And("only these two lines exist", log => log.Count == 2)
            .AssertPassed();

    [Scenario("Admin without Authorization -> deny and still logs method/path (Finally runs)")]
    [Fact]
    public Task Admin_MissingAuth_Denies_And_Logs_Path()
        => Given("a fresh chain+log", BuildChain)
            .When("executing GET /admin/stats without auth", t =>
            {
                var (chain, log) = t;
                chain.Execute(new HttpRequest("GET", "/admin/stats", H()));
                return log;
            })
            .Then("first line is deny", log => log.ElementAtOrDefault(0) == "deny: missing auth")
            .And("no method/path is logged after stop", log => log.Count == 1)
            .AssertPassed();

    [Scenario("Admin with Authorization -> no deny, just method/path")]
    [Fact]
    public Task Admin_WithAuth_Allows_And_Logs_Path()
        => Given("a fresh chain+log", BuildChain)
            .When("executing GET /admin/metrics with bearer token", t =>
            {
                var (chain, log) = t;
                chain.Execute(new HttpRequest("GET", "/admin/metrics", H(auth: "Bearer token")));
                return log;
            })
            .Then("single line is method/path", log => log.SequenceEqual(["GET /admin/metrics"]))
            .AssertPassed();

    [Scenario("Order with both: X-Request-Id then deny then method/path")]
    [Fact]
    public Task RequestId_Then_Deny()
        => Given("a fresh chain+log", BuildChain)
            .When("executing GET /admin/x with X-Request-Id and no auth", t =>
            {
                var (chain, log) = t;
                chain.Execute(new HttpRequest("GET", "/admin/x", H(requestId: "rid-7")));
                return log;
            })
            .Then("reqid is logged first", log => log.ElementAtOrDefault(0) == "reqid=rid-7")
            .And("deny next", log => log.ElementAtOrDefault(1) == "deny: missing auth")
            .And("stops after deny (no method/path)", log => log.Count == 2 && !log.Any(s => s.StartsWith("GET ")))
            .AssertPassed();
}