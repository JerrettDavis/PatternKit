using PatternKit.Cloud.ExternalConfigurationStore;
using TinyBDD;

namespace PatternKit.Tests.Cloud.ExternalConfigurationStore;

public sealed class ExternalConfigurationStoreTests
{
    [Scenario("GetAsync LoadsAndValidatesSettings")]
    [Fact]
    public async Task GetAsync_LoadsAndValidatesSettings()
    {
        var store = ExternalConfigurationStore<AppSettings>.Create("app-config")
            .LoadFrom(static _ => new ValueTask<ExternalConfigurationSnapshot<AppSettings>>(
                new ExternalConfigurationSnapshot<AppSettings>(new("https://api.example.com", true), "v1", DateTimeOffset.UtcNow)))
            .ValidateWith("Endpoint is required.", static settings => !string.IsNullOrWhiteSpace(settings.Endpoint))
            .Build();

        var result = await store.GetAsync();

        ScenarioExpect.True(result.Succeeded);
        ScenarioExpect.Equal("app-config", result.StoreName);
        ScenarioExpect.Equal("v1", result.Snapshot.Version);
        ScenarioExpect.Equal("https://api.example.com", result.Snapshot.Settings.Endpoint);
    }

    [Scenario("GetAsync RejectsInvalidSettings")]
    [Fact]
    public async Task GetAsync_RejectsInvalidSettings()
    {
        var store = ExternalConfigurationStore<AppSettings>.Create()
            .LoadFrom(static _ => new ValueTask<ExternalConfigurationSnapshot<AppSettings>>(
                new ExternalConfigurationSnapshot<AppSettings>(new("", true), "v1", DateTimeOffset.UtcNow)))
            .ValidateWith("Endpoint is required.", static settings => !string.IsNullOrWhiteSpace(settings.Endpoint))
            .Build();

        var result = await store.GetAsync();

        ScenarioExpect.False(result.Succeeded);
        ScenarioExpect.Equal("Endpoint is required.", result.RejectionReason);
    }

    [Scenario("GetAsync ReusesFreshCachedSnapshot")]
    [Fact]
    public async Task GetAsync_ReusesFreshCachedSnapshot()
    {
        var loads = 0;
        var store = ExternalConfigurationStore<AppSettings>.Create()
            .LoadFrom(_ =>
            {
                loads++;
                return new ValueTask<ExternalConfigurationSnapshot<AppSettings>>(
                    new ExternalConfigurationSnapshot<AppSettings>(new("https://api.example.com", true), $"v{loads}", DateTimeOffset.UtcNow));
            })
            .CacheFor(TimeSpan.FromMinutes(1))
            .Build();

        var first = await store.GetAsync();
        var second = await store.GetAsync();

        ScenarioExpect.Equal(1, loads);
        ScenarioExpect.Equal(first.Snapshot.Version, second.Snapshot.Version);
    }

    [Scenario("Builder RejectsInvalidConfiguration")]
    [Fact]
    public void Builder_RejectsInvalidConfiguration()
    {
        ScenarioExpect.Throws<ArgumentException>(() => ExternalConfigurationStore<AppSettings>.Create(""));
        ScenarioExpect.Throws<ArgumentNullException>(() => ExternalConfigurationStore<AppSettings>.Create().LoadFrom(null!));
        ScenarioExpect.Throws<ArgumentException>(() => ExternalConfigurationStore<AppSettings>.Create().ValidateWith("", static _ => true));
        ScenarioExpect.Throws<ArgumentNullException>(() => ExternalConfigurationStore<AppSettings>.Create().ValidateWith("invalid", null!));
        ScenarioExpect.Throws<ArgumentOutOfRangeException>(() => ExternalConfigurationStore<AppSettings>.Create().CacheFor(TimeSpan.FromMilliseconds(-1)));
        ScenarioExpect.Throws<InvalidOperationException>(() => ExternalConfigurationStore<AppSettings>.Create().Build());
        ScenarioExpect.Throws<ArgumentNullException>(() => new ExternalConfigurationSnapshot<AppSettings>(null!, "v1", DateTimeOffset.UtcNow));
        ScenarioExpect.Throws<ArgumentException>(() => new ExternalConfigurationSnapshot<AppSettings>(new("endpoint", true), "", DateTimeOffset.UtcNow));
    }

    private sealed record AppSettings(string Endpoint, bool Enabled);
}
