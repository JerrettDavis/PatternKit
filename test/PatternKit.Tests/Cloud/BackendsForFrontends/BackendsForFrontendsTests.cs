using PatternKit.Cloud.BackendsForFrontends;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.BackendsForFrontends;

[Feature("Backends for Frontends")]
public sealed class BackendsForFrontendsTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Backends for Frontends dispatches to the matching frontend")]
    [Fact]
    public Task Backends_For_Frontends_Dispatches_To_The_Matching_Frontend()
        => Given("a commerce BFF with web and mobile frontends", CreateBff)
        .When("a mobile checkout summary is requested", bff => bff.Dispatch(new ClientRequest("mobile", "C-100")))
        .Then("the mobile response is shaped for the client experience", result =>
        {
            ScenarioExpect.True(result.Handled);
            ScenarioExpect.Equal("commerce-bff", result.GatewayName);
            ScenarioExpect.Equal("mobile", result.FrontendName);
            ScenarioExpect.Equal("compact", result.Response!.Shape);
        })
        .AssertPassed();

    [Scenario("Backends for Frontends uses fallback when no frontend matches")]
    [Fact]
    public Task Backends_For_Frontends_Uses_Fallback_When_No_Frontend_Matches()
        => Given("a commerce BFF with a default frontend", CreateBff)
        .When("an unrecognized client requests a summary", bff => bff.Dispatch(new ClientRequest("partner", "C-100")))
        .Then("the fallback response is returned", result =>
        {
            ScenarioExpect.True(result.Handled);
            ScenarioExpect.Equal("fallback", result.FrontendName);
            ScenarioExpect.Equal("standard", result.Response!.Shape);
        })
        .AssertPassed();

    [Scenario("Backends for Frontends validates configuration and reports failures")]
    [Fact]
    public Task Backends_For_Frontends_Validates_Configuration_And_Reports_Failures()
        => Given("invalid BFF inputs", () => true)
        .Then("empty gateway names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => BackendsForFrontends<ClientRequest, ClientResponse>.Create("")
                .Frontend("web", MatchWeb, HandleWeb)
                .Build()))
        .And("missing frontends are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => BackendsForFrontends<ClientRequest, ClientResponse>.Create().Build()))
        .And("duplicate frontends are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => BackendsForFrontends<ClientRequest, ClientResponse>.Create()
                .Frontend("web", MatchWeb, HandleWeb)
                .Frontend("WEB", MatchWeb, HandleWeb)))
        .And("null requests are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateBff().Dispatch(null!)))
        .And("handler failures are explicit result failures", _ =>
        {
            var bff = BackendsForFrontends<ClientRequest, ClientResponse>.Create("commerce-bff")
                .Frontend("web", MatchWeb, static _ => throw new InvalidOperationException("backend unavailable"))
                .Build();
            var result = bff.Dispatch(new ClientRequest("web", "C-100"));
            ScenarioExpect.True(result.Failed);
            ScenarioExpect.Equal("web", result.FrontendName);
            ScenarioExpect.Contains("backend unavailable", result.Exception!.Message);
        })
        .AssertPassed();

    private static BackendsForFrontends<ClientRequest, ClientResponse> CreateBff()
        => BackendsForFrontends<ClientRequest, ClientResponse>.Create("commerce-bff")
            .Frontend("mobile", static request => request.Client == "mobile", static ctx => new(ctx.Request.CustomerId, "compact"))
            .Frontend("web", MatchWeb, HandleWeb)
            .Fallback(static ctx => new(ctx.Request.CustomerId, "standard"))
            .Build();

    private static bool MatchWeb(ClientRequest request) => request.Client == "web";

    private static ClientResponse HandleWeb(BackendsForFrontendsContext<ClientRequest> ctx) => new(ctx.Request.CustomerId, "rich");

    private sealed record ClientRequest(string Client, string CustomerId);

    private sealed record ClientResponse(string CustomerId, string Shape);
}
