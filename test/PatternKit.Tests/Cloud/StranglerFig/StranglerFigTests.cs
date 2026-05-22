using PatternKit.Cloud.StranglerFig;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Cloud.StranglerFig;

[Feature("Strangler Fig")]
public sealed class StranglerFigTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Strangler Fig routes migrated traffic to modern implementation")]
    [Fact]
    public Task Strangler_Fig_Routes_Migrated_Traffic_To_Modern_Implementation()
        => Given("a checkout migration with a migrated tenant", CreateMigration)
        .When("the migrated tenant is routed", migration => migration.Route(new CheckoutRequest("enterprise", "O-100")))
        .Then("the modern implementation handles the request", result =>
        {
            ScenarioExpect.True(result.UsedModern);
            ScenarioExpect.Equal("enterprise-cutover", result.Decision.RuleName);
            ScenarioExpect.Equal("modern:O-100", result.Response.Confirmation);
        })
        .AssertPassed();

    [Scenario("Strangler Fig keeps unmigrated traffic on legacy implementation")]
    [Fact]
    public Task Strangler_Fig_Keeps_Unmigrated_Traffic_On_Legacy_Implementation()
        => Given("a checkout migration with a fallback", CreateMigration)
        .When("an unmigrated tenant is routed", migration => migration.Route(new CheckoutRequest("retail", "O-200")))
        .Then("the legacy implementation handles the request", result =>
        {
            ScenarioExpect.True(result.UsedLegacy);
            ScenarioExpect.Equal("fallback", result.Decision.RuleName);
            ScenarioExpect.Equal("legacy:O-200", result.Response.Confirmation);
        })
        .AssertPassed();

    [Scenario("Strangler Fig validates migration configuration")]
    [Fact]
    public Task Strangler_Fig_Validates_Migration_Configuration()
        => Given("invalid Strangler Fig inputs", () => true)
        .Then("invalid names are rejected", _ =>
            ScenarioExpect.Throws<ArgumentException>(() => StranglerFig<CheckoutRequest, CheckoutResponse>.Create("")
                .RouteToModern("enterprise", IsEnterprise)
                .Legacy(Legacy)
                .Modern(Modern)
                .Build()))
        .And("missing routes are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => StranglerFig<CheckoutRequest, CheckoutResponse>.Create().Legacy(Legacy).Modern(Modern).Build()))
        .And("missing handlers are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => StranglerFig<CheckoutRequest, CheckoutResponse>.Create().RouteToModern("enterprise", IsEnterprise).Build()))
        .And("duplicate route names are rejected", _ =>
            ScenarioExpect.Throws<InvalidOperationException>(() => StranglerFig<CheckoutRequest, CheckoutResponse>.Create()
                .RouteToModern("enterprise", IsEnterprise)
                .RouteToModern("ENTERPRISE", IsEnterprise)))
        .And("null requests are rejected", _ =>
            ScenarioExpect.Throws<ArgumentNullException>(() => CreateMigration().Route(null!)))
        .AssertPassed();

    private static StranglerFig<CheckoutRequest, CheckoutResponse> CreateMigration()
        => StranglerFig<CheckoutRequest, CheckoutResponse>.Create("checkout-migration")
            .RouteToModern("enterprise-cutover", IsEnterprise)
            .Legacy(Legacy)
            .Modern(Modern)
            .Build();

    private static bool IsEnterprise(CheckoutRequest request) => request.Tenant == "enterprise";

    private static CheckoutResponse Legacy(CheckoutRequest request) => new($"legacy:{request.OrderId}");

    private static CheckoutResponse Modern(CheckoutRequest request) => new($"modern:{request.OrderId}");

    private sealed record CheckoutRequest(string Tenant, string OrderId);

    private sealed record CheckoutResponse(string Confirmation);
}
