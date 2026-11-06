using Microsoft.Extensions.DependencyInjection;
using PatternKit.Examples.FeatureToggleDemo;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.FeatureToggleDemo;

[Feature("Checkout Feature Toggle demo")]
public sealed partial class CheckoutFeatureToggleDemoTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    [Scenario("Checkout Feature Toggle demo evaluates checkout rules")]
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Checkout_Feature_Toggle_Demo_Evaluates_Checkout_Rules(bool sourceGenerated)
        => Given("the checkout feature toggle demo", () => sourceGenerated)
        .When("the selected path runs", generated =>
            generated
                ? CheckoutFeatureToggleDemo.RunGenerated()
                : CheckoutFeatureToggleDemo.RunFluent())
        .Then("checkout toggles are evaluated for the request context", summary =>
        {
            ScenarioExpect.Equal("checkout-features", summary.ToggleSetName);
            ScenarioExpect.True(summary.FraudReviewEnabled);
            ScenarioExpect.Equal(!sourceGenerated, summary.NewCheckoutEnabled);
            ScenarioExpect.Equal(!sourceGenerated, summary.LoyaltyOffersEnabled);
        })
        .AssertPassed();

    [Scenario("Checkout Feature Toggle demo is importable through IServiceCollection")]
    [Fact]
    public Task Checkout_Feature_Toggle_Demo_Is_Importable_Through_IServiceCollection()
        => Given("a service provider with checkout feature toggles", () =>
        {
            var services = new ServiceCollection();
            services.AddCheckoutFeatureToggleDemo();
            return services.BuildServiceProvider(validateScopes: true);
        })
        .When("a scoped workflow evaluates a checkout request", provider =>
        {
            using (provider)
            using (var scope = provider.CreateScope())
            {
                var workflow = scope.ServiceProvider.GetRequiredService<CheckoutFeatureToggleWorkflow>();
                return workflow.Evaluate("beta", "Gold", 650m);
            }
        })
        .Then("the imported toggle set enables the expected features", summary =>
        {
            ScenarioExpect.Equal("checkout-features", summary.ToggleSetName);
            ScenarioExpect.True(summary.NewCheckoutEnabled);
            ScenarioExpect.True(summary.FraudReviewEnabled);
            ScenarioExpect.True(summary.LoyaltyOffersEnabled);
        })
        .AssertPassed();
}
