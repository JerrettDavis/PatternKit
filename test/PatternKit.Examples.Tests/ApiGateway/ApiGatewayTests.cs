using PatternKit.Examples.ApiGateway;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ApiGateway;

[Feature("Mini API Gateway routing & middleware")]
public sealed class ApiGatewayTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private static IReadOnlyDictionary<string, string> Headers(
        string? accept = "application/json",
        string? auth = null,
        string? requestId = null)
    {
        var d = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(accept)) d["Accept"] = accept;
        if (!string.IsNullOrEmpty(auth)) d["Authorization"] = auth;
        if (!string.IsNullOrEmpty(requestId)) d["X-Request-Id"] = requestId;
        return d;
    }

    private static MiniRouter DefaultRouter()
        => MiniRouter.Create()
            // middleware: first match wins (side effects only — not asserted here)
            .Use(
                static (in r) => r.Headers.ContainsKey("X-Request-Id"),
                static (in r) => Console.WriteLine($"reqid={r.Headers["X-Request-Id"]}"))
            .Use(
                static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal) &&
                                 !r.Headers.ContainsKey("Authorization"),
                static (in _) => Console.WriteLine("Denied: missing Authorization"))
            // routes
            .Map(
                static (in r) => r is { Method: "GET", Path: "/health" },
                static (in _) => Responses.Text(200, "OK"))
            .Map(
                static (in r) => r.Method == "GET" && r.Path.StartsWith("/users/", StringComparison.Ordinal),
                static (in r) =>
                {
                    var idStr = r.Path["/users/".Length..];
                    return int.TryParse(idStr, out var id)
                        ? Responses.Json(200, $"{{\"id\":{id},\"name\":\"user{id}\"}}")
                        : Responses.Text(404, "User not found");
                })
            .Map(
                static (in r) => r is { Method: "POST", Path: "/users" },
                static (in _) => Responses.Json(201, "{\"ok\":true}"))
            .Map(
                static (in r) => r.Path.StartsWith("/admin", StringComparison.Ordinal) &&
                                 !r.Headers.ContainsKey("Authorization"),
                static (in _) => Responses.Unauthorized())
            .NotFound(static (in _) => Responses.NotFound())
            .Build();

    // A router with a route that returns an empty content-type to exercise negotiation.
    private static MiniRouter NegotiatingRouter()
        => MiniRouter.Create()
            .Map(
                static (in r) => r is { Method: "GET", Path: "/neg" },
                static (in _) => new Response(200, "", "ok"))
            .NotFound(static (in _) => new Response(404, "", "nope"))
            .Build();

    // --- scenarios -----------------------------------------------------------

    [Scenario("GET /health returns 200 text/plain 'OK'")]
    [Fact]
    public async Task Health_Check()
    {
        await Given("a default router", DefaultRouter)
            .When("GET /health", r => r.Handle(new Request("GET", "/health", Headers())))
            .Then("status is 200", res => res.StatusCode == 200)
            .And("body is 'OK'", res => res.Body == "OK")
            .And("content-type is text/plain", res => res.ContentType.StartsWith("text/plain"))
            .AssertPassed();
    }

    [Scenario("GET /users/42 -> 200 application/json with id")]
    [Fact]
    public async Task Users_Get_By_Id()
    {
        await Given("a default router", DefaultRouter)
            .When("GET /users/42", r => r.Handle(new Request("GET", "/users/42", Headers())))
            .Then("status 200", res => res.StatusCode == 200)
            .And("content-type json", res => res.ContentType.StartsWith("application/json"))
            .And("body contains id 42", res => res.Body.Contains("\"id\":42"))
            .AssertPassed();
    }

    [Scenario("GET /users/abc -> 404 text/plain 'User not found'")]
    [Fact]
    public async Task Users_Get_Invalid_Id()
    {
        await Given("a default router", DefaultRouter)
            .When("GET /users/abc", r => r.Handle(new Request("GET", "/users/abc", Headers())))
            .Then("status 404", res => res.StatusCode == 404)
            .And("text/plain", res => res.ContentType.StartsWith("text/plain"))
            .And("body says 'User not found'", res => res.Body.Contains("User not found"))
            .AssertPassed();
    }

    [Scenario("POST /users -> 201 application/json {\"ok\":true}")]
    [Fact]
    public async Task Users_Create()
    {
        await Given("a default router", DefaultRouter)
            .When("POST /users", r => r.Handle(new Request("POST", "/users", Headers(), "{\"name\":\"Ada\"}")))
            .Then("status 201", res => res.StatusCode == 201)
            .And("content-type json", res => res.ContentType.StartsWith("application/json"))
            .And("body {\"ok\":true}", res => res.Body.Contains("\"ok\":true"))
            .AssertPassed();
    }

    [Scenario("GET /admin without Authorization -> 401 Unauthorized")]
    [Fact]
    public async Task Admin_Requires_Authorization()
    {
        await Given("a default router", DefaultRouter)
            .When("GET /admin/metrics without auth", r => r.Handle(new Request("GET", "/admin/metrics", Headers(auth: null))))
            .Then("status 401", res => res.StatusCode == 401)
            .And("text/plain", res => res.ContentType.StartsWith("text/plain"))
            .AssertPassed();
    }

    [Scenario("Content negotiation picks json or text when handler leaves content-type empty")]
    [Fact]
    public async Task Content_Negotiation_Works()
    {
        await Given("a negotiating router", NegotiatingRouter)
            .When("GET /neg with Accept: application/json", r => r.Handle(new Request("GET", "/neg", Headers(accept: "application/json"))))
            .Then("content-type is application/json", res => res.ContentType.StartsWith("application/json"))
            .And("status 200", res => res.StatusCode == 200)
            .And("body ok", res => res.Body == "ok")
            .AssertPassed();

        await Given("a negotiating router", NegotiatingRouter)
            .When("GET /neg with Accept: text/plain", r => r.Handle(new Request("GET", "/neg", Headers(accept: "text/plain"))))
            .Then("content-type is text/plain", res => res.ContentType.StartsWith("text/plain"))
            .AssertPassed();

        await Given("a negotiating router", NegotiatingRouter)
            .When("GET /neg with unknown Accept", r => r.Handle(new Request("GET", "/neg", Headers(accept: "application/xml"))))
            .Then("falls back to json", res => res.ContentType.StartsWith("application/json"))
            .AssertPassed();
    }

    [Scenario("Middleware is first-match-wins (only the first matching action executes)")]
    [Fact]
    public async Task Middleware_FirstMatch_Wins()
    {
        var hits = new List<string>();

        MiniRouter Build()
            => MiniRouter.Create()
                .Use(static (in r) => r.Path.StartsWith("/a"), (in _) => hits.Add("A"))
                .Use(static (in r) => r.Path.StartsWith("/a"), (in _) => hits.Add("B")) // also matches but should NOT run
                .Map(static (in _) => true, static (in _) => Responses.Text(200, "ok"))
                .NotFound(static (in _) => Responses.NotFound())
                .Build();

        await Given("a router with two matching middleware branches", Build)
            .When("GET /a", r => r.Handle(new Request("GET", "/a", Headers())))
            .Then("exactly one middleware ran", _ => hits.Count == 1)
            .And("the first middleware ran", _ => hits[0] == "A")
            .AssertPassed();
    }
}