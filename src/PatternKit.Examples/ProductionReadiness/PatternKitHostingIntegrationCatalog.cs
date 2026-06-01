using Microsoft.Extensions.DependencyInjection;

namespace PatternKit.Examples.ProductionReadiness;

/// <summary>
/// Describes the reusable hosting integration available for a catalog pattern.
/// </summary>
public enum PatternHostingIntegrationKind
{
    /// <summary>
    /// The pattern is importable through its production example registration.
    /// </summary>
    ExampleServiceCollection,

    /// <summary>
    /// The pattern has a reusable extension in PatternKit.Hosting.Extensions.
    /// </summary>
    ReusableHostingExtension
}

/// <summary>
/// Describes how one catalog pattern integrates with Microsoft.Extensions.DependencyInjection.
/// </summary>
public sealed record PatternHostingIntegrationDescriptor(
    string PatternName,
    PatternFamily Family,
    PatternHostingIntegrationKind Kind,
    string RegistrationApi,
    string DocumentationPath,
    string TestPath,
    string Notes);

/// <summary>
/// Read-only catalog of PatternKit hosting integration coverage.
/// </summary>
public interface IPatternKitHostingIntegrationCatalog
{
    IReadOnlyList<PatternHostingIntegrationDescriptor> Integrations { get; }
}

/// <summary>
/// Pattern-to-IServiceCollection audit manifest used by docs, tests, and benchmarks.
/// </summary>
public sealed class PatternKitHostingIntegrationCatalog : IPatternKitHostingIntegrationCatalog
{
    private const string HostingDocumentationPath = "docs/guides/hosting-extensions.md";
    private const string HostingTestPath = "test/PatternKit.Hosting.Extensions.Tests/DependencyInjection/PatternKitServiceCollectionExtensionsTests.cs";
    private const string ExampleDocumentationPath = "docs/examples/production-ready-integrations.md";
    private const string ExampleTestPath = "test/PatternKit.Examples.Tests/DependencyInjection/PatternKitExampleDependencyInjectionTests.cs";

    private static readonly IReadOnlyDictionary<string, string> ReusableHostingExtensions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Message Channel"] = "AddPatternKitMessageChannel<TPayload>",
            ["Message Store"] = "AddPatternKitMessageStore<TPayload>",
            ["Guaranteed Delivery"] = "AddPatternKitGuaranteedDelivery<TPayload>",
            ["Retry"] = "AddPatternKitRetryPolicy<TResult>",
            ["Circuit Breaker"] = "AddPatternKitCircuitBreakerPolicy<TResult>",
            ["Bulkhead"] = "AddPatternKitBulkheadPolicy<TResult>",
            ["Rate Limiting"] = "AddPatternKitRateLimitPolicy<TResult>",
            ["Queue-Based Load Leveling"] = "AddPatternKitQueueLoadLevelingPolicy<TResult>",
            ["Priority Queue"] = "AddPatternKitPriorityQueue<TItem, TPriority>",
            ["Backpressure"] = "AddPatternKitBackpressurePolicy<TResult>",
            ["Null Object"] = "AddPatternKitNullObject<TContract>"
        };

    private static readonly Lazy<IReadOnlyList<PatternHostingIntegrationDescriptor>> LazyIntegrations =
        new(CreateIntegrations);

    public IReadOnlyList<PatternHostingIntegrationDescriptor> Integrations => LazyIntegrations.Value;

    private static IReadOnlyList<PatternHostingIntegrationDescriptor> CreateIntegrations()
    {
        var catalog = new PatternKitPatternCatalog();
        return catalog.Patterns
            .Select(static pattern =>
            {
                if (ReusableHostingExtensions.TryGetValue(pattern.Name, out var registrationApi))
                {
                    return new PatternHostingIntegrationDescriptor(
                        pattern.Name,
                        pattern.Family,
                        PatternHostingIntegrationKind.ReusableHostingExtension,
                        registrationApi,
                        HostingDocumentationPath,
                        HostingTestPath,
                        "Reusable PatternKit.Hosting.Extensions registration for application-owned services.");
                }

                return new PatternHostingIntegrationDescriptor(
                    pattern.Name,
                    pattern.Family,
                    PatternHostingIntegrationKind.ExampleServiceCollection,
                    "AddPatternKitExamples",
                    ExampleDocumentationPath,
                    ExampleTestPath,
                    "Importable through the production example catalog while reusable package-level hosting APIs are evaluated.");
            })
            .ToArray();
    }
}

public static class PatternKitHostingIntegrationCatalogServiceCollectionExtensions
{
    public static IServiceCollection AddPatternKitHostingIntegrationCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IPatternKitHostingIntegrationCatalog, PatternKitHostingIntegrationCatalog>();
        return services;
    }
}
