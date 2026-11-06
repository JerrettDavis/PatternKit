using Microsoft.Extensions.DependencyInjection;
using PatternKit.Generators.Messaging;
using PatternKit.Messaging;
using PatternKit.Messaging.Transformation;

namespace PatternKit.Examples.Messaging;

/// <summary>Customer profile command used by the content-enricher example.</summary>
public sealed record CustomerProfileUpdate(string CustomerId, string? Email, string? Tier, bool MarketingOptIn);

/// <summary>Summary returned by the customer profile content-enricher example.</summary>
public sealed record CustomerProfileEnrichmentSummary(string CustomerId, string Email, string Tier, bool MarketingOptIn, int AppliedSteps);

/// <summary>Service that enriches customer profile messages before profile writes.</summary>
public sealed class CustomerProfileEnrichmentService(AsyncContentEnricher<CustomerProfileUpdate> enricher)
{
    public async ValueTask<CustomerProfileEnrichmentSummary> EnrichAsync(
        CustomerProfileUpdate update,
        CancellationToken cancellationToken = default)
    {
        var result = await enricher.EnrichAsync(Message<CustomerProfileUpdate>.Create(update), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var payload = result.Message.Payload;
        return new CustomerProfileEnrichmentSummary(
            payload.CustomerId,
            payload.Email ?? string.Empty,
            payload.Tier ?? string.Empty,
            payload.MarketingOptIn,
            result.StepResults.Count(step => step.Applied));
    }
}

/// <summary>Fluent content-enricher builder used when source generators are not enabled.</summary>
public static class CustomerProfileContentEnrichers
{
    public static AsyncContentEnricher<CustomerProfileUpdate> Create()
        => AsyncContentEnricher<CustomerProfileUpdate>.Create("customer-profile-enrichment")
            .WithDefaultPolicy(EnrichmentErrorPolicy.Skip)
            .Enrich(
                "normalize-email",
                static (profile, _, _) => ValueTask.FromResult(profile with { Email = NormalizeEmail(profile.Email) }))
            .Enrich(
                "default-tier",
                static (profile, _, _) => ValueTask.FromResult(profile.Tier is null ? throw new InvalidOperationException("Tier missing.") : profile),
                EnrichmentErrorPolicy.UseDefault,
                static profile => profile with { Tier = "Standard" })
            .Enrich(
                "marketing-opt-in",
                static (profile, _, _) => ValueTask.FromResult(profile with { MarketingOptIn = profile.Tier == "Premium" }))
            .Build();

    private static string NormalizeEmail(string? email)
        => string.IsNullOrWhiteSpace(email) ? "unknown@example.com" : email.Trim().ToLowerInvariant();
}

/// <summary>Source-generated content enricher for customer profile imports.</summary>
[GenerateContentEnricher(
    typeof(CustomerProfileUpdate),
    FactoryName = "Create",
    EnricherName = "customer-profile-enrichment",
    DefaultPolicy = ContentEnrichmentErrorPolicy.Skip)]
public static partial class GeneratedCustomerProfileContentEnricher
{
    [ContentEnrichmentStep("normalize-email", Order = 10)]
    private static ValueTask<CustomerProfileUpdate> NormalizeEmail(
        CustomerProfileUpdate profile,
        MessageContext context,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(profile with
        {
            Email = string.IsNullOrWhiteSpace(profile.Email)
                ? "unknown@example.com"
                : profile.Email.Trim().ToLowerInvariant()
        });

    [ContentEnrichmentStep(
        "default-tier",
        Order = 20,
        Policy = ContentEnrichmentErrorPolicy.UseDefault,
        DefaultFactoryName = nameof(DefaultTier))]
    private static ValueTask<CustomerProfileUpdate> RequireTier(
        CustomerProfileUpdate profile,
        MessageContext context,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(profile.Tier is null ? throw new InvalidOperationException("Tier missing.") : profile);

    [ContentEnrichmentStep("marketing-opt-in", Order = 30)]
    private static ValueTask<CustomerProfileUpdate> ApplyMarketingPolicy(
        CustomerProfileUpdate profile,
        MessageContext context,
        CancellationToken cancellationToken)
        => ValueTask.FromResult(profile with { MarketingOptIn = profile.Tier == "Premium" });

    private static CustomerProfileUpdate DefaultTier(CustomerProfileUpdate profile)
        => profile with { Tier = "Standard" };
}

/// <summary>Runner that demonstrates both fluent and generated content-enricher paths.</summary>
public sealed class CustomerProfileContentEnricherExampleRunner(CustomerProfileEnrichmentService service)
{
    public ValueTask<CustomerProfileEnrichmentSummary> RunGeneratedAsync(CustomerProfileUpdate update, CancellationToken cancellationToken = default)
        => service.EnrichAsync(update, cancellationToken);

    public static async ValueTask<CustomerProfileEnrichmentSummary> RunFluentAsync(
        CustomerProfileUpdate update,
        CancellationToken cancellationToken = default)
    {
        var result = await CustomerProfileContentEnrichers.Create()
            .EnrichAsync(Message<CustomerProfileUpdate>.Create(update), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var payload = result.Message.Payload;
        return new CustomerProfileEnrichmentSummary(
            payload.CustomerId,
            payload.Email ?? string.Empty,
            payload.Tier ?? string.Empty,
            payload.MarketingOptIn,
            result.StepResults.Count(step => step.Applied));
    }
}

/// <summary>DI helpers for importing the customer profile content-enricher example into standard .NET containers.</summary>
public static class CustomerProfileContentEnricherExampleServiceCollectionExtensions
{
    public static IServiceCollection AddCustomerProfileContentEnricherDemo(this IServiceCollection services)
    {
        services.AddSingleton(_ => GeneratedCustomerProfileContentEnricher.Create());
        services.AddSingleton<CustomerProfileEnrichmentService>();
        services.AddSingleton<CustomerProfileContentEnricherExampleRunner>();
        return services;
    }
}
