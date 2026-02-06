using PatternKit.Examples.Generators.Chain;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Generators.Chain;

[Feature("Chain Generator Example")]
public sealed class ChainGeneratorDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Demo runs without errors")]
    [Fact]
    public async Task Demo_Runs_Successfully()
    {
        await Given("the chain generator demo", () => ChainGeneratorDemo.Run())
            .Then("it should return output lines", lines => lines.Count > 0)
            .AssertPassed();
    }

    [Scenario("Responsibility model routes GET /health")]
    [Fact]
    public async Task Responsibility_Routes_HealthCheck()
    {
        await Given("a request router", () => new RequestRouter())
            .When("handling GET /health", router =>
            {
                var req = new HttpRequest("/health", "GET", null);
                return router.Handle(in req);
            })
            .Then("should return 200 OK: healthy", result => result == "200 OK: healthy")
            .AssertPassed();
    }

    [Scenario("Responsibility model returns 404 for unknown route")]
    [Fact]
    public async Task Responsibility_Returns_NotFound()
    {
        await Given("a request router", () => new RequestRouter())
            .When("handling DELETE /unknown", router =>
            {
                var req = new HttpRequest("/unknown", "DELETE", null);
                return router.Handle(in req);
            })
            .Then("should return 404 Not Found", result => result.StartsWith("404 Not Found"))
            .AssertPassed();
    }

    [Scenario("Responsibility TryHandle returns false when no handler matches without default")]
    [Fact]
    public async Task Responsibility_TryHandle_With_Default()
    {
        await Given("a request router", () => new RequestRouter())
            .When("TryHandle for unknown route", router =>
            {
                var req = new HttpRequest("/unknown", "PUT", null);
                var handled = router.TryHandle(in req, out var result);
                return (handled, result);
            })
            .Then("should be handled by default", r => r.handled && r.result.Contains("404"))
            .AssertPassed();
    }

    [Scenario("Pipeline model wraps request with logging")]
    [Fact]
    public async Task Pipeline_Wraps_With_Logging()
    {
        await Given("a middleware pipeline", () => new MiddlewarePipeline())
            .When("handling 'hello'", pipeline =>
            {
                var input = "hello";
                return pipeline.Handle(in input);
            })
            .Then("should contain [LOG] prefix", result => result.Contains("[LOG]"))
            .And("should contain 200 OK", result => result.Contains("200 OK"))
            .AssertPassed();
    }

    [Scenario("Pipeline model short-circuits on auth denial")]
    [Fact]
    public async Task Pipeline_ShortCircuits_On_Auth_Denial()
    {
        await Given("a middleware pipeline", () => new MiddlewarePipeline())
            .When("handling 'DENY request'", pipeline =>
            {
                var input = "DENY request";
                return pipeline.Handle(in input);
            })
            .Then("should return 403 with logging", result => result.Contains("403 Forbidden"))
            .AssertPassed();
    }
}
