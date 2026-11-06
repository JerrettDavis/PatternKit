using PatternKit.Application.FeatureToggles;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Application.FeatureToggles;

[Feature("Feature Toggle")]
public sealed partial class FeatureToggleSetTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Feature Toggle set evaluates static and contextual rules")]
    [Fact]
    public Task Feature_Toggle_Set_Evaluates_Static_And_Contextual_Rules()
        => Given("a checkout feature toggle set", () => FeatureToggleSet<CheckoutContext>.Create("checkout")
            .AddStatic("legacy-checkout", true)
            .AddRule("new-checkout", false, static ctx => ctx.Tenant == "beta")
            .AddRule("fraud-review", false, static ctx => ctx.Total >= 500m)
            .Build())
        .When("toggle decisions are evaluated", set => new
        {
            Legacy = set.Evaluate("legacy-checkout", new CheckoutContext("standard", 20m)),
            NewCheckoutForBeta = set.Evaluate("new-checkout", new CheckoutContext("beta", 20m)),
            NewCheckoutForStandard = set.Evaluate("new-checkout", new CheckoutContext("standard", 20m)),
            FraudReview = set.Evaluate("fraud-review", new CheckoutContext("standard", 600m)),
            Missing = set.Evaluate("missing", new CheckoutContext("standard", 20m))
        })
        .Then("the toggle decisions include enabled state and missing state", result =>
        {
            ScenarioExpect.True(result.Legacy.Enabled);
            ScenarioExpect.True(result.NewCheckoutForBeta.Enabled);
            ScenarioExpect.False(result.NewCheckoutForStandard.Enabled);
            ScenarioExpect.True(result.FraudReview.Enabled);
            ScenarioExpect.False(result.Missing.Enabled);
            ScenarioExpect.False(result.Missing.Found);
        })
        .AssertPassed();

    [Scenario("Feature Toggle set validates required configuration")]
    [Fact]
    public Task Feature_Toggle_Set_Validates_Required_Configuration()
        => Given("feature toggle builders", () => true)
        .Then("invalid arguments are rejected", _ =>
        {
            ScenarioExpect.Throws<ArgumentException>(() => FeatureToggleSet<CheckoutContext>.Create(""));
            ScenarioExpect.Throws<ArgumentException>(() => FeatureToggleRule<CheckoutContext>.Static("", true));
            ScenarioExpect.Throws<ArgumentNullException>(() => FeatureToggleRule<CheckoutContext>.Conditional("x", false, null!));
            ScenarioExpect.Throws<ArgumentException>(() => FeatureToggleDecision.Configured("", true, "reason"));
            ScenarioExpect.Throws<ArgumentException>(() => FeatureToggleDecision.Configured("x", true, ""));
            ScenarioExpect.Throws<ArgumentNullException>(() => FeatureToggleSet<CheckoutContext>.Create("checkout").UseComparer(null!));
            ScenarioExpect.Throws<InvalidOperationException>(() => FeatureToggleSet<CheckoutContext>.Create("checkout")
                .AddStatic("x", true)
                .AddStatic("x", false)
                .Build());

            var set = FeatureToggleSet<CheckoutContext>.Create("checkout").Build();
            ScenarioExpect.Throws<ArgumentException>(() => set.Evaluate("", new CheckoutContext("standard", 1m)));
        })
        .AssertPassed();

    private sealed record CheckoutContext(string Tenant, decimal Total);
}
