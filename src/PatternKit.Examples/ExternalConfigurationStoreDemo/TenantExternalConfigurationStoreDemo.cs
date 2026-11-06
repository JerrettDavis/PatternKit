using Microsoft.Extensions.DependencyInjection;
using PatternKit.Cloud.ExternalConfigurationStore;
using PatternKit.Generators.Cloud;

namespace PatternKit.Examples.ExternalConfigurationStoreDemo;

/// <summary>Tenant feature settings loaded from an external configuration source.</summary>
public sealed record TenantFeatureSettings(string TenantId, Uri ApiEndpoint, bool NewCheckoutEnabled);

/// <summary>Summary returned by the tenant external-configuration-store example.</summary>
public sealed record TenantExternalConfigurationSummary(bool Loaded, string Version, bool NewCheckoutEnabled, string? RejectionReason);

/// <summary>In-memory provider used to model an external configuration service.</summary>
public sealed class TenantConfigurationProvider
{
    private TenantFeatureSettings _settings = new("tenant-a", new Uri("https://api.example.com/tenant-a"), true);
    private string _version = "v1";

    public void Replace(TenantFeatureSettings settings, string version)
        => (_settings, _version) = (settings, version);

    public ValueTask<ExternalConfigurationSnapshot<TenantFeatureSettings>> LoadAsync(CancellationToken cancellationToken)
        => new(new ExternalConfigurationSnapshot<TenantFeatureSettings>(_settings, _version, DateTimeOffset.UtcNow));
}

/// <summary>Provider registry used by source-generated static loader methods.</summary>
public static class TenantConfigurationProviderRegistry
{
    public static TenantConfigurationProvider Provider { get; set; } = new();
}

/// <summary>Fluent external-configuration-store builder for non-generator consumers.</summary>
public static class TenantExternalConfigurationStores
{
    public static ExternalConfigurationStore<TenantFeatureSettings> Create(TenantConfigurationProvider provider)
        => ExternalConfigurationStore<TenantFeatureSettings>.Create("tenant-feature-config")
            .LoadFrom(provider.LoadAsync)
            .ValidateWith("Tenant id is required.", static settings => !string.IsNullOrWhiteSpace(settings.TenantId))
            .ValidateWith("API endpoint must be absolute.", static settings => settings.ApiEndpoint.IsAbsoluteUri)
            .CacheFor(TimeSpan.FromMinutes(5))
            .Build();
}

/// <summary>Source-generated external configuration store for tenant feature settings.</summary>
[GenerateExternalConfigurationStore(
    typeof(TenantFeatureSettings),
    FactoryName = "Create",
    StoreName = "tenant-feature-config",
    CacheMilliseconds = 300000)]
public static partial class GeneratedTenantExternalConfigurationStore
{
    [ExternalConfigurationLoader]
    private static ValueTask<ExternalConfigurationSnapshot<TenantFeatureSettings>> Load(CancellationToken cancellationToken)
        => TenantConfigurationProviderRegistry.Provider.LoadAsync(cancellationToken);

    [ExternalConfigurationValidator("Tenant id is required.", 10)]
    private static bool HasTenant(TenantFeatureSettings settings)
        => !string.IsNullOrWhiteSpace(settings.TenantId);

    [ExternalConfigurationValidator("API endpoint must be absolute.", 20)]
    private static bool HasAbsoluteEndpoint(TenantFeatureSettings settings)
        => settings.ApiEndpoint.IsAbsoluteUri;
}

/// <summary>Service that reads tenant feature settings from the generated external configuration store.</summary>
public sealed class TenantExternalConfigurationService(ExternalConfigurationStore<TenantFeatureSettings> store)
{
    public async ValueTask<TenantExternalConfigurationSummary> LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await store.GetAsync(cancellationToken);
        return new TenantExternalConfigurationSummary(
            result.Succeeded,
            result.Snapshot.Version,
            result.Succeeded && result.Snapshot.Settings.NewCheckoutEnabled,
            result.RejectionReason);
    }
}

/// <summary>Runner that demonstrates both fluent and generated external-configuration-store paths.</summary>
public sealed class TenantExternalConfigurationStoreDemoRunner(TenantExternalConfigurationService service)
{
    public ValueTask<TenantExternalConfigurationSummary> RunGeneratedAsync(CancellationToken cancellationToken = default)
        => service.LoadAsync(cancellationToken);

    public static async ValueTask<TenantExternalConfigurationSummary> RunFluentAsync(CancellationToken cancellationToken = default)
    {
        var provider = new TenantConfigurationProvider();
        var store = TenantExternalConfigurationStores.Create(provider);
        var result = await store.GetAsync(cancellationToken);
        return new TenantExternalConfigurationSummary(
            result.Succeeded,
            result.Snapshot.Version,
            result.Succeeded && result.Snapshot.Settings.NewCheckoutEnabled,
            result.RejectionReason);
    }
}

/// <summary>DI helpers for importing the tenant external configuration store example into standard .NET containers.</summary>
public static class TenantExternalConfigurationStoreServiceCollectionExtensions
{
    public static IServiceCollection AddTenantExternalConfigurationStoreDemo(this IServiceCollection services)
    {
        services.AddSingleton<TenantConfigurationProvider>();
        services.AddSingleton(sp =>
        {
            TenantConfigurationProviderRegistry.Provider = sp.GetRequiredService<TenantConfigurationProvider>();
            return GeneratedTenantExternalConfigurationStore.Create();
        });
        services.AddSingleton<TenantExternalConfigurationService>();
        services.AddSingleton<TenantExternalConfigurationStoreDemoRunner>();
        return services;
    }
}
