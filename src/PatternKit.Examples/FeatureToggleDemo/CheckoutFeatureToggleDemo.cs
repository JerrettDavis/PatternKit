using Microsoft.Extensions.DependencyInjection;
using PatternKit.Application.FeatureToggles;
using PatternKit.Generators.FeatureToggles;

namespace PatternKit.Examples.FeatureToggleDemo;

public static class CheckoutFeatureToggleDemo
{
    public static CheckoutFeatureToggleSummary RunFluent()
    {
        var toggles = CheckoutFeatureTogglePolicies.CreateFluentToggleSet();
        return RunScenario(toggles, new CheckoutFeatureContext("beta", "Gold", 650m));
    }

    public static CheckoutFeatureToggleSummary RunGenerated()
        => RunScenario(GeneratedCheckoutFeatureToggles.CreateToggles(), new CheckoutFeatureContext("standard", "Silver", 650m));

    private static CheckoutFeatureToggleSummary RunScenario(IFeatureToggleSet<CheckoutFeatureContext> toggles, CheckoutFeatureContext context)
    {
        var newCheckout = toggles.Evaluate("new-checkout", context);
        var fraudReview = toggles.Evaluate("fraud-review", context);
        var loyaltyOffers = toggles.Evaluate("loyalty-offers", context);
        return new(toggles.Name, newCheckout.Enabled, fraudReview.Enabled, loyaltyOffers.Enabled);
    }
}

public sealed record CheckoutFeatureContext(string Tenant, string LoyaltyTier, decimal Total);

public sealed record CheckoutFeatureToggleSummary(
    string ToggleSetName,
    bool NewCheckoutEnabled,
    bool FraudReviewEnabled,
    bool LoyaltyOffersEnabled);

public static class CheckoutFeatureTogglePolicies
{
    public static FeatureToggleSet<CheckoutFeatureContext> CreateFluentToggleSet()
        => FeatureToggleSet<CheckoutFeatureContext>.Create("checkout-features")
            .AddRule("new-checkout", false, static context => context.Tenant == "beta")
            .AddRule("fraud-review", false, static context => context.Total >= 500m)
            .AddRule("loyalty-offers", false, static context => context.LoyaltyTier == "Gold")
            .Build();
}

public sealed class CheckoutFeatureToggleWorkflow
{
    private readonly IFeatureToggleSet<CheckoutFeatureContext> _toggles;

    public CheckoutFeatureToggleWorkflow(IFeatureToggleSet<CheckoutFeatureContext> toggles)
    {
        _toggles = toggles;
    }

    public CheckoutFeatureToggleSummary Evaluate(string tenant, string loyaltyTier, decimal total)
        => new(
            _toggles.Name,
            _toggles.IsEnabled("new-checkout", new CheckoutFeatureContext(tenant, loyaltyTier, total)),
            _toggles.IsEnabled("fraud-review", new CheckoutFeatureContext(tenant, loyaltyTier, total)),
            _toggles.IsEnabled("loyalty-offers", new CheckoutFeatureContext(tenant, loyaltyTier, total)));
}

public sealed record CheckoutFeatureToggleDemoRunner(
    Func<CheckoutFeatureToggleSummary> RunFluent,
    Func<CheckoutFeatureToggleSummary> RunGenerated);

public static class CheckoutFeatureToggleServiceCollectionExtensions
{
    public static IServiceCollection AddCheckoutFeatureToggleDemo(this IServiceCollection services)
    {
        services.AddScoped<IFeatureToggleSet<CheckoutFeatureContext>>(_ => CheckoutFeatureTogglePolicies.CreateFluentToggleSet());
        services.AddScoped<CheckoutFeatureToggleWorkflow>();
        services.AddSingleton(new CheckoutFeatureToggleDemoRunner(
            CheckoutFeatureToggleDemo.RunFluent,
            CheckoutFeatureToggleDemo.RunGenerated));
        return services;
    }
}

[GenerateFeatureToggleSet(typeof(CheckoutFeatureContext), FactoryName = "CreateToggles", SetName = "checkout-features")]
public static partial class GeneratedCheckoutFeatureToggles
{
    [FeatureToggleRule("new-checkout")]
    private static bool IsBetaTenant(CheckoutFeatureContext context) => context.Tenant == "beta";

    [FeatureToggleRule("fraud-review")]
    private static bool IsLargeOrder(CheckoutFeatureContext context) => context.Total >= 500m;

    [FeatureToggleRule("loyalty-offers")]
    private static bool IsGoldTier(CheckoutFeatureContext context) => context.LoyaltyTier == "Gold";
}
